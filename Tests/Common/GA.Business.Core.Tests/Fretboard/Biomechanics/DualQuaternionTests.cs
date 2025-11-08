namespace GA.Business.Core.Tests.Fretboard.Biomechanics;

using Core.Fretboard.Biomechanics;

[TestFixture]
public class DualQuaternionTests
{
    private const float _tolerance = 0.001f;

    [Test]
    public void Identity_ShouldHaveNoTransformation()
    {
        // Arrange
        var identity = DualQuaternion.Identity;
        var point = new Vector3(10, 20, 30);

        // Act
        var transformed = identity.TransformPoint(point);

        // Assert
        Assert.That(transformed.X, Is.EqualTo(point.X).Within(_tolerance));
        Assert.That(transformed.Y, Is.EqualTo(point.Y).Within(_tolerance));
        Assert.That(transformed.Z, Is.EqualTo(point.Z).Within(_tolerance));
    }

    [Test]
    public void FromTranslation_ShouldTranslatePoint()
    {
        // Arrange
        var translation = new Vector3(5, 10, 15);
        var dq = DualQuaternion.FromTranslation(translation);
        var point = new Vector3(1, 2, 3);

        // Act
        var transformed = dq.TransformPoint(point);

        // Assert
        Assert.That(transformed.X, Is.EqualTo(6).Within(_tolerance));
        Assert.That(transformed.Y, Is.EqualTo(12).Within(_tolerance));
        Assert.That(transformed.Z, Is.EqualTo(18).Within(_tolerance));
    }

    [Test]
    public void FromRotation_ShouldRotatePoint()
    {
        // Arrange - 90 degree rotation around Z axis
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
        var dq = DualQuaternion.FromRotation(rotation);
        var point = new Vector3(1, 0, 0);

        // Act
        var transformed = dq.TransformPoint(point);

        // Assert - Should rotate to (0, 1, 0)
        Assert.That(transformed.X, Is.EqualTo(0).Within(_tolerance));
        Assert.That(transformed.Y, Is.EqualTo(1).Within(_tolerance));
        Assert.That(transformed.Z, Is.EqualTo(0).Within(_tolerance));
    }

    [Test]
    public void FromRotationTranslation_ShouldRotateThenTranslate()
    {
        // Arrange - 90 degree rotation around Z axis, then translate by (10, 0, 0)
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
        var translation = new Vector3(10, 0, 0);
        var dq = DualQuaternion.FromRotationTranslation(rotation, translation);
        var point = new Vector3(1, 0, 0);

        // Act
        var transformed = dq.TransformPoint(point);

        // Assert - Should rotate to (0, 1, 0) then translate to (10, 1, 0)
        Assert.That(transformed.X, Is.EqualTo(10).Within(_tolerance));
        Assert.That(transformed.Y, Is.EqualTo(1).Within(_tolerance));
        Assert.That(transformed.Z, Is.EqualTo(0).Within(_tolerance));
    }

    [Test]
    public void Multiplication_ShouldComposeTransformations()
    {
        // Arrange
        var dq1 = DualQuaternion.FromTranslation(new Vector3(5, 0, 0));
        var dq2 = DualQuaternion.FromTranslation(new Vector3(0, 10, 0));
        var point = new Vector3(0, 0, 0);

        // Act
        var composed = dq1 * dq2;
        var transformed = composed.TransformPoint(point);

        // Assert - Should translate by (5, 10, 0)
        Assert.That(transformed.X, Is.EqualTo(5).Within(_tolerance));
        Assert.That(transformed.Y, Is.EqualTo(10).Within(_tolerance));
        Assert.That(transformed.Z, Is.EqualTo(0).Within(_tolerance));
    }

    [Test]
    public void GetTranslation_ShouldExtractTranslationVector()
    {
        // Arrange
        var translation = new Vector3(7, 8, 9);
        var dq = DualQuaternion.FromTranslation(translation);

        // Act
        var extracted = dq.GetTranslation();

        // Assert
        Assert.That(extracted.X, Is.EqualTo(7).Within(_tolerance));
        Assert.That(extracted.Y, Is.EqualTo(8).Within(_tolerance));
        Assert.That(extracted.Z, Is.EqualTo(9).Within(_tolerance));
    }

    [Test]
    public void GetRotation_ShouldExtractRotationQuaternion()
    {
        // Arrange
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4);
        var dq = DualQuaternion.FromRotation(rotation);

        // Act
        var extracted = dq.GetRotation();

