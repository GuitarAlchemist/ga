namespace GA.Business.Core.Fretboard.Shapes.Topology;

/// <summary>
///     A simplicial complex for topological data analysis
/// </summary>
/// <remarks>
///     A simplicial complex is a collection of simplices (points, edges, triangles, tetrahedra, etc.)
///     that are "glued together" in a consistent way.
///     Simplices:
///     - 0-simplex: vertex (point)
///     - 1-simplex: edge (line segment)
///     - 2-simplex: triangle (filled)
///     - 3-simplex: tetrahedron
///     - k-simplex: k-dimensional generalization
///     Properties:
///     - Every face of a simplex is also in the complex
///     - Intersection of two simplices is either empty or a common face
///     Musical applications:
///     - Vertices: Chord shapes
///     - Edges: Transitions between shapes
///     - Triangles: Three shapes that form a cycle
///     - Higher simplices: Larger harmonic structures
///     References:
///     - Edelsbrunner, H., & Harer, J. (2010). Computational Topology
///     - Carlsson, G. (2009). "Topology and data"
///     - Zomorodian, A., & Carlsson, G. (2005). "Computing persistent homology"
/// </remarks>
[PublicAPI]
public class SimplicialComplex
{
    private readonly HashSet<Simplex> _simplices = [];
    private readonly Dictionary<int, List<Simplex>> _simplicesByDimension = new();

    /// <summary>
    ///     Get all vertices (0-simplices)
    /// </summary>
    public IReadOnlyList<Simplex> Vertices => GetSimplices(0);

    /// <summary>
    ///     Get all edges (1-simplices)
    /// </summary>
    public IReadOnlyList<Simplex> Edges => GetSimplices(1);

    /// <summary>
    ///     Get all triangles (2-simplices)
    /// </summary>
    public IReadOnlyList<Simplex> Triangles => GetSimplices(2);

    /// <summary>
    ///     Maximum dimension of simplices in the complex
    /// </summary>
    public int Dimension => _simplicesByDimension.Keys.DefaultIfEmpty(0).Max();

    /// <summary>
    ///     Total number of simplices
    /// </summary>
    public int Count => _simplices.Count;

    /// <summary>
    ///     Add a simplex to the complex
    /// </summary>
    /// <remarks>
    ///     Automatically adds all faces of the simplex
    /// </remarks>
    public void AddSimplex(Simplex simplex)
    {
        if (_simplices.Contains(simplex))
        {
            return;
        }

        _simplices.Add(simplex);

        if (!_simplicesByDimension.ContainsKey(simplex.Dimension))
        {
            _simplicesByDimension[simplex.Dimension] = [];
        }

        _simplicesByDimension[simplex.Dimension].Add(simplex);

        // Add all faces
        foreach (var face in simplex.Faces())
        {
            AddSimplex(face);
        }
    }

    /// <summary>
    ///     Get all simplices of a given dimension
    /// </summary>
    public IReadOnlyList<Simplex> GetSimplices(int dimension)
    {
        return _simplicesByDimension.GetValueOrDefault(dimension, []);
    }

    /// <summary>
    ///     Build Vietoris-Rips complex from a distance matrix
    /// </summary>
    /// <param name="points">Point identifiers</param>
    /// <param name="distances">Distance function</param>
    /// <param name="epsilon">Radius threshold</param>
    /// <param name="maxDimension">Maximum simplex dimension to construct</param>
    /// <remarks>
    ///     Vietoris-Rips complex at scale e:
    ///     - Include all points as vertices
    ///     - Include edge {i,j} if d(i,j) = e
    ///     - Include k-simplex if all pairwise distances = e
    ///     Used for persistent homology
    /// </remarks>
    public static SimplicialComplex VietorisRips<T>(
        IReadOnlyList<T> points,
        Func<T, T, double> distances,
        double epsilon,
        int maxDimension = 2) where T : notnull
    {
        var complex = new SimplicialComplex();

        // Add vertices
        var vertices = points.Select(p => new Simplex([p])).ToList();
        foreach (var vertex in vertices)
        {
            complex.AddSimplex(vertex);
        }

        // Add edges
        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                if (distances(points[i], points[j]) <= epsilon)
                {
                    complex.AddSimplex(new Simplex([points[i], points[j]]));
                }
            }
        }

        // Add higher-dimensional simplices
        if (maxDimension >= 2)
        {
            AddHigherSimplices(complex, points, distances, epsilon, maxDimension);
        }

        return complex;
    }

    private static void AddHigherSimplices<T>(
        SimplicialComplex complex,
        IReadOnlyList<T> points,
        Func<T, T, double> distances,
        double epsilon,
        int maxDimension) where T : notnull
    {
        // For each edge, try to form triangles
        var edges = complex.Edges;

        for (var dim = 2; dim <= maxDimension; dim++)
        {
            var currentSimplices = complex.GetSimplices(dim - 1);

            foreach (var simplex in currentSimplices)
            {
                // Try to extend simplex by adding one more vertex
                foreach (var point in points)
                {
                    if (simplex.Vertices.Contains(point))
                    {
                        continue;
                    }

                    // Check if point is within epsilon of all vertices in simplex
                    var allClose = simplex.Vertices.All(v =>
                        distances((T)v, point) <= epsilon);

                    if (allClose)
                    {
                        var newVertices = simplex.Vertices.Append(point).ToArray();
                        complex.AddSimplex(new Simplex(newVertices));
                    }
                }
            }
        }
    }

    public override string ToString()
    {
        return $"SimplicialComplex[dim={Dimension}, " +
               $"vertices={Vertices.Count}, edges={Edges.Count}, triangles={Triangles.Count}]";
    }
}

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
            yield return new Simplex(faceVertices);
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
