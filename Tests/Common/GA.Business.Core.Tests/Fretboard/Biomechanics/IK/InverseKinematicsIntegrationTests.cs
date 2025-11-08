namespace GA.Business.Core.Tests.Fretboard.Biomechanics.IK;

using System.Text;
using Core.Fretboard.Analysis;
using Core.Fretboard.Biomechanics;

[TestFixture]
public class InverseKinematicsIntegrationTests
{
    private const float WristTolerance = 0.25f;
    private const double ScaleLengthMm = PhysicalFretboardCalculator.ScaleLengths.Electric;
    private const double NutWidthMm = 43.0;
    private const double BridgeWidthMm = 52.0;
    private const double FingerOffsetMm = 1.5;

    [Test]
    public void Solve_RoundtripPose_ShouldRecoverHighReachability()
    {
        var (hand, target, targetPose) = BuildRoundtripTarget();

        var config = new IkSolverConfig
        {
            PopulationSize = 80,
            Generations = 120,
            RandomSeed = 123,
            UseLocalSearch = true,
            UseGpuAcceleration = true
        };

        var solver = new InverseKinematicsSolver(config);
        TestContext.WriteLine("Solving IK roundtrip pose with GPU acceleration...");
        var solution = solver.Solve(hand, target);
        LogSolutionMetrics(solution);

        Assert.That(solution.FitnessDetails.Reachability, Is.GreaterThan(85.0));
        AssertJointSimilarity(targetPose, solution.BestPose);
    }

    [Test]
    public void Solve_RoundtripPose_ShouldWorkWithCpuFallback()
    {
        var (hand, target, targetPose) = BuildRoundtripTarget();

        var config = new IkSolverConfig
        {
            PopulationSize = 80,
            Generations = 120,
            RandomSeed = 456,
            UseLocalSearch = true,
            UseGpuAcceleration = false
        };

        var solver = new InverseKinematicsSolver(config);
        TestContext.WriteLine("Solving IK roundtrip pose with CPU fallback...");
        var solution = solver.Solve(hand, target);
        LogSolutionMetrics(solution);

        Assert.That(solution.FitnessDetails.Reachability, Is.GreaterThan(85.0));
        AssertJointSimilarity(targetPose, solution.BestPose);
    }

    [Test]
    public void CpuVsGpu_ShouldProduceComparableSolutions()
    {
        var (hand, target, _) = BuildRoundtripTarget();

        var baseConfig = new IkSolverConfig
        {
            PopulationSize = 90,
            Generations = 130,
            RandomSeed = 789,
            UseLocalSearch = true
        };

        var gpuSolver = new InverseKinematicsSolver(baseConfig with { UseGpuAcceleration = true });
        var cpuSolver = new InverseKinematicsSolver(baseConfig with { UseGpuAcceleration = false });

        var stopwatch = Stopwatch.StartNew();
        var gpuSolution = gpuSolver.Solve(hand, target);
        stopwatch.Stop();
        TestContext.WriteLine($"GPU solve time: {stopwatch.ElapsedMilliseconds} ms");

        stopwatch.Restart();
        var cpuSolution = cpuSolver.Solve(hand, target);
        stopwatch.Stop();
        TestContext.WriteLine($"CPU solve time: {stopwatch.ElapsedMilliseconds} ms");

        Assert.That(gpuSolution.FitnessDetails.Reachability, Is.GreaterThan(80.0));
        Assert.That(cpuSolution.FitnessDetails.Reachability, Is.GreaterThan(80.0));
        Assert.That(Math.Abs(gpuSolution.Fitness - cpuSolution.Fitness), Is.LessThan(1e-3));
    }

