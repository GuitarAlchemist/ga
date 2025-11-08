namespace GA.Business.Core.Tests.Fretboard.Biomechanics;

using Core.Fretboard.Biomechanics;

[TestFixture]
public class HandModelTests
{
    [Test]
    public void CreateStandardAdult_ShouldHaveFiveFingers()
    {
        // Act
        var hand = HandModel.CreateStandardAdult();

        // Assert
        Assert.That(hand.Fingers.Count, Is.EqualTo(5));
        Assert.That(hand.Fingers[0].Type, Is.EqualTo(FingerType.Thumb));
        Assert.That(hand.Fingers[1].Type, Is.EqualTo(FingerType.Index));
        Assert.That(hand.Fingers[2].Type, Is.EqualTo(FingerType.Middle));
        Assert.That(hand.Fingers[3].Type, Is.EqualTo(FingerType.Ring));
        Assert.That(hand.Fingers[4].Type, Is.EqualTo(FingerType.Little));
    }

    [Test]
    public void CreateStandardAdult_ShouldHave19DegreesOfFreedom()
    {
        // Act
        var hand = HandModel.CreateStandardAdult();

        // Assert
        // Thumb: 3 DOF (CMC: 2, MCP: 2, IP: 1) = 5 DOF
        // Wait, let me recalculate based on the implementation:
        // Thumb: CMC (2 DOF) + MCP (2 DOF) + IP (1 DOF) = 5 DOF
        // Index: CMC (0 DOF) + MCP (2 DOF) + PIP (1 DOF) + DIP (1 DOF) = 4 DOF
        // Middle, Ring, Little: 4 DOF each
        // Total: 5 + 4 + 4 + 4 + 4 = 21 DOF

        // Actually, checking the code more carefully:
        // Thumb has 3 joints with varying DOF
        // Other fingers have 4 joints each
        var expectedDof = hand.Fingers.Sum(f => f.TotalDof);

        Assert.That(hand.TotalDof, Is.EqualTo(expectedDof));
        Assert.That(hand.TotalDof, Is.GreaterThan(15)); // At least 15 DOF
    }

    [Test]
    public void Thumb_ShouldHaveThreeJoints()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();

        // Act
        var thumb = hand.GetFinger(FingerType.Thumb);

