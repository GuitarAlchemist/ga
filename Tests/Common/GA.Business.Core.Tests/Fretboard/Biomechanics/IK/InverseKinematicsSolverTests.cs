namespace GA.Business.Core.Tests.Fretboard.Biomechanics.IK;

using Core.Fretboard.Biomechanics;

[TestFixture]
public class InverseKinematicsSolverTests
{
    private const float _tolerance = 0.1f;

    [Test]
    public void Solve_RestPoseTarget_ShouldConverge()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Create target at rest pose fingertip positions
        var result = ForwardKinematics.ComputeFingertipPositions(restPose);
        var targets = ImmutableDictionary<FingerType, Vector3>.Empty.ToBuilder();

        foreach (var finger in hand.Fingers)
        {
            var fingertip = result.GetFingertip(finger.Type);
            targets[finger.Type] = fingertip.Position;
        }

        var target = new ChordTarget
        {
            ChordName = "Rest Pose",
            TargetPositions = targets.ToImmutable(),
            Tolerance = 5.0f
        };

        var config = new IkSolverConfig
        {
            PopulationSize = 50,
            Generations = 100,
            RandomSeed = 42 // For reproducibility
        };

        var solver = new InverseKinematicsSolver(config);

        // Act
        var solution = solver.Solve(hand, target);

        // Assert
        Assert.That(solution, Is.Not.Null);
        Assert.That(solution.BestPose, Is.Not.Null);
        Assert.That(solution.FitnessDetails.Reachability, Is.GreaterThan(80.0)); // Should reach well
        // Note: GenerationCount may be less than configured max due to early termination (reachability >= 99.9)
        Assert.That(solution.GenerationCount, Is.LessThanOrEqualTo(100));
        Assert.That(solution.GenerationBestFitness.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Solve_ShouldImproveOverGenerations()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        var result = ForwardKinematics.ComputeFingertipPositions(restPose);
        var targets = ImmutableDictionary<FingerType, Vector3>.Empty.ToBuilder();

        foreach (var finger in hand.Fingers)
        {
            var fingertip = result.GetFingertip(finger.Type);
            targets[finger.Type] = fingertip.Position;
        }

        var target = new ChordTarget
        {
            TargetPositions = targets.ToImmutable(),
            Tolerance = 5.0f
        };

        var config = new IkSolverConfig
        {
            PopulationSize = 50,
            Generations = 50,
            RandomSeed = 42
        };

        var solver = new InverseKinematicsSolver(config);

        // Act
        var solution = solver.Solve(hand, target);

        // Assert
        var firstFitness = solution.GenerationBestFitness[0];
        var lastFitness = solution.GenerationBestFitness[^1];

        // Fitness should improve (or at least not decrease)
        Assert.That(lastFitness, Is.GreaterThanOrEqualTo(firstFitness));
    }

    [Test]
    public void Solve_WithElitism_ShouldPreserveBestIndividuals()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        var result = ForwardKinematics.ComputeFingertipPositions(restPose);
        var targets = ImmutableDictionary<FingerType, Vector3>.Empty.ToBuilder();

        foreach (var finger in hand.Fingers)
        {
            var fingertip = result.GetFingertip(finger.Type);
            targets[finger.Type] = fingertip.Position;
        }

        var target = new ChordTarget
        {
            TargetPositions = targets.ToImmutable(),
            Tolerance = 5.0f
        };

        var config = new IkSolverConfig
        {
            PopulationSize = 50,
            Generations = 50,
            EliteCount = 5,
            RandomSeed = 42
        };

        var solver = new InverseKinematicsSolver(config);

        // Act
        var solution = solver.Solve(hand, target);

