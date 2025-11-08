namespace GA.Business.Core.Tests.Fretboard.Biomechanics.IK;

using Core.Fretboard.Biomechanics;

[TestFixture]
public class HandPoseChromosomeTests
{
    private const float _tolerance = 0.001f;

    [Test]
    public void CreateRandom_ShouldGenerateValidAngles()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var random = new Random(42);

        // Act
        var chromosome = HandPoseChromosome.CreateRandom(hand, random);

        // Assert
        Assert.That(chromosome.JointAngles.Length, Is.GreaterThan(0));
        Assert.That(chromosome.Model, Is.EqualTo(hand));

        // All angles should be within valid ranges
        var pose = chromosome.ToHandPose();
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

        var wrist = pose.WristAngles;
        var limits = hand.WristLimits;
        Assert.That(wrist.X, Is.InRange(limits.MinFlexion - _tolerance, limits.MaxFlexion + _tolerance));
        Assert.That(wrist.Y, Is.InRange(limits.MinDeviation - _tolerance, limits.MaxDeviation + _tolerance));
        Assert.That(wrist.Z, Is.InRange(limits.MinRotation - _tolerance, limits.MaxRotation + _tolerance));
    }

    [Test]
    public void ToHandPose_ShouldConvertCorrectly()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var random = new Random(42);
        var chromosome = HandPoseChromosome.CreateRandom(hand, random);

        // Act
        var pose = chromosome.ToHandPose();

        // Assert
        Assert.That(pose.JointAngles, Is.EqualTo(chromosome.JointAngles));
        Assert.That(pose.Model, Is.EqualTo(chromosome.Model));
        Assert.That(pose.WristAngles, Is.EqualTo(chromosome.WristAngles));
    }

    [Test]
    public void FromHandPose_ShouldConvertCorrectly()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var pose = HandPose.CreateRestPose(hand) with { WristAngles = new Vector3(0.1f, -0.05f, 0.2f) };

        // Act
        var chromosome = HandPoseChromosome.FromHandPose(pose);

        // Assert
        Assert.That(chromosome.JointAngles, Is.EqualTo(pose.JointAngles));
        Assert.That(chromosome.Model, Is.EqualTo(pose.Model));
        Assert.That(chromosome.WristAngles, Is.EqualTo(pose.WristAngles));
    }

    [Test]
    public void Clamp_ShouldEnforceJointLimits()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var pose = HandPose.CreateRestPose(hand);

        // Create chromosome with out-of-bounds angles
        var angles = pose.JointAngles.ToArray();
        angles[0] = 10.0f; // Way beyond any joint limit
        angles[1] = -10.0f; // Way below any joint limit

        var chromosome = new HandPoseChromosome([..angles], hand)
        {
            WristAngles = new Vector3(hand.WristLimits.MaxFlexion + 0.5f, hand.WristLimits.MaxDeviation + 0.5f,
                hand.WristLimits.MaxRotation + 0.5f)
        };

        // Act
        var clamped = chromosome.Clamp();

        // Assert
        var clampedPose = clamped.ToHandPose();
        var angleIndex = 0;

        foreach (var finger in hand.Fingers)
        {
            foreach (var joint in finger.Joints)
            {
                var flexion = clampedPose.JointAngles[angleIndex++];
                Assert.That(flexion, Is.GreaterThanOrEqualTo(joint.MinFlexion - _tolerance));
                Assert.That(flexion, Is.LessThanOrEqualTo(joint.MaxFlexion + _tolerance));

                if (joint.DegreesOfFreedom == 2)
                {
                    var abduction = clampedPose.JointAngles[angleIndex++];
                    Assert.That(abduction, Is.GreaterThanOrEqualTo(joint.MinAbduction - _tolerance));
                    Assert.That(abduction, Is.LessThanOrEqualTo(joint.MaxAbduction + _tolerance));
                }
            }
        }

        var clampedWrist = clamped.ToHandPose().WristAngles;
        var limits = hand.WristLimits;
        Assert.That(clampedWrist.X, Is.InRange(limits.MinFlexion - _tolerance, limits.MaxFlexion + _tolerance));
        Assert.That(clampedWrist.Y, Is.InRange(limits.MinDeviation - _tolerance, limits.MaxDeviation + _tolerance));
        Assert.That(clampedWrist.Z, Is.InRange(limits.MinRotation - _tolerance, limits.MaxRotation + _tolerance));
    }

    [Test]
    public void ChordTarget_FromFretPositions_ShouldCreateTarget()
    {
        // Arrange
        var fretPositions = ImmutableArray.Create(
            (String: 0, Fret: 0), // Open E
            (String: 1, Fret: 2), // B
            (String: 2, Fret: 2), // G#
            (String: 3, Fret: 2), // E
            (String: 4, Fret: 0), // A
            (String: 5, Fret: 0) // E
        );

        // Act
        var target = ChordTarget.FromFretPositions("E Major", fretPositions);

        // Assert
        Assert.Multiple(() =>
        {
            var coverageSummary = string.Join("; ",
                target.BarreCoverage.Select(kvp => $"{kvp.Key}:{string.Join(',', kvp.Value)}"));
            TestContext.WriteLine($"Barre coverage summary: {coverageSummary}");

            Assert.That(target.ChordName, Is.EqualTo("E Major"));
            Assert.That(target.FretPositions, Is.EqualTo(fretPositions));
            Assert.That(target.Tolerance, Is.EqualTo(5.0f));
            Assert.That(target.TargetPositions, Is.Not.Empty);
            Assert.That(target.TargetPositions.ContainsKey(FingerType.Index), Is.True);
            Assert.That(target.TargetPositions.ContainsKey(FingerType.Middle), Is.True);
            Assert.That(target.TargetPositions.ContainsKey(FingerType.Ring), Is.True);
            Assert.That(target.TargetPositions.ContainsKey(FingerType.Thumb), Is.True);
            Assert.That(target.ApproachDirections, Is.Not.Empty);
            Assert.That(target.ApproachDirections.ContainsKey(FingerType.Index), Is.True);

            var indexTarget = target.TargetPositions[FingerType.Index];
            Assert.That(indexTarget.Z, Is.GreaterThan(0.0f));

            foreach (var coverage in target.BarreCoverage)
            {
                Assert.That(coverage.Value, Is.Not.Empty,
                    $"Finger {coverage.Key} should have at least one covered string.");
            }
        });
    }

    [Test]
    public void IKSolverConfig_DefaultValues_ShouldBeReasonable()
    {
        // Arrange & Act
        var config = new IkSolverConfig();

        // Assert
        Assert.That(config.PopulationSize, Is.EqualTo(100));
        Assert.That(config.Generations, Is.EqualTo(200));
        Assert.That(config.MutationRate, Is.EqualTo(0.15));
        Assert.That(config.CrossoverRate, Is.EqualTo(0.8));
        Assert.That(config.TournamentSize, Is.EqualTo(5));
        Assert.That(config.EliteCount, Is.EqualTo(5));
    }

    [Test]
    public void FitnessWeights_DefaultValues_ShouldPrioritizeReachability()
    {
        // Arrange & Act
        var weights = new FitnessWeights();

        // Assert
        Assert.That(weights.Reachability, Is.EqualTo(100.0));
        Assert.That(weights.Comfort, Is.EqualTo(50.0));
        Assert.That(weights.Naturalness, Is.EqualTo(30.0));
        Assert.That(weights.Efficiency, Is.EqualTo(20.0));
        Assert.That(weights.Stability, Is.EqualTo(10.0));

        // Reachability should be highest
        Assert.That(weights.Reachability, Is.GreaterThan(weights.Comfort));
        Assert.That(weights.Reachability, Is.GreaterThan(weights.Naturalness));
    }

    [Test]
    public void FitnessBreakdown_ShouldStoreAllComponents()
    {
        // Arrange & Act
        var breakdown = new FitnessBreakdown
        {
            Reachability = 95.0,
            Comfort = 80.0,
            Naturalness = 70.0,
            Efficiency = 85.0,
            Stability = 75.0,
            TotalFitness = 1000.0,
            ReachErrors = ImmutableDictionary<FingerType, float>.Empty
                .Add(FingerType.Index, 2.5f)
                .Add(FingerType.Middle, 3.0f),
            FingerComfort = ImmutableDictionary<FingerType, double>.Empty
                .Add(FingerType.Index, 85.0)
                .Add(FingerType.Middle, 90.0)
        };

        // Assert
        Assert.That(breakdown.Reachability, Is.EqualTo(95.0));
        Assert.That(breakdown.Comfort, Is.EqualTo(80.0));
        Assert.That(breakdown.Naturalness, Is.EqualTo(70.0));
        Assert.That(breakdown.Efficiency, Is.EqualTo(85.0));
        Assert.That(breakdown.Stability, Is.EqualTo(75.0));
        Assert.That(breakdown.TotalFitness, Is.EqualTo(1000.0));
        Assert.That(breakdown.ReachErrors.Count, Is.EqualTo(2));
        Assert.That(breakdown.FingerComfort.Count, Is.EqualTo(2));
    }
}