        // Assert
        Assert.That(extracted.X, Is.EqualTo(rotation.X).Within(_tolerance));
        Assert.That(extracted.Y, Is.EqualTo(rotation.Y).Within(_tolerance));
        Assert.That(extracted.Z, Is.EqualTo(rotation.Z).Within(_tolerance));
        Assert.That(extracted.W, Is.EqualTo(rotation.W).Within(_tolerance));
    }

    [Test]
    public void Normalize_ShouldMaintainTransformation()
    {
        // Arrange
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 3);
        var translation = new Vector3(5, 10, 15);
        var dq = DualQuaternion.FromRotationTranslation(rotation, translation);
        var point = new Vector3(1, 2, 3);

        // Act
        var normalized = dq.Normalize();
        var original = dq.TransformPoint(point);
        var normalizedResult = normalized.TransformPoint(point);

        // Assert
        Assert.That(normalizedResult.X, Is.EqualTo(original.X).Within(_tolerance));
        Assert.That(normalizedResult.Y, Is.EqualTo(original.Y).Within(_tolerance));
        Assert.That(normalizedResult.Z, Is.EqualTo(original.Z).Within(_tolerance));
    }

    [Test]
    public void Slerp_ShouldInterpolateSmoothly()
    {
        // Arrange
        var dq1 = DualQuaternion.FromTranslation(new Vector3(0, 0, 0));
        var dq2 = DualQuaternion.FromTranslation(new Vector3(10, 0, 0));
        var point = new Vector3(0, 0, 0);

        // Act
        var halfway = DualQuaternion.Slerp(dq1, dq2, 0.5f);
        var transformed = halfway.TransformPoint(point);

        // Assert - Should be halfway between (0,0,0) and (10,0,0)
        Assert.That(transformed.X, Is.EqualTo(5).Within(_tolerance));
        Assert.That(transformed.Y, Is.EqualTo(0).Within(_tolerance));
        Assert.That(transformed.Z, Is.EqualTo(0).Within(_tolerance));
    }

    [Test]
    public void ToMatrix_ShouldProduceEquivalentTransformation()
    {
        // Arrange
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 4);
        var translation = new Vector3(5, 10, 15);
        var dq = DualQuaternion.FromRotationTranslation(rotation, translation);
        var point = new Vector3(1, 2, 3);

        // Act
        var matrix = dq.ToMatrix();
        var dqResult = dq.TransformPoint(point);
        var matrixResult = Vector3.Transform(point, matrix);

        // Assert
        Assert.That(matrixResult.X, Is.EqualTo(dqResult.X).Within(_tolerance));
        Assert.That(matrixResult.Y, Is.EqualTo(dqResult.Y).Within(_tolerance));
        Assert.That(matrixResult.Z, Is.EqualTo(dqResult.Z).Within(_tolerance));
    }

    [Test]
    public void FromMatrix_ShouldRoundTrip()
    {
        // Arrange
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 6);
        var translation = new Vector3(3, 7, 11);
        var original = DualQuaternion.FromRotationTranslation(rotation, translation);

        // Act
        var matrix = original.ToMatrix();
        var reconstructed = DualQuaternion.FromMatrix(matrix);
        var point = new Vector3(1, 2, 3);
        var originalResult = original.TransformPoint(point);
        var reconstructedResult = reconstructed.TransformPoint(point);

        // Assert
        Assert.That(reconstructedResult.X, Is.EqualTo(originalResult.X).Within(_tolerance));
        Assert.That(reconstructedResult.Y, Is.EqualTo(originalResult.Y).Within(_tolerance));
        Assert.That(reconstructedResult.Z, Is.EqualTo(originalResult.Z).Within(_tolerance));
    }

    [Test]
    public void TransformVector_ShouldRotateWithoutTranslation()
    {
        // Arrange - 90 degree rotation around Z axis with translation
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
        var translation = new Vector3(100, 200, 300); // Should be ignored
        var dq = DualQuaternion.FromRotationTranslation(rotation, translation);
        var vector = new Vector3(1, 0, 0);

        // Act
        var transformed = dq.TransformVector(vector);

        // Assert - Should only rotate, not translate
        Assert.That(transformed.X, Is.EqualTo(0).Within(_tolerance));
        Assert.That(transformed.Y, Is.EqualTo(1).Within(_tolerance));
        Assert.That(transformed.Z, Is.EqualTo(0).Within(_tolerance));
    }

    [Test]
    public void ChainedTransformations_ShouldMatchSequentialApplication()
    {
        // Arrange - Simulate a finger joint chain
        var joint1 = DualQuaternion.FromRotationTranslation(
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 6),
            new Vector3(0, 10, 0)
        );
        var joint2 = DualQuaternion.FromRotationTranslation(
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 4),
            new Vector3(0, 15, 0)
        );
        var joint3 = DualQuaternion.FromRotationTranslation(
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 3),
            new Vector3(0, 10, 0)
        );

        var point = new Vector3(0, 0, 0);

        // Act - Chained multiplication (A * B * C applies C, then B, then A)
        var chained = joint1 * joint2 * joint3;
        var chainedResult = chained.TransformPoint(point);

        // Sequential application in reverse order (right-to-left)
        var sequential = joint3.TransformPoint(point);
        sequential = joint2.TransformPoint(sequential);
        sequential = joint1.TransformPoint(sequential);

        // Assert - Should produce same result
        Assert.That(chainedResult.X, Is.EqualTo(sequential.X).Within(_tolerance));
        Assert.That(chainedResult.Y, Is.EqualTo(sequential.Y).Within(_tolerance));
        Assert.That(chainedResult.Z, Is.EqualTo(sequential.Z).Within(_tolerance));
    }
}