        // Assert
        // With elitism, best fitness should never decrease
        for (var i = 1; i < solution.GenerationBestFitness.Length; i++)
        {
            Assert.That(solution.GenerationBestFitness[i],
                Is.GreaterThanOrEqualTo(solution.GenerationBestFitness[i - 1] - _tolerance));
        }
    }

    [Test]
    public void Solve_ShouldRespectJointLimits()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        var result = ForwardKinematics.ComputeFingertipPositions(restPose);
        var targets = ImmutableDictionary<FingerType, Vector3>.Empty.ToBuilder();

        foreach (var finger in hand.Fingers)
        {
            var fingertip = result.GetFingertip(finger.Type);
            targets[finger.Type] = fingertip.Position;
        }

        var target = new ChordTarget
        {
            TargetPositions = targets.ToImmutable(),
            Tolerance = 5.0f
        };

        var config = new IkSolverConfig
        {
            PopulationSize = 30,
            Generations = 30,
            RandomSeed = 42
        };

        var solver = new InverseKinematicsSolver(config);

        // Act
        var solution = solver.Solve(hand, target);

        // Assert - All joint angles should be within limits
        var pose = solution.BestPose;
        var angleIndex = 0;

        foreach (var finger in hand.Fingers)
        {
            foreach (var joint in finger.Joints)
            {
                var flexion = pose.JointAngles[angleIndex++];
                Assert.That(flexion, Is.GreaterThanOrEqualTo(joint.MinFlexion - _tolerance));
                Assert.That(flexion, Is.LessThanOrEqualTo(joint.MaxFlexion + _tolerance));

                if (joint.DegreesOfFreedom == 2)
                {
                    var abduction = pose.JointAngles[angleIndex++];
                    Assert.That(abduction, Is.GreaterThanOrEqualTo(joint.MinAbduction - _tolerance));
                    Assert.That(abduction, Is.LessThanOrEqualTo(joint.MaxAbduction + _tolerance));
                }
            }
        }
    }

    [Test]
    public void Solve_ShouldPopulateSolutionMetadata()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        var result = ForwardKinematics.ComputeFingertipPositions(restPose);
        var targets = ImmutableDictionary<FingerType, Vector3>.Empty.ToBuilder();

        foreach (var finger in hand.Fingers)
        {
            var fingertip = result.GetFingertip(finger.Type);
            targets[finger.Type] = fingertip.Position;
        }

        var target = new ChordTarget
        {
            ChordName = "Test Chord",
            TargetPositions = targets.ToImmutable(),
            Tolerance = 5.0f
        };

        var config = new IkSolverConfig
        {
            PopulationSize = 30,
            Generations = 20,
            RandomSeed = 42
        };

        var solver = new InverseKinematicsSolver(config);

        // Act
        var solution = solver.Solve(hand, target);

        // Assert
        Assert.That(solution.Target, Is.EqualTo(target));
        // Note: GenerationCount may be less than configured max due to early termination (reachability >= 99.9)
        Assert.That(solution.GenerationCount, Is.LessThanOrEqualTo(20));
        Assert.That(solution.SolveTime, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(solution.GenerationBestFitness.Length, Is.GreaterThan(0));
        Assert.That(solution.GenerationBestFitness.Length, Is.EqualTo(solution.GenerationCount));
    }

    [Test]
    public void IsAcceptable_HighReachability_ShouldReturnTrue()
    {
        // Arrange
        var solution = new IkSolution
        {
            BestPose = HandPose.CreateRestPose(HandModel.CreateStandardAdult()),
            Fitness = 1000.0,
            FitnessDetails = new FitnessBreakdown
            {
                Reachability = 95.0,
                Comfort = 80.0,
                Naturalness = 70.0,
                Efficiency = 85.0,
                Stability = 75.0,
                TotalFitness = 1000.0
            },
            GenerationCount = 100,
            GenerationBestFitness = [],
            SolveTime = TimeSpan.FromSeconds(1),
            Target = new ChordTarget()
        };

        // Act
        var isAcceptable = solution.IsAcceptable(80.0);

        // Assert
        Assert.That(isAcceptable, Is.True);
    }

    [Test]
    public void IsAcceptable_LowReachability_ShouldReturnFalse()
    {
        // Arrange
        var solution = new IkSolution
        {
            BestPose = HandPose.CreateRestPose(HandModel.CreateStandardAdult()),
            Fitness = 500.0,
            FitnessDetails = new FitnessBreakdown
            {
                Reachability = 50.0,
                Comfort = 80.0,
                Naturalness = 70.0,
                Efficiency = 85.0,
                Stability = 75.0,
                TotalFitness = 500.0
            },
            GenerationCount = 100,
            GenerationBestFitness = [],
            SolveTime = TimeSpan.FromSeconds(1),
            Target = new ChordTarget()
        };

        // Act
        var isAcceptable = solution.IsAcceptable(80.0);

        // Assert
        Assert.That(isAcceptable, Is.False);
    }

    [Test]
    public void GetConvergenceRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var solution = new IkSolution
        {
            BestPose = HandPose.CreateRestPose(HandModel.CreateStandardAdult()),
            Fitness = 1000.0,
            FitnessDetails = new FitnessBreakdown { TotalFitness = 1000.0 },
            GenerationCount = 100,
            GenerationBestFitness = [100.0, 200.0, 300.0, 400.0, 500.0],
            SolveTime = TimeSpan.FromSeconds(1),
            Target = new ChordTarget()
        };

        // Act
        var convergenceRate = solution.GetConvergenceRate();

        // Assert
        // (500 - 100) / 100 = 4.0
        Assert.That(convergenceRate, Is.EqualTo(4.0).Within(_tolerance));
    }

    [Test]
    public void Solve_WithLocalSearch_ShouldNotDecreaseReachability()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);
        var modifiedAngles = restPose.JointAngles.ToArray();
        modifiedAngles[0] += 0.25f; // Flex index finger slightly

        var modifiedPose = new HandPose
        {
            JointAngles = [..modifiedAngles],
            Model = hand
        };

        var targetResult = ForwardKinematics.ComputeFingertipPositions(modifiedPose);
        var indexTarget = targetResult.GetFingertip(FingerType.Index).Position;

        var target = new ChordTarget
        {
            TargetPositions = ImmutableDictionary<FingerType, Vector3>.Empty.Add(FingerType.Index, indexTarget),
            Tolerance = 5.0f
        };

        var baseConfig = new IkSolverConfig
        {
            PopulationSize = 20,
            Generations = 1,
            RandomSeed = 123,
            UseLocalSearch = false
        };

        var solverWithoutLocal = new InverseKinematicsSolver(baseConfig);
        var solutionWithout = solverWithoutLocal.Solve(hand, target);

        var solverWithLocal = new InverseKinematicsSolver(baseConfig with { UseLocalSearch = true });
        var solutionWith = solverWithLocal.Solve(hand, target);

        // Assert
        Assert.That(solutionWith.FitnessDetails.Reachability,
            Is.GreaterThanOrEqualTo(solutionWithout.FitnessDetails.Reachability - 1e-3));

        var wristLimits = hand.WristLimits;
        var wrist = solutionWith.BestPose.WristAngles;
        Assert.That(wrist.X, Is.InRange(wristLimits.MinFlexion - _tolerance, wristLimits.MaxFlexion + _tolerance));
        Assert.That(wrist.Y, Is.InRange(wristLimits.MinDeviation - _tolerance, wristLimits.MaxDeviation + _tolerance));
        Assert.That(wrist.Z, Is.InRange(wristLimits.MinRotation - _tolerance, wristLimits.MaxRotation + _tolerance));

        var refinedResult = ForwardKinematics.ComputeFingertipPositions(solutionWith.BestPose);
        foreach (var constraint in hand.FingerSpreadConstraints)
        {
            var primary = refinedResult.GetFingertip(constraint.Primary).Position;
            var secondary = refinedResult.GetFingertip(constraint.Secondary).Position;
            var separation = Math.Sqrt(Math.Pow(primary.X - secondary.X, 2) + Math.Pow(primary.Y - secondary.Y, 2));

            Assert.That(separation, Is.LessThanOrEqualTo(constraint.MaxSeparationMm + 1.0));
            if (constraint.MinSeparationMm > 0)
            {
                Assert.That(separation, Is.GreaterThanOrEqualTo(constraint.MinSeparationMm - 1.0));
            }
        }
    }
}