    [Test]
    public void Solver_ShouldHonorEnvironmentOverride()
    {
        var original = Environment.GetEnvironmentVariable("GA_IK_USE_GPU");
        try
        {
            Environment.SetEnvironmentVariable("GA_IK_USE_GPU", "false");
            var cpuForced = new InverseKinematicsSolver(new IkSolverConfig { UseGpuAcceleration = true });
            Assert.That(cpuForced.UsesGpu, Is.False);

            Environment.SetEnvironmentVariable("GA_IK_USE_GPU", "true");
            var gpuForced = new InverseKinematicsSolver(new IkSolverConfig { UseGpuAcceleration = false });
            Assert.That(gpuForced.UsesGpu, Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GA_IK_USE_GPU", original);
        }
    }

    [Test]
    public void Solve_CMajorOpenChord_ShouldFindPlayablePose()
    {
        var hand = HandModel.CreateStandardAdult();
        var config = new IkSolverConfig
        {
            PopulationSize = 120,
            Generations = 160,
            RandomSeed = 321,
            UseLocalSearch = true,
            UseGpuAcceleration = false
        };

        var cMajorFretPositions = ImmutableArray.Create(
            (String: 0, Fret: 0), // Low E (open)
            (String: 1, Fret: 3), // A string, 3rd fret (C)
            (String: 2, Fret: 2), // D string, 2nd fret (E)
            (String: 3, Fret: 0), // G (open)
            (String: 4, Fret: 1), // B string, 1st fret (C)
            (String: 5, Fret: 0)); // High E (open)

        var fingerAssignments = new Dictionary<FingerType, (int String, int Fret)>
        {
            [FingerType.Index] = (4, 1),
            [FingerType.Middle] = (2, 2),
            [FingerType.Ring] = (1, 3)
        };

        var target = CreateTarget("Cmaj (open)", fingerAssignments, cMajorFretPositions, 5.0f);
        var solver = new InverseKinematicsSolver(config);

        TestContext.WriteLine("Solving IK for open C major chord...");
        var solution = solver.Solve(hand, target);
        LogSolutionMetrics(solution);
        Assert.That(solution.FitnessDetails.Reachability, Is.GreaterThan(80.0));
        Assert.That(solution.IsAcceptable());

        var solvedFk = ForwardKinematics.ComputeFingertipPositions(solution.BestPose);
        AssertFingerPlacements(solvedFk, fingerAssignments, target);
    }

    [Test]
    public void Solve_FBarreShape_ShouldMaintainReachability()
    {
        var hand = HandModel.CreateStandardAdult();
        var config = new IkSolverConfig
        {
            PopulationSize = 140,
            Generations = 200,
            RandomSeed = 654,
            UseLocalSearch = true,
            UseGpuAcceleration = true
        };

        var fBarrePositions = ImmutableArray.Create(
            (String: 0, Fret: 1),
            (String: 1, Fret: 3),
            (String: 2, Fret: 3),
            (String: 3, Fret: 2),
            (String: 4, Fret: 1),
            (String: 5, Fret: 1));

        var fingerAssignments = new Dictionary<FingerType, (int String, int Fret)>
        {
            [FingerType.Index] = (4, 1),
            [FingerType.Middle] = (3, 2),
            [FingerType.Ring] = (2, 3),
            [FingerType.Little] = (1, 3)
        };

        var barreCoverage = new Dictionary<FingerType, ImmutableArray<int>>
        {
            [FingerType.Index] = ImmutableArray.Create(0, 4, 5)
        };

        var target = CreateTarget("F barre (E-shape)", fingerAssignments, fBarrePositions, 6.0f, barreCoverage);
        var solver = new InverseKinematicsSolver(config);

        TestContext.WriteLine("Solving IK for F barre chord...");
        var solution = solver.Solve(hand, target);
        LogSolutionMetrics(solution);
        Assert.That(solution.FitnessDetails.Reachability, Is.GreaterThan(78.0));
        Assert.That(solution.IsAcceptable(75.0));

        var solvedFk = ForwardKinematics.ComputeFingertipPositions(solution.BestPose);
        AssertFingerPlacements(solvedFk, fingerAssignments, target);
    }

    [Test]
    public void Solve_StretchVoicing_ShouldRespectConstraints()
    {
        var hand = HandModel.CreateStandardAdult();
        var config = new IkSolverConfig
        {
            PopulationSize = 160,
            Generations = 220,
            RandomSeed = 987,
            UseLocalSearch = true,
            UseGpuAcceleration = true
        };

        var stretchPositions = ImmutableArray.Create(
            (String: 0, Fret: 8),
            (String: 1, Fret: 7),
            (String: 2, Fret: 5),
            (String: 3, Fret: 0),
            (String: 4, Fret: 0),
            (String: 5, Fret: 9));

        var fingerAssignments = new Dictionary<FingerType, (int String, int Fret)>
        {
            [FingerType.Index] = (2, 5),
            [FingerType.Middle] = (1, 7),
            [FingerType.Ring] = (0, 8),
            [FingerType.Little] = (5, 9)
        };

        var target = CreateTarget("Gadd9 stretch", fingerAssignments, stretchPositions, 6.0f);
        var solver = new InverseKinematicsSolver(config);

        TestContext.WriteLine("Solving IK for Gadd9 stretch voicing...");
        var solution = solver.Solve(hand, target);
        LogSolutionMetrics(solution);
        Assert.That(solution.FitnessDetails.Reachability, Is.GreaterThan(75.0));
        Assert.That(solution.IsAcceptable(70.0));
        Assert.That(solution.FitnessDetails.Comfort, Is.GreaterThan(40.0));

        var solvedFk = ForwardKinematics.ComputeFingertipPositions(solution.BestPose);
        AssertFingerPlacements(solvedFk, fingerAssignments, target);
    }

    [Test]
    public void Solve_ThumbOverChord_ShouldPositionThumbCorrectly()
    {
        var hand = HandModel.CreateStandardAdult();
        var config = new IkSolverConfig
        {
            PopulationSize = 150,
            Generations = 210,
            RandomSeed = 246,
            UseLocalSearch = true,
            UseGpuAcceleration = true
        };

        var thumbOverPositions = ImmutableArray.Create(
            (String: 0, Fret: 2),
            (String: 1, Fret: 0),
            (String: 2, Fret: 0),
            (String: 3, Fret: 2),
            (String: 4, Fret: 3),
            (String: 5, Fret: 2));

        var fingerAssignments = new Dictionary<FingerType, (int String, int Fret)>
        {
            [FingerType.Thumb] = (0, 2),
            [FingerType.Index] = (3, 2),
            [FingerType.Middle] = (5, 2),
            [FingerType.Ring] = (4, 3)
        };

        var target = CreateTarget("D/F# thumb-over", fingerAssignments, thumbOverPositions, 5.5f);
        var solver = new InverseKinematicsSolver(config);

        TestContext.WriteLine("Solving IK for D/F# thumb-over voicing...");
        var solution = solver.Solve(hand, target);
        LogSolutionMetrics(solution);

        Assert.That(solution.FitnessDetails.Reachability, Is.GreaterThan(78.0));
        Assert.That(solution.IsAcceptable(75.0));

        var solvedFk = ForwardKinematics.ComputeFingertipPositions(solution.BestPose);
        AssertFingerPlacements(solvedFk, fingerAssignments, target);

        var thumbPosition = solvedFk.GetFingertip(FingerType.Thumb).Position;
        TestContext.WriteLine($"Thumb position: {thumbPosition}");
        Assert.That(thumbPosition.Z, Is.LessThan(0.0f), "Thumb should wrap behind the neck (negative Z).");
    }

    [Test]
    public void Solve_DiagonalSpreadChord_ShouldRemainStable()
    {
        var hand = HandModel.CreateStandardAdult();
        var config = new IkSolverConfig
        {
            PopulationSize = 170,
            Generations = 230,
            RandomSeed = 135,
            UseLocalSearch = true,
            UseGpuAcceleration = true
        };

        var diagonalPositions = ImmutableArray.Create(
            (String: 0, Fret: 3),
            (String: 1, Fret: 2),
            (String: 2, Fret: 0),
            (String: 3, Fret: 0),
            (String: 4, Fret: 3),
            (String: 5, Fret: 3));

        var fingerAssignments = new Dictionary<FingerType, (int String, int Fret)>
        {
            [FingerType.Thumb] = (0, 3),
            [FingerType.Index] = (1, 2),
            [FingerType.Ring] = (4, 3),
            [FingerType.Little] = (5, 3)
        };

        var target = CreateTarget("Cadd9/G diagonal", fingerAssignments, diagonalPositions, 6.0f);
        var solver = new InverseKinematicsSolver(config);

        TestContext.WriteLine("Solving IK for Cadd9/G diagonal spread...");
        var solution = solver.Solve(hand, target);
        LogSolutionMetrics(solution);

        Assert.That(solution.FitnessDetails.Reachability, Is.GreaterThan(76.0));
        Assert.That(solution.FitnessDetails.Stability, Is.GreaterThan(55.0));

        var solvedFk = ForwardKinematics.ComputeFingertipPositions(solution.BestPose);
        AssertFingerPlacements(solvedFk, fingerAssignments, target);
    }

    private static (HandModel Hand, ChordTarget Target, HandPose Pose) BuildRoundtripTarget()
    {
        var hand = HandModel.CreateStandardAdult();
        var pose = HandPose.CreateRestPose(hand) with { WristAngles = new Vector3(0.12f, -0.05f, 0.18f) };
        var fk = ForwardKinematics.ComputeFingertipPositions(pose);

        var targets = ImmutableDictionary<FingerType, Vector3>.Empty.ToBuilder();
        foreach (var finger in hand.Fingers)
        {
            targets[finger.Type] = fk.GetFingertip(finger.Type).Position;
        }

        var chordTarget = new ChordTarget
        {
            ChordName = "Roundtrip",
            TargetPositions = targets.ToImmutable(),
            Tolerance = 3.0f
        };

        return (hand, chordTarget, pose);
    }

    private static void AssertJointSimilarity(HandPose expected, HandPose actual)
    {
        Assert.That(expected.JointAngles.Length, Is.EqualTo(actual.JointAngles.Length));

        var maxDelta = 0.0f;
        for (var i = 0; i < expected.JointAngles.Length; i++)
        {
            maxDelta = Math.Max(maxDelta, Math.Abs(expected.JointAngles[i] - actual.JointAngles[i]));
        }

        Assert.That(maxDelta, Is.LessThan(0.4f));

        var wristDelta = Vector3.Abs(expected.WristAngles - actual.WristAngles);
        Assert.That(wristDelta.X, Is.LessThan(WristTolerance));
        Assert.That(wristDelta.Y, Is.LessThan(WristTolerance));
        Assert.That(wristDelta.Z, Is.LessThan(WristTolerance));
    }

    private static void LogSolutionMetrics(IkSolution solution)
    {
        TestContext.WriteLine(
            $"Solution: reachability={solution.FitnessDetails.Reachability:F1}, comfort={solution.FitnessDetails.Comfort:F1}, " +
            $"naturalness={solution.FitnessDetails.Naturalness:F1}, efficiency={solution.FitnessDetails.Efficiency:F1}, " +
            $"stability={solution.FitnessDetails.Stability:F1}, fitness={solution.Fitness:F2}");
        TestContext.WriteLine(
            $"Generations={solution.GenerationCount}, avg diversity={solution.GetAverageDiversity():F3}, " +
            $"convergence={solution.GetConvergenceRate():F3}, time={solution.SolveTime.TotalMilliseconds:F1}ms");
    }

    private static void AssertFingerPlacements(
        HandPoseResult solved,
        IReadOnlyDictionary<FingerType, (int String, int Fret)> assignments,
        ChordTarget target,
        IReadOnlyDictionary<FingerType, ImmutableArray<int>>? barreCoverage = null)
    {
        var coverage = barreCoverage ?? target.BarreCoverage;

        LogFingerDetails(solved, target, coverage);

        foreach (var (finger, desired) in target.TargetPositions)
        {
            var actual = solved.GetFingertip(finger).Position;
            var distance = Vector3.Distance(actual, desired);
            Assert.That(distance, Is.LessThanOrEqualTo(target.Tolerance + 2.0f),
                $"Finger {finger} exceeds tolerance with distance {distance:F2}mm");
        }

        foreach (var (finger, fretInfo) in assignments)
        {
            var fingertip = solved.GetFingertip(finger).Position;
            var estimatedFret = EstimateFretNumber(fingertip);
            Assert.That(estimatedFret, Is.EqualTo(fretInfo.Fret),
                $"Finger {finger} should fret {fretInfo.Fret} but mapped to {estimatedFret}");

            var estimatedString = EstimateStringNumber(fingertip, fretInfo.Fret);
            Assert.That(estimatedString, Is.EqualTo(fretInfo.String),
                $"Finger {finger} should be on string {fretInfo.String} but mapped to {estimatedString}");
        }

        var stringToFret = target.FretPositions
            .Where(fp => fp.Fret > 0)
            .GroupBy(fp => fp.String)
            .ToDictionary(g => g.Key, g => g.First().Fret);

        foreach (var (finger, strings) in coverage)
        {
            if (!assignments.TryGetValue(finger, out var primary))
            {
                continue;
            }

            Assert.That(strings, Does.Contain(primary.String),
                $"Finger {finger} barre coverage missing primary string {primary.String}");

            if (strings.Length <= 1)
            {
                continue;
            }

            TestContext.WriteLine($"Finger {finger} barre coverage strings: {string.Join(',', strings)}");

            foreach (var stringNumber in strings)
            {
                if (!stringToFret.TryGetValue(stringNumber, out var fret))
                {
                    continue;
                }

                var expected = CalculateFretTarget(stringNumber, fret);
                var actual = solved.GetFingertip(finger).Position;
                var planarDistance = Math.Sqrt(Math.Pow(actual.X - expected.X, 2) + Math.Pow(actual.Y - expected.Y, 2));
                TestContext.WriteLine(
                    $"   String {stringNumber}: expected=({expected.X:F2},{expected.Y:F2}), actual=({actual.X:F2},{actual.Y:F2}), planar error={planarDistance:F2}mm");

                Assert.That(planarDistance, Is.LessThanOrEqualTo(target.Tolerance + 4.0f),
                    $"Finger {finger} should be aligned with string {stringNumber} for barre coverage.");
            }
        }
    }

    private static ChordTarget CreateTarget(
        string chordName,
        IReadOnlyDictionary<FingerType, (int String, int Fret)> assignments,
        ImmutableArray<(int String, int Fret)> fretPositions,
        float tolerance,
        IReadOnlyDictionary<FingerType, ImmutableArray<int>>? barreCoverage = null)
    {
        var targets = ImmutableDictionary.CreateBuilder<FingerType, Vector3>();
        var directions = ImmutableDictionary.CreateBuilder<FingerType, Vector3>();
        var coverageBuilder = ImmutableDictionary.CreateBuilder<FingerType, ImmutableArray<int>>();

        foreach (var (finger, assignment) in assignments)
        {
            targets[finger] = CalculateFretTarget(assignment.String, assignment.Fret);
            var desiredDirection = finger == FingerType.Thumb ? new Vector3(0f, 0f, 1f) : new Vector3(0f, 0f, -1f);
            directions[finger] = NormalizeOrDefault(desiredDirection,
                finger == FingerType.Thumb ? Vector3.UnitZ : Vector3.UnitZ * -1);

            if (barreCoverage is not null && barreCoverage.TryGetValue(finger, out var strings) && strings.Length > 0)
            {
                coverageBuilder[finger] = strings;
            }
            else
            {
                coverageBuilder[finger] = ImmutableArray.Create(assignment.String);
            }
        }

        return new ChordTarget
        {
            ChordName = chordName,
            FretPositions = fretPositions,
            TargetPositions = targets.ToImmutable(),
            ApproachDirections = directions.ToImmutable(),
            Tolerance = tolerance,
            BarreCoverage = coverageBuilder.ToImmutable()
        };
    }

    private static void LogFingerDetails(
        HandPoseResult solved,
        ChordTarget target,
        IReadOnlyDictionary<FingerType, ImmutableArray<int>> coverage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Finger placement summary:");

        foreach (var (finger, desired) in target.TargetPositions)
        {
            var actual = solved.GetFingertip(finger).Position;
            var error = Vector3.Distance(actual, desired);
            var delta = actual - desired;

            sb.AppendLine(
                $" - {finger}: target=({desired.X:F2},{desired.Y:F2},{desired.Z:F2}) " +
                $"actual=({actual.X:F2},{actual.Y:F2},{actual.Z:F2}) error={error:F2}mm " +
                $"delta=({delta.X:F2},{delta.Y:F2},{delta.Z:F2})");
        }

        if (coverage.Count > 0)
        {
            sb.AppendLine("Barre coverage:");
            foreach (var (finger, strings) in coverage)
            {
                if (strings.Length == 0)
                {
                    continue;
                }

                sb.AppendLine($" - {finger}: strings {string.Join(',', strings)}");
            }
        }

        TestContext.WriteLine(sb.ToString());
    }

    private static Vector3 CalculateFretTarget(int stringNumber, int fretNumber)
    {
        var fretPosition = PhysicalFretboardCalculator.CalculateFretPositionMm(fretNumber);
        var x = (float)Math.Max(0.0, fretPosition - FingerOffsetMm);

        var stringSpacing = PhysicalFretboardCalculator.CalculateStringSpacingMM(
            Math.Max(1, fretNumber));

        var totalWidth = stringSpacing * 5.0;
        var y = (float)(-totalWidth / 2.0 + stringNumber * stringSpacing);

        const float fingertipHeight = 3.0f;
        return new Vector3(x, y, fingertipHeight);
    }

    private static Vector3 NormalizeOrDefault(Vector3 vector, Vector3 fallback)
    {
        var lengthSquared = vector.LengthSquared();
        return lengthSquared < 1e-6f ? Vector3.Normalize(fallback) : Vector3.Normalize(vector);
    }

    private static int EstimateFretNumber(Vector3 fingertipPosition)
    {
        var effectiveX = fingertipPosition.X + (float)FingerOffsetMm;
        var bestFret = 0;
        var minDelta = double.MaxValue;

        for (var fret = 0; fret <= 12; fret++)
        {
            var fretPosition = PhysicalFretboardCalculator.CalculateFretPositionMm(fret);
            var delta = Math.Abs(fretPosition - effectiveX);
            if (delta < minDelta)
            {
                minDelta = delta;
                bestFret = fret;
            }
        }

        return bestFret;
    }

    private static int EstimateStringNumber(Vector3 fingertipPosition, int fret)
    {
        var spacing = PhysicalFretboardCalculator.CalculateStringSpacingMM(
            Math.Max(1, fret));
        var totalWidth = spacing * 5.0;
        var bestString = 0;
        var minDelta = double.MaxValue;

        for (var stringNumber = 0; stringNumber < 6; stringNumber++)
        {
            var stringY = -totalWidth / 2.0 + stringNumber * spacing;
            var delta = Math.Abs(stringY - fingertipPosition.Y);
            if (delta < minDelta)
            {
                minDelta = delta;
                bestString = stringNumber;
            }
        }

        return bestString;
    }
}
