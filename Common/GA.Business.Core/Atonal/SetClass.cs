namespace GA.Business.Core.Atonal;

using System.Linq;
using System.Numerics;
using Primitives;

/// <summary>
///     Represents a set class in post-tonal music theory.
/// </summary>
/// <remarks>
///     A set class is an equivalence class of pitch class sets related by transposition or inversion.
///     It is characterized by its prime form, which is the most compact representation of the set.
///     Set classes are fundamental to analyzing atonal and twelve-tone music, as they allow for
///     identifying structural relationships between different pitch collections regardless of their
///     specific pitch content (<see href="https://harmoniousapp.net/p/71/Set-Classes" />)
///     This class provides properties for accessing the prime form, cardinality, and interval class vector
///     of the set class, which are essential characteristics for set class analysis.
///     Implement <see cref="IEquatable{SetClass}" />
/// </remarks>
[PublicAPI]
public sealed class SetClass(PitchClassSet pitchClassSet) : IEquatable<SetClass>, IStaticReadonlyCollection<SetClass>
{
    private const int _pitchClassSpaceSize = 12;

    private Complex[]? _fourierCoefficients;
    private double[]? _magnitudeSpectrum;
    private double? _spectralCentroid;

    /// <summary>
    ///     Gets all modal set classes
    /// </summary>
    public static IReadOnlyCollection<SetClass> ModalItems => [..Items.Where(@class => @class.IsModal)];

    /// <summary>
    ///     Gets the <see cref="Cardinality" />
    /// </summary>
    public Cardinality Cardinality => PrimeForm.Cardinality;

    /// <summary>
    ///     Gets the <see cref="IntervalClassVector" />
    /// </summary>
    public IntervalClassVector IntervalClassVector => PrimeForm.IntervalClassVector;

    /// <summary>
    ///     Gets the <see cref="PitchClassSet" /> prime form
    /// </summary>
    public PitchClassSet PrimeForm { get; } = pitchClassSet.PrimeForm ??
                                              throw new ArgumentException("Invalid pitch class set", nameof(pitchClassSet));

    /// <summary>
    ///     Gets the <see cref="ModalFamily" /> of the set class, if it exists
    /// </summary>
    public ModalFamily? ModalFamily =>
        ModalFamily.TryGetValue(IntervalClassVector, out var modalFamily) ? modalFamily : null;

    /// <summary>
    ///     Determines whether this set class represents a modal scale
    /// </summary>
    public bool IsModal => ModalFamily != null;

    #region IStaticReadonlyCollection Members

    /// <summary>
    ///     Gets all set classes
    ///     <br /><see cref="IReadOnlyCollection{PitchClassSet}" />
    /// </summary>
    public static IReadOnlyCollection<SetClass> Items => AllSetClasses.Instance;

    #endregion

    /// <inheritdoc />
    public override string ToString()
    {
        return $"SetClass[{Cardinality}-{IntervalClassVector.Id}]";
    }

    #region Spectral Analysis

    /// <summary>
    ///     Gets the 12-point discrete Fourier transform (DFT) of the prime form's pitch-class vector.
    /// </summary>
    /// <remarks>
    ///     Input: binary pitch-class vector (length 12) for the prime form.
    ///     Output: complex coefficients encoding rotational symmetries and intervallic structure.
    /// </remarks>
    public Complex[] GetFourierCoefficients()
    {
        if (_fourierCoefficients != null)
        {
            return _fourierCoefficients;
        }

        var vector = GetSpectralPrimeForm().ToBinaryVector(_pitchClassSpaceSize);
        var result = new Complex[_pitchClassSpaceSize];

        for (var k = 0; k < _pitchClassSpaceSize; k++)
        {
            var sum = Complex.Zero;

            for (var n = 0; n < _pitchClassSpaceSize; n++)
            {
                var angle = -2.0 * Math.PI * k * n / _pitchClassSpaceSize;
                var exp = Complex.Exp(new Complex(0.0, angle));
                sum += vector[n] * exp;
            }

            result[k] = sum;
        }

        _fourierCoefficients = result;
        return result;
    }

    /// <summary>
    ///     Gets the magnitude spectrum |X_k| of the Fourier transform.
    ///     Invariant under transposition of the pitch-class set.
    /// </summary>
    public double[] GetMagnitudeSpectrum()
    {
        if (_magnitudeSpectrum != null)
        {
            return _magnitudeSpectrum;
        }

        var coeffs = GetFourierCoefficients();
        var magnitudes = new double[coeffs.Length];

        for (var i = 0; i < coeffs.Length; i++)
        {
            magnitudes[i] = coeffs[i].Magnitude;
        }

        _magnitudeSpectrum = magnitudes;
        return magnitudes;
    }

    /// <summary>
    ///     Gets the phase spectrum arg(X_k) of the Fourier transform.
    ///     Phase encodes rotational alignment on the chromatic circle.
    /// </summary>
    public double[] GetPhaseSpectrum()
    {
        var coeffs = GetFourierCoefficients();
        var phases = new double[coeffs.Length];

        for (var i = 0; i < coeffs.Length; i++)
        {
            phases[i] = coeffs[i].Phase;
        }

        return phases;
    }

    /// <summary>
    ///     Computes a simple spectral centroid over the 12 bins,
    ///     giving a rough sense of where energy is concentrated across harmonics.
    /// </summary>
    public double GetSpectralCentroid()
    {
        if (_spectralCentroid.HasValue)
        {
            return _spectralCentroid.Value;
        }

        var magnitudes = GetMagnitudeSpectrum();

        var total = 0.0;
        var weighted = 0.0;

        for (var k = 0; k < magnitudes.Length; k++)
        {
            var mag = magnitudes[k];
            total += mag;
            weighted += k * mag;
        }

        _spectralCentroid = total == 0.0 ? 0.0 : weighted / total;
        return _spectralCentroid.Value;
    }

    /// <summary>
    ///     Computes an L1 spectral distance between this set class and another.
    ///     Lower values = more similar spectral fingerprints.
    /// </summary>
    public double GetSpectralDistance(SetClass other)
    {
        var a = GetMagnitudeSpectrum();
        var b = other.GetMagnitudeSpectrum();

        var len = Math.Min(a.Length, b.Length);
        var sum = 0.0;

        for (var i = 0; i < len; i++)
        {
            sum += Math.Abs(a[i] - b[i]);
        }

        return sum;
    }

    #endregion

    internal PitchClassSet GetSpectralPrimeForm()
    {
        if (PrimeForm.Cardinality.Value > 0)
        {
            return PrimeForm;
        }

        return pitchClassSet;
    }

    #region Innner Classes

    private class AllSetClasses : LazyCollectionBase<SetClass>
    {
        public static readonly AllSetClasses Instance = new();

        private AllSetClasses() : base(Collection, ", ")
        {
        }

        private static IEnumerable<SetClass> Collection =>
            PitchClassSet.Items
                .Select(pcs => pcs.PrimeForm)
                .Where(pf => pf != null)
                .Distinct()
                .Select(pf => new SetClass(pf!))
                .OrderBy(setClass => setClass.Cardinality)
                .ThenBy(setClass => setClass.PrimeForm.Id);
    }

    #endregion

    #region Equality Members

    public bool Equals(SetClass? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        return ReferenceEquals(this, other) || PrimeForm.Equals(other.PrimeForm);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == GetType() && Equals((SetClass)obj);
    }

    public override int GetHashCode()
    {
        return PrimeForm.GetHashCode();
    }

    public static bool operator ==(SetClass? left, SetClass? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SetClass? left, SetClass? right)
    {
        return !Equals(left, right);
    }

    #endregion
}
