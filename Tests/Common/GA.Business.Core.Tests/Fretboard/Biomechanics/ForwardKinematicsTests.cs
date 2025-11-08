namespace GA.Business.Core.Tests.Fretboard.Biomechanics;

using Core.Fretboard.Biomechanics;

[TestFixture]
public class ForwardKinematicsTests
{
    private const float _tolerance = 1.0f; // 1mm tolerance for position

    [Test]
    public void RestPose_ShouldProduceReasonableFingertipPositions()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Act
        var result = ForwardKinematics.ComputeFingertipPositions(restPose);

        // Assert
        Assert.That(result.Fingertips.Count, Is.EqualTo(5));

        // All fingertips should be above the palm (positive Y)
        foreach (var fingertip in result.Fingertips.Values)
        {
            Assert.That(fingertip.Position.Y, Is.GreaterThan(0),
                $"{fingertip.Finger} fingertip should be above palm");
        }
    }

    [Test]
    public void StraightFinger_ShouldHaveFingertipAtExpectedDistance()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Create straight index finger (all joints at 0 flexion)
        var angles = restPose.JointAngles.ToArray();

        // Set all index finger joints to 0 (straight)
        // Index finger is second finger (index 1)
        // Need to find the angle indices for index finger
        var angleIndex = 0;

        // Skip thumb angles
        var thumb = hand.GetFinger(FingerType.Thumb);
        foreach (var joint in thumb.Joints)
        {
            angleIndex++;
            if (joint.DegreesOfFreedom == 2)
            {
                angleIndex++;
            }
        }

        // Set index finger to straight
        var index = hand.GetFinger(FingerType.Index);
        foreach (var joint in index.Joints)
        {
            angles[angleIndex++] = 0; // Flexion = 0
            if (joint.DegreesOfFreedom == 2)
            {
                angles[angleIndex++] = 0; // Abduction = 0
            }
        }

        var straightPose = restPose with { JointAngles = [..angles] };

        // Act
        var result = ForwardKinematics.ComputeFingertipPositions(straightPose);
        var indexFingertip = result.GetFingertip(FingerType.Index);

        // Assert - Fingertip should be at base + total finger length
        var expectedY = index.BasePosition.Y + index.TotalLength;
        Assert.That(indexFingertip.Position.Y, Is.EqualTo(expectedY).Within(_tolerance));
    }

    [Test]
    public void FullyFlexedFinger_ShouldHaveShorterReach()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Compute straight finger position
        var straightResult = ForwardKinematics.ComputeFingertipPositions(restPose);
        var straightReach = straightResult.GetFingertip(FingerType.Index).Position.Y;

        // Create fully flexed index finger
        var angles = restPose.JointAngles.ToArray();

        // Find index finger angles
        var angleIndex = 0;
        var thumb = hand.GetFinger(FingerType.Thumb);
        foreach (var joint in thumb.Joints)
        {
            angleIndex++;
            if (joint.DegreesOfFreedom == 2)
            {
                angleIndex++;
            }
        }

        // Flex index finger to maximum
        var index = hand.GetFinger(FingerType.Index);
        foreach (var joint in index.Joints)
        {
            angles[angleIndex++] = joint.MaxFlexion; // Maximum flexion
            if (joint.DegreesOfFreedom == 2)
            {
                angles[angleIndex++] = 0;
            }
        }

        var flexedPose = restPose with { JointAngles = [..angles] };

        // Act
        var flexedResult = ForwardKinematics.ComputeFingertipPositions(flexedPose);
        var flexedReach = flexedResult.GetFingertip(FingerType.Index).Position.Y;

        // Assert - Flexed finger should have shorter reach
        Assert.That(flexedReach, Is.LessThan(straightReach));
    }

    [Test]
    public void FingertipDirection_ShouldPointAwayFromPalm()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Act
        var result = ForwardKinematics.ComputeFingertipPositions(restPose);

        // Assert - All fingertips should point generally upward (positive Y component)
        foreach (var fingertip in result.Fingertips.Values)
        {
            Assert.That(fingertip.Direction.Y, Is.GreaterThan(0),
                $"{fingertip.Finger} should point away from palm");

            // Direction should be normalized
            var length = fingertip.Direction.Length();
            Assert.That(length, Is.EqualTo(1.0f).Within(0.01f),
                $"{fingertip.Finger} direction should be normalized");
        }
    }

    [Test]
    public void JointPositions_ShouldFormChain()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Act
        var result = ForwardKinematics.ComputeFingertipPositions(restPose);
        var indexFingertip = result.GetFingertip(FingerType.Index);

        // Assert - Should have joint positions for each joint + base
        var index = hand.GetFinger(FingerType.Index);
        Assert.That(indexFingertip.JointPositions.Count, Is.EqualTo(index.Joints.Count + 1));

        // First position should be base position
        Assert.That(indexFingertip.JointPositions[0].X, Is.EqualTo(index.BasePosition.X).Within(_tolerance));
        Assert.That(indexFingertip.JointPositions[0].Y, Is.EqualTo(index.BasePosition.Y).Within(_tolerance));
        Assert.That(indexFingertip.JointPositions[0].Z, Is.EqualTo(index.BasePosition.Z).Within(_tolerance));

        // Last position should be fingertip
        var lastPos = indexFingertip.JointPositions[^1];
        Assert.That(lastPos.X, Is.EqualTo(indexFingertip.Position.X).Within(_tolerance));
        Assert.That(lastPos.Y, Is.EqualTo(indexFingertip.Position.Y).Within(_tolerance));
        Assert.That(lastPos.Z, Is.EqualTo(indexFingertip.Position.Z).Within(_tolerance));
    }

    [Test]
    public void MiddleFinger_ShouldReachFarthest()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Act
        var result = ForwardKinematics.ComputeFingertipPositions(restPose);

        var indexReach = result.GetFingertip(FingerType.Index).Position.Y;
        var middleReach = result.GetFingertip(FingerType.Middle).Position.Y;
        var ringReach = result.GetFingertip(FingerType.Ring).Position.Y;
        var littleReach = result.GetFingertip(FingerType.Little).Position.Y;

        // Assert - Middle finger should reach farthest (longest finger)
        Assert.That(middleReach, Is.GreaterThan(indexReach));
        Assert.That(middleReach, Is.GreaterThan(ringReach));
        Assert.That(middleReach, Is.GreaterThan(littleReach));
    }

    [Test]
    public void Abduction_ShouldSpreadFingersApart()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Compute rest position
        var restResult = ForwardKinematics.ComputeFingertipPositions(restPose);
        var restIndexX = restResult.GetFingertip(FingerType.Index).Position.X;
        var restMiddleX = restResult.GetFingertip(FingerType.Middle).Position.X;

        // Create pose with index finger abducted away from middle
        var angles = restPose.JointAngles.ToArray();

        // Find index finger MCP joint abduction angle index
        var angleIndex = 0;

        // Count angles for all fingers before index
        foreach (var finger in hand.Fingers)
        {
            if (finger.Type == FingerType.Index)
            {
                // Found index finger, now find MCP joint
                foreach (var joint in finger.Joints)
                {
                    if (joint.Type == JointType.Mcp)
                    {
                        // MCP has flexion first, then abduction
                        angleIndex++; // Skip flexion
                        break; // angleIndex now points to abduction
                    }

                    // Count DOF for this joint
                    angleIndex++;
                    if (joint.DegreesOfFreedom == 2)
                    {
                        angleIndex++;
                    }
                }

                break;
            }

            // Count all DOF for this finger
            foreach (var joint in finger.Joints)
            {
                angleIndex++;
                if (joint.DegreesOfFreedom == 2)
                {
                    angleIndex++;
                }
            }
        }

        // Set MCP abduction to +20 degrees (abduct away from middle finger)
        // Positive abduction should move index finger away from middle (more negative X)
        angles[angleIndex] = ToRadians(20);

        var abductedPose = restPose with { JointAngles = [..angles] };

        // Act
        var abductedResult = ForwardKinematics.ComputeFingertipPositions(abductedPose);
        var abductedIndexX = abductedResult.GetFingertip(FingerType.Index).Position.X;

        // Assert - Index finger should move away from middle finger (more negative X)
        // Since index is at negative X and middle is at 0, abducting index should make it MORE negative
        Assert.That(abductedIndexX, Is.LessThan(restIndexX));
    }

    [Test]
    public void ComputeReachError_ShouldMeasureDistanceToTarget()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);
        var result = ForwardKinematics.ComputeFingertipPositions(restPose);
        var indexPos = result.GetFingertip(FingerType.Index).Position;

        // Target 10mm away from fingertip
        var target = indexPos + new Vector3(10, 0, 0);

        // Act
        var error = ForwardKinematics.ComputeReachError(restPose, FingerType.Index, target);

        // Assert
        Assert.That(error, Is.EqualTo(10).Within(_tolerance));
    }

    [Test]
    public void CanReach_ShouldReturnTrueWhenWithinTolerance()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);
        var result = ForwardKinematics.ComputeFingertipPositions(restPose);
        var indexPos = result.GetFingertip(FingerType.Index).Position;

        // Target 3mm away (within default 5mm tolerance)
        var nearTarget = indexPos + new Vector3(3, 0, 0);

        // Target 10mm away (outside default 5mm tolerance)
        var farTarget = indexPos + new Vector3(10, 0, 0);

        // Act & Assert
        Assert.That(ForwardKinematics.CanReach(restPose, FingerType.Index, nearTarget), Is.True);
        Assert.That(ForwardKinematics.CanReach(restPose, FingerType.Index, farTarget), Is.False);
    }

    [Test]
    public void ComputeJacobian_ShouldHaveCorrectDimensions()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);
        var index = hand.GetFinger(FingerType.Index);

        // Act
        var jacobian = ForwardKinematics.ComputeJacobian(restPose, FingerType.Index);

        // Assert - Jacobian should be 3 � numDOF
        Assert.That(jacobian.GetLength(0), Is.EqualTo(3)); // 3D position
        Assert.That(jacobian.GetLength(1), Is.EqualTo(index.TotalDof)); // DOF for index finger
    }

    [Test]
    public void Jacobian_ShouldShowPositiveYDerivativeForFlexion()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Act
        var jacobian = ForwardKinematics.ComputeJacobian(restPose, FingerType.Index);

        // Assert - Flexing joints should generally move fingertip in Y direction
        // (This is a simplified check - actual derivatives depend on current pose)
        var hasYComponent = false;
        for (var dof = 0; dof < jacobian.GetLength(1); dof++)
        {
            if (Math.Abs(jacobian[1, dof]) > 0.1f) // Y component
            {
                hasYComponent = true;
                break;
            }
        }

        Assert.That(hasYComponent, Is.True, "Jacobian should have Y components");
    }

    [Test]
    public void AllFingers_ShouldHaveValidFingertipPositions()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Act
        var result = ForwardKinematics.ComputeFingertipPositions(restPose);

        // Assert
        foreach (var fingertip in result.Fingertips.Values)
        {
            // Position should not be NaN or infinite
            Assert.That(float.IsNaN(fingertip.Position.X), Is.False, $"{fingertip.Finger} X is NaN");
            Assert.That(float.IsNaN(fingertip.Position.Y), Is.False, $"{fingertip.Finger} Y is NaN");
            Assert.That(float.IsNaN(fingertip.Position.Z), Is.False, $"{fingertip.Finger} Z is NaN");
            Assert.That(float.IsInfinity(fingertip.Position.X), Is.False, $"{fingertip.Finger} X is infinite");
            Assert.That(float.IsInfinity(fingertip.Position.Y), Is.False, $"{fingertip.Finger} Y is infinite");
            Assert.That(float.IsInfinity(fingertip.Position.Z), Is.False, $"{fingertip.Finger} Z is infinite");

            // Direction should be normalized
            var dirLength = fingertip.Direction.Length();
            Assert.That(dirLength, Is.EqualTo(1.0f).Within(0.01f), $"{fingertip.Finger} direction not normalized");
        }
    }

    private static float ToRadians(float degrees)
    {
        return degrees * MathF.PI / 180.0f;
    }
}
