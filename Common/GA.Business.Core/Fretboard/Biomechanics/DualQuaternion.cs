namespace GA.Business.Core.Fretboard.Biomechanics;

using System;
using System.Numerics;

/// <summary>
///     Dual quaternion for representing rigid transformations (rotation + translation).
///     A dual quaternion is q^ = qr + e qt where e² = 0.
///     This provides a unified, efficient representation for 3D transformations without gimbal lock.
/// </summary>
/// <remarks>
///     Dual quaternions are superior to matrices for skeletal animation and IK:
///     - 8 parameters vs 16 for matrices
///     - No gimbal lock (unlike Euler angles)
///     - Smooth interpolation (SLERP)
///     - Natural representation of screw motion (joint rotation)
///     - Efficient composition (multiplication)
///     References:
///     - "Dual Quaternions for Rigid Transformation Blending" - Kavan et al.
///     - "Geometric Algebra for Computer Science" - Dorst, Fontijne, Mann
///     - "A Beginners Guide to Dual-Quaternions" - Kenwright (2012)
///     http://wscg.zcu.cz/wscg2012/short/a29-full.pdf
///     - libdq C library - Edgar Simo-Serra
///     https://codeberg.org/bobbens/libdq
///     - dqnet C# port - Stephane Pareilleux
///     https://github.com/spareilleux/dqnet
/// </remarks>
public readonly struct DualQuaternion : IEquatable<DualQuaternion>
{
    /// <summary>
    ///     Real part (rotation quaternion)
    /// </summary>
    public Quaternion Real { get; }

    /// <summary>
    ///     Dual part (translation quaternion)
    /// </summary>
    public Quaternion Dual { get; }

    /// <summary>
    ///     Create a dual quaternion from real and dual parts
    /// </summary>
    public DualQuaternion(Quaternion real, Quaternion dual)
    {
        Real = real;
        Dual = dual;
    }

    /// <summary>
    ///     Create identity dual quaternion (no rotation, no translation)
    /// </summary>
    public static DualQuaternion Identity => new(Quaternion.Identity, Quaternion.Zero);

    /// <summary>
    ///     Create dual quaternion from rotation only
    /// </summary>
    public static DualQuaternion FromRotation(Quaternion rotation)
    {
        return new(rotation, Quaternion.Zero);
    }

    /// <summary>
    ///     Create dual quaternion from translation only
    /// </summary>
    public static DualQuaternion FromTranslation(Vector3 translation)
    {
        // qt = 0.5 * t * qr where t is pure quaternion (0, tx, ty, tz)
        var t = new Quaternion(translation.X, translation.Y, translation.Z, 0);
        var dual = MultiplyScalar(t, 0.5f); // qr is identity for pure translation
        return new(Quaternion.Identity, dual);
    }

    /// <summary>
    ///     Create dual quaternion from rotation and translation
    /// </summary>
    public static DualQuaternion FromRotationTranslation(Quaternion rotation, Vector3 translation)
    {
        // Normalize rotation
        var qr = Quaternion.Normalize(rotation);

        // qt = 0.5 * t * qr where t is pure quaternion
        var t = new Quaternion(translation.X, translation.Y, translation.Z, 0);
        var qt = MultiplyScalar(Quaternion.Multiply(t, qr), 0.5f);

        return new(qr, qt);
    }

    /// <summary>
    ///     Create dual quaternion from axis-angle rotation and translation
    /// </summary>
    public static DualQuaternion FromAxisAngleTranslation(Vector3 axis, float angleRadians, Vector3 translation)
    {
        var rotation = Quaternion.CreateFromAxisAngle(axis, angleRadians);
        return FromRotationTranslation(rotation, translation);
    }

    /// <summary>
    ///     Extract rotation quaternion
    /// </summary>
    public Quaternion GetRotation()
    {
        return Quaternion.Normalize(Real);
    }

    /// <summary>
    ///     Extract translation vector
    /// </summary>
    public Vector3 GetTranslation()
    {
        // t = 2 * qt * qr*
        var qrConj = Quaternion.Conjugate(Real);
        var t = MultiplyScalar(Quaternion.Multiply(Dual, qrConj), 2.0f);
        return new(t.X, t.Y, t.Z);
    }

    /// <summary>
    ///     Multiply two dual quaternions (compose transformations)
    /// </summary>
    public static DualQuaternion operator *(DualQuaternion a, DualQuaternion b)
    {
        // q^1 * q^2 = (qr1 * qr2) + e(qr1 * qt2 + qt1 * qr2)
        var real = Quaternion.Multiply(a.Real, b.Real);
        var dual = Quaternion.Add(
            Quaternion.Multiply(a.Real, b.Dual),
            Quaternion.Multiply(a.Dual, b.Real)
        );
        return new(real, dual);
    }

    /// <summary>
    ///     Conjugate of dual quaternion
    /// </summary>
    public DualQuaternion Conjugate()
    {
        return new(
            Quaternion.Conjugate(Real),
            Quaternion.Conjugate(Dual)
        );
    }

    /// <summary>
    ///     Dual conjugate (conjugate dual part only)
    /// </summary>
    public DualQuaternion DualConjugate()
    {
        return new(Real, Quaternion.Negate(Dual));
    }

    /// <summary>
    ///     Combined conjugate (both quaternion and dual)
    /// </summary>
    public DualQuaternion CombinedConjugate()
    {
        return new(
            Quaternion.Conjugate(Real),
            Quaternion.Negate(Quaternion.Conjugate(Dual))
        );
    }

    /// <summary>
    ///     Normalize dual quaternion
    /// </summary>
    public DualQuaternion Normalize()
    {
        var norm = Real.Length();
        if (norm < 1e-6f)
        {
            return Identity;
        }

        var invNorm = 1.0f / norm;
        var real = new Quaternion(
            Real.X * invNorm,
            Real.Y * invNorm,
            Real.Z * invNorm,
            Real.W * invNorm
        );

        // Dual part normalization: qt' = (qt - qr * (qr · qt) / |qr|²) / |qr|
        var dot = Real.X * Dual.X + Real.Y * Dual.Y + Real.Z * Dual.Z + Real.W * Dual.W;
        var dual = new Quaternion(
            (Dual.X - Real.X * dot * invNorm * invNorm) * invNorm,
            (Dual.Y - Real.Y * dot * invNorm * invNorm) * invNorm,
            (Dual.Z - Real.Z * dot * invNorm * invNorm) * invNorm,
            (Dual.W - Real.W * dot * invNorm * invNorm) * invNorm
        );

        return new(real, dual);
    }

    /// <summary>
    ///     Helper method to multiply quaternion by scalar
    /// </summary>
    private static Quaternion MultiplyScalar(Quaternion q, float scalar)
    {
        return new(q.X * scalar, q.Y * scalar, q.Z * scalar, q.W * scalar);
    }

    /// <summary>
    ///     Transform a point using this dual quaternion
    /// </summary>
    public Vector3 TransformPoint(Vector3 point)
    {
        // First apply rotation
        var rotated = Vector3.Transform(point, Real);

        // Then apply translation
        var translation = GetTranslation();
        return rotated + translation;
    }

    /// <summary>
    ///     Transform a vector (rotation only, no translation)
    /// </summary>
    public Vector3 TransformVector(Vector3 vector)
    {
        return Vector3.Transform(vector, Real);
    }

    /// <summary>
    ///     Spherical linear interpolation (SLERP) between two dual quaternions
    /// </summary>
    /// <param name="a">Start dual quaternion</param>
    /// <param name="b">End dual quaternion</param>
    /// <param name="t">Interpolation parameter [0, 1]</param>
    /// <returns>Interpolated dual quaternion</returns>
    public static DualQuaternion Slerp(DualQuaternion a, DualQuaternion b, float t)
    {
        // Ensure shortest path
        var dot = a.Real.X * b.Real.X + a.Real.Y * b.Real.Y +
                  a.Real.Z * b.Real.Z + a.Real.W * b.Real.W;

        var bAdjusted = b;
        if (dot < 0)
        {
            bAdjusted = new(
                Quaternion.Negate(b.Real),
                Quaternion.Negate(b.Dual)
            );
            dot = -dot;
        }

        // SLERP real part
        var realInterp = Quaternion.Slerp(a.Real, bAdjusted.Real, t);

        // SLERP dual part
        var dualInterp = QuaternionSlerp(a.Dual, bAdjusted.Dual, t);

        return new(realInterp, dualInterp);
    }

    /// <summary>
    ///     Helper for quaternion SLERP (since System.Numerics.Quaternion.Slerp handles real part)
    /// </summary>
    private static Quaternion QuaternionSlerp(Quaternion a, Quaternion b, float t)
    {
        // Linear interpolation for dual part (good enough for small angles)
        return new(
            a.X + t * (b.X - a.X),
            a.Y + t * (b.Y - a.Y),
            a.Z + t * (b.Z - a.Z),
            a.W + t * (b.W - a.W)
        );
    }

    /// <summary>
    ///     Screw linear interpolation (ScLERP) - more accurate for dual quaternions
    /// </summary>
    public static DualQuaternion ScLerp(DualQuaternion a, DualQuaternion b, float t)
    {
        // ScLERP: q^(t) = q^1 * (q^1* * q^2)^t
        var diff = a.CombinedConjugate() * b;
        var powered = Pow(diff, t);
        return a * powered;
    }

    /// <summary>
    ///     Power of dual quaternion (for ScLERP)
    /// </summary>
    public static DualQuaternion Pow(DualQuaternion q, float exponent)
    {
        // Simplified power for small angles
        // For full implementation, convert to screw parameters
        var angle = 2.0f * MathF.Acos(q.Real.W);
        var newAngle = angle * exponent;

        if (MathF.Abs(angle) < 1e-6f)
        {
            return Identity;
        }

        var axis = new Vector3(q.Real.X, q.Real.Y, q.Real.Z) / MathF.Sin(angle * 0.5f);
        var translation = q.GetTranslation() * exponent;

        return FromAxisAngleTranslation(axis, newAngle, translation);
    }

    /// <summary>
    ///     Convert to 4x4 transformation matrix
    /// </summary>
    public Matrix4x4 ToMatrix()
    {
        var rotation = Matrix4x4.CreateFromQuaternion(Real);
        var translation = GetTranslation();
        rotation.M41 = translation.X;
        rotation.M42 = translation.Y;
        rotation.M43 = translation.Z;
        return rotation;
    }

    /// <summary>
    ///     Create from 4x4 transformation matrix
    /// </summary>
    public static DualQuaternion FromMatrix(Matrix4x4 matrix)
    {
        var rotation = Quaternion.CreateFromRotationMatrix(matrix);
        var translation = new Vector3(matrix.M41, matrix.M42, matrix.M43);
        return FromRotationTranslation(rotation, translation);
    }

    public bool Equals(DualQuaternion other)
    {
        return Real.Equals(other.Real) && Dual.Equals(other.Dual);
    }

    public override bool Equals(object? obj)
    {
        return obj is DualQuaternion other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Real, Dual);
    }

    public static bool operator ==(DualQuaternion left, DualQuaternion right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(DualQuaternion left, DualQuaternion right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"DQ[Real: {Real}, Dual: {Dual}]";
    }
}
