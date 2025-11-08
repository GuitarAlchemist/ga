namespace GA.Business.Core.Tests.Fretboard.Biomechanics.IK;

using Core.Fretboard.Biomechanics;

[TestFixture]
public class FitnessEvaluatorTests
{
    private const float _tolerance = 0.1f;

    [Test]
    public void EvaluateFitness_RestPose_ShouldHaveHighComfort()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);
        var chromosome = HandPoseChromosome.FromHandPose(restPose);

        var evaluator = new FitnessEvaluator(new FitnessWeights());

        // Create a target that matches rest pose fingertip positions
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

        // Act
        var fitness = evaluator.EvaluateFitness(chromosome, target);

        // Assert
        Assert.That(chromosome.FitnessDetails, Is.Not.Null);
        Assert.That(chromosome.FitnessDetails!.Comfort, Is.GreaterThan(95.0)); // Rest pose should be very comfortable
        Assert.That(chromosome.FitnessDetails.Reachability, Is.GreaterThan(99.0)); // Perfect reach
        Assert.That(fitness, Is.GreaterThan(0.0));
    }

    [Test]
    public void EvaluateFitness_PerfectReach_ShouldHaveHighReachability()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var pose = HandPose.CreateRestPose(hand);
        var chromosome = HandPoseChromosome.FromHandPose(pose);

        var evaluator = new FitnessEvaluator(new FitnessWeights());

        // Create target at exact fingertip positions
        var result = ForwardKinematics.ComputeFingertipPositions(pose);
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

        // Act
        var fitness = evaluator.EvaluateFitness(chromosome, target);

        // Assert
        Assert.That(chromosome.FitnessDetails!.Reachability, Is.GreaterThan(99.0));

        // All reach errors should be near zero
        foreach (var error in chromosome.FitnessDetails.ReachErrors.Values)
        {
            Assert.That(error, Is.LessThan(0.1f));
        }
    }

    [Test]
    public void EvaluateFitness_ImpossibleTarget_ShouldHaveLowReachability()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var pose = HandPose.CreateRestPose(hand);
        var chromosome = HandPoseChromosome.FromHandPose(pose);

        var evaluator = new FitnessEvaluator(new FitnessWeights());

        // Create impossible target (very far away)
        var targets = ImmutableDictionary<FingerType, Vector3>.Empty
            .Add(FingerType.Index, new Vector3(1000, 1000, 1000))
            .Add(FingerType.Middle, new Vector3(1000, 1000, 1000));

        var target = new ChordTarget
        {
            TargetPositions = targets,
            Tolerance = 5.0f
        };

        // Act
        var fitness = evaluator.EvaluateFitness(chromosome, target);

        // Assert
        Assert.That(chromosome.FitnessDetails!.Reachability, Is.LessThan(10.0));

        // Reach errors should be very large
        foreach (var error in chromosome.FitnessDetails.ReachErrors.Values)
        {
            Assert.That(error, Is.GreaterThan(100.0f));
        }
    }

    [Test]
    public void EvaluateFitness_ExtremeFlexion_ShouldHaveLowComfort()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var pose = HandPose.CreateRestPose(hand);

        // Set all joints to maximum flexion (uncomfortable)
        var angles = pose.JointAngles.ToArray();
        var angleIndex = 0;

        foreach (var finger in hand.Fingers)
        {
            foreach (var joint in finger.Joints)
            {
                angles[angleIndex++] = joint.MaxFlexion; // Maximum flexion

                if (joint.DegreesOfFreedom == 2)
                {
                    angles[angleIndex++] = joint.MaxAbduction;
                }
            }
        }

        var extremePose = pose with { JointAngles = [..angles] };
        var chromosome = HandPoseChromosome.FromHandPose(extremePose);

        var evaluator = new FitnessEvaluator(new FitnessWeights());

        // Create target at fingertip positions (so reachability is good)
        var result = ForwardKinematics.ComputeFingertipPositions(extremePose);
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

        // Act
        var fitness = evaluator.EvaluateFitness(chromosome, target);

        // Assert
        Assert.That(chromosome.FitnessDetails!.Comfort, Is.LessThan(50.0)); // Should be uncomfortable
        Assert.That(chromosome.FitnessDetails.Reachability, Is.GreaterThan(99.0)); // But reachable
    }

    [Test]
    public void EvaluateFitness_ShouldCalculateAllComponents()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var pose = HandPose.CreateRestPose(hand);
        var chromosome = HandPoseChromosome.FromHandPose(pose);

        var evaluator = new FitnessEvaluator(new FitnessWeights());

        var result = ForwardKinematics.ComputeFingertipPositions(pose);
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

        // Act
        var fitness = evaluator.EvaluateFitness(chromosome, target);

        // Assert
        Assert.That(chromosome.FitnessDetails, Is.Not.Null);
        Assert.That(chromosome.FitnessDetails!.Reachability, Is.InRange(0.0, 100.0));
        Assert.That(chromosome.FitnessDetails.Comfort, Is.InRange(0.0, 100.0));
        Assert.That(chromosome.FitnessDetails.Naturalness, Is.InRange(0.0, 100.0));
        Assert.That(chromosome.FitnessDetails.Efficiency, Is.InRange(0.0, 100.0));
        Assert.That(chromosome.FitnessDetails.Stability, Is.InRange(0.0, 100.0));
        Assert.That(chromosome.FitnessDetails.TotalFitness, Is.EqualTo(fitness));
    }

    [Test]
    public void EvaluateFitness_CustomWeights_ShouldAffectTotalFitness()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var pose = HandPose.CreateRestPose(hand);
        var chromosome1 = HandPoseChromosome.FromHandPose(pose);
        var chromosome2 = HandPoseChromosome.FromHandPose(pose);

        // Evaluator with default weights
        var evaluator1 = new FitnessEvaluator(new FitnessWeights());

        // Evaluator with custom weights (prioritize comfort over reachability)
        var evaluator2 = new FitnessEvaluator(new FitnessWeights
        {
            Reachability = 10.0,
            Comfort = 200.0,
            Naturalness = 30.0,
            Efficiency = 20.0,
            Stability = 10.0
        });

        var result = ForwardKinematics.ComputeFingertipPositions(pose);
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

        // Act
        var fitness1 = evaluator1.EvaluateFitness(chromosome1, target);
        var fitness2 = evaluator2.EvaluateFitness(chromosome2, target);

        // Assert
        // Component scores should be the same
        Assert.That(chromosome1.FitnessDetails!.Reachability,
            Is.EqualTo(chromosome2.FitnessDetails!.Reachability).Within(_tolerance));
        Assert.That(chromosome1.FitnessDetails.Comfort,
            Is.EqualTo(chromosome2.FitnessDetails.Comfort).Within(_tolerance));

        // But total fitness should be different due to different weights
        Assert.That(fitness1, Is.Not.EqualTo(fitness2));
    }

    [Test]
    public void EvaluateFitness_ShouldPopulateReachErrors()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var pose = HandPose.CreateRestPose(hand);
        var chromosome = HandPoseChromosome.FromHandPose(pose);

        var evaluator = new FitnessEvaluator(new FitnessWeights());

        var targets = ImmutableDictionary<FingerType, Vector3>.Empty
            .Add(FingerType.Index, new Vector3(0, 100, 0))
            .Add(FingerType.Middle, new Vector3(0, 100, 0));

        var target = new ChordTarget
        {
            TargetPositions = targets,
            Tolerance = 5.0f
        };

        // Act
        evaluator.EvaluateFitness(chromosome, target);

        // Assert
        Assert.That(chromosome.FitnessDetails!.ReachErrors.Count, Is.EqualTo(2));
        Assert.That(chromosome.FitnessDetails.ReachErrors.ContainsKey(FingerType.Index), Is.True);
        Assert.That(chromosome.FitnessDetails.ReachErrors.ContainsKey(FingerType.Middle), Is.True);
    }
}