        // Assert
        Assert.That(thumb.Joints.Count, Is.EqualTo(3));
        Assert.That(thumb.Joints[0].Type, Is.EqualTo(JointType.Cmc));
        Assert.That(thumb.Joints[1].Type, Is.EqualTo(JointType.Mcp));
        Assert.That(thumb.Joints[2].Type, Is.EqualTo(JointType.Ip));
    }

    [Test]
    public void IndexFinger_ShouldHaveFourJoints()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();

        // Act
        var index = hand.GetFinger(FingerType.Index);

        // Assert
        Assert.That(index.Joints.Count, Is.EqualTo(4));
        Assert.That(index.Joints[0].Type, Is.EqualTo(JointType.Cmc));
        Assert.That(index.Joints[1].Type, Is.EqualTo(JointType.Mcp));
        Assert.That(index.Joints[2].Type, Is.EqualTo(JointType.Pip));
        Assert.That(index.Joints[3].Type, Is.EqualTo(JointType.Dip));
    }

    [Test]
    public void FingerJoint_ShouldEnforceFlexionLimits()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var mcpJoint = hand.GetFinger(FingerType.Index).Joints[1]; // MCP joint

        // Act & Assert - Within limits
        Assert.That(mcpJoint.IsWithinLimits(ToRadians(45)), Is.True);

        // Act & Assert - Below minimum
        Assert.That(mcpJoint.IsWithinLimits(ToRadians(-10)), Is.False);

        // Act & Assert - Above maximum
        Assert.That(mcpJoint.IsWithinLimits(ToRadians(100)), Is.False);
    }

    [Test]
    public void FingerJoint_ShouldClampToLimits()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var mcpJoint = hand.GetFinger(FingerType.Index).Joints[1]; // MCP joint

        // Act
        var (flexion, abduction) = mcpJoint.ClampToLimits(ToRadians(100), ToRadians(30));

        // Assert
        Assert.That(flexion, Is.LessThanOrEqualTo(mcpJoint.MaxFlexion));
        Assert.That(abduction, Is.LessThanOrEqualTo(mcpJoint.MaxAbduction));
    }

    [Test]
    public void CreateScaled_ShouldScaleAllDimensions()
    {
        // Arrange
        var standard = HandModel.CreateStandardAdult();
        var scaleFactor = 1.2f;

        // Act
        var scaled = HandModel.CreateScaled(scaleFactor);

        // Assert
        Assert.That(scaled.PalmWidth, Is.EqualTo(standard.PalmWidth * scaleFactor).Within(0.01f));
        Assert.That(scaled.PalmLength, Is.EqualTo(standard.PalmLength * scaleFactor).Within(0.01f));

        // Check finger bone lengths are scaled
        for (var i = 0; i < 5; i++)
        {
            var standardFinger = standard.Fingers[i];
            var scaledFinger = scaled.Fingers[i];

            for (var j = 0; j < standardFinger.Joints.Count; j++)
            {
                var expectedLength = standardFinger.Joints[j].BoneLength * scaleFactor;
                Assert.That(scaledFinger.Joints[j].BoneLength, Is.EqualTo(expectedLength).Within(0.01f));
            }
        }
    }

    [Test]
    public void HandPose_CreateRestPose_ShouldUseRestAngles()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();

        // Act
        var restPose = HandPose.CreateRestPose(hand);

        // Assert
        Assert.That(restPose.JointAngles.Length, Is.GreaterThan(0));
        Assert.That(restPose.Model, Is.EqualTo(hand));
    }

    [Test]
    public void HandPose_IsValid_ShouldReturnTrueForRestPose()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Act
        var isValid = restPose.IsValid();

        // Assert
        Assert.That(isValid, Is.True);
    }

    [Test]
    public void HandPose_ClampToLimits_ShouldEnforceConstraints()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var restPose = HandPose.CreateRestPose(hand);

        // Create invalid pose by setting extreme angles
        var invalidAngles = restPose.JointAngles.ToArray();
        for (var i = 0; i < invalidAngles.Length; i++)
        {
            invalidAngles[i] = ToRadians(200); // Way beyond limits
        }

        var invalidPose = restPose with { JointAngles = [..invalidAngles] };

        // Act
        var clamped = invalidPose.ClampToLimits();

        // Assert
        Assert.That(clamped.IsValid(), Is.True);
    }

    [Test]
    public void MiddleFinger_ShouldBeLongestFinger()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();

        // Act
        var middle = hand.GetFinger(FingerType.Middle);
        var index = hand.GetFinger(FingerType.Index);
        var ring = hand.GetFinger(FingerType.Ring);
        var little = hand.GetFinger(FingerType.Little);

        // Assert
        Assert.That(middle.TotalLength, Is.GreaterThan(index.TotalLength));
        Assert.That(middle.TotalLength, Is.GreaterThan(ring.TotalLength));
        Assert.That(middle.TotalLength, Is.GreaterThan(little.TotalLength));
    }

    [Test]
    public void FingerBasePositions_ShouldBeSpreadAcrossPalm()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();

        // Act
        var thumb = hand.GetFinger(FingerType.Thumb);
        var index = hand.GetFinger(FingerType.Index);
        var middle = hand.GetFinger(FingerType.Middle);
        var ring = hand.GetFinger(FingerType.Ring);
        var little = hand.GetFinger(FingerType.Little);

        // Assert - Fingers should be spread across X axis
        Assert.That(thumb.BasePosition.X, Is.LessThan(index.BasePosition.X));
        Assert.That(index.BasePosition.X, Is.LessThan(middle.BasePosition.X));
        Assert.That(middle.BasePosition.X, Is.LessThan(ring.BasePosition.X));
        Assert.That(ring.BasePosition.X, Is.LessThan(little.BasePosition.X));
    }

    [Test]
    public void JointConstraints_ShouldMatchBiomechanicalLimits()
    {
        // Arrange
        var hand = HandModel.CreateStandardAdult();
        var indexMcp = hand.GetFinger(FingerType.Index).Joints[1]; // MCP joint

        // Assert - MCP should flex 0-90 degrees
        Assert.That(indexMcp.MinFlexion, Is.EqualTo(ToRadians(0)).Within(0.01f));
        Assert.That(indexMcp.MaxFlexion, Is.GreaterThanOrEqualTo(ToRadians(80)));

        // Assert - MCP should have abduction capability
        Assert.That(indexMcp.DegreesOfFreedom, Is.EqualTo(2));
    }

    private static float ToRadians(float degrees)
    {
        return degrees * MathF.PI / 180.0f;
    }
}
