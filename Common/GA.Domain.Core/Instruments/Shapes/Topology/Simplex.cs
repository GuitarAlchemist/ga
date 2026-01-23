namespace GA.Domain.Core.Instruments.Shapes.Topology;

/// <summary>
///     A simplex (generalized triangle)
/// </summary>
[PublicAPI]
public class Simplex : IEquatable<Simplex>
{
    public Simplex(IEnumerable<object> vertices)
    {
        Vertices = [.. vertices.OrderBy(v => v.GetHashCode())];
    }

    public IReadOnlyList<object> Vertices { get; }
    public int Dimension => Vertices.Count - 1;

    public bool Equals(Simplex? other)
    {
        if (other == null || Dimension != other.Dimension)
        {
            return false;
        }

        return Vertices.SequenceEqual(other.Vertices);
    }

    /// <summary>
    ///     Get all faces of this simplex
    /// </summary>
    /// <remarks>
    ///     A k-simplex has k+1 faces of dimension k-1
    /// </remarks>
    public IEnumerable<Simplex> Faces()
    {
        if (Dimension < 1)
        {
            yield break;
        }

        for (var i = 0; i < Vertices.Count; i++)
        {
            var faceVertices = Vertices.Where((_, idx) => idx != i);
            yield return new(faceVertices);
        }
    }

    /// <summary>
    ///     Get all k-faces
    /// </summary>
    public IEnumerable<Simplex> Faces(int k)
    {
        if (k < 0 || k >= Dimension)
        {
            yield break;
        }

        if (k == Dimension - 1)
        {
            foreach (var face in Faces())
            {
                yield return face;
            }

            yield break;
        }

        // Recursively get k-faces
        foreach (var face in Faces())
        {
            foreach (var subface in face.Faces(k))
            {
                yield return subface;
            }
        }
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Simplex);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var vertex in Vertices)
        {
            hash.Add(vertex);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        return $"Simplex[{string.Join(",", Vertices)}]";
    }
}