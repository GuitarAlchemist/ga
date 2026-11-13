namespace GeoTiles;

using System.Buffers.Binary;
using System.Text.Json;
using Openize.Drako;

/// <summary>One triangle list of a tile, with its positions already in the tileset's world frame, in doubles.</summary>
public sealed record TilePrimitive(Vec3d[] Positions, float[]? Uvs, int[] Indices, byte[]? Image, string? ImageMime);

/// <summary>A decoded tile. <see cref="Copyright"/> is the glTF's asset.copyright: Google names there the data
/// providers that must be shown on screen while the tile is.</summary>
public sealed record TileMesh(IReadOnlyList<TilePrimitive> Primitives, string? Copyright);

/// <summary>Reads a binary glTF 2.0 (GLB) tile: the node hierarchy, triangle primitives, plain or compressed with
/// KHR_draco_mesh_compression, and the base colour image. In memory only.</summary>
public static class GlbReader
{
    private const uint Magic = 0x46546C67; // "glTF"
    private const uint ChunkJson = 0x4E4F534A;
    private const uint ChunkBin = 0x004E4942;

    public static TileMesh Read(ReadOnlySpan<byte> glb, Mat4d tileTransform)
    {
        if (glb.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(glb) != Magic)
        {
            throw new FormatException("not a GLB: no glTF magic");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(glb[4..]);
        if (version != 2)
        {
            throw new FormatException($"GLB version {version}, expected 2");
        }

        byte[]? json = null;
        byte[] bin = [];
        var at = 12;
        while (at + 8 <= glb.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb[at..]);
            var type = BinaryPrimitives.ReadUInt32LittleEndian(glb[(at + 4)..]);
            if (at + 8 + length > glb.Length)
            {
                throw new FormatException("GLB: a chunk runs past the end of the file");
            }

            var data = glb.Slice(at + 8, length).ToArray();
            if (type == ChunkJson)
            {
                json = data;
            }
            else if (type == ChunkBin)
            {
                bin = data;
            }

            at += 8 + length;
        }

        if (json is null)
        {
            throw new FormatException("GLB: no JSON chunk");
        }

        using var doc = JsonDocument.Parse(json);
        var gltf = new Gltf(doc.RootElement, bin);
        var primitives = new List<TilePrimitive>();
        var world = tileTransform * Mat4d.YUpToZUp;
        foreach (var node in gltf.SceneRoots())
        {
            gltf.Walk(node, world, primitives, 0);
        }

        string? copyright = null;
        if (doc.RootElement.TryGetProperty("asset", out var asset) && asset.TryGetProperty("copyright", out var c))
        {
            copyright = c.GetString();
        }

        return new TileMesh(primitives, copyright);
    }

    private sealed class Gltf(JsonElement root, byte[] bin)
    {
        public IEnumerable<int> SceneRoots()
        {
            if (!root.TryGetProperty("scenes", out var scenes) || scenes.GetArrayLength() == 0)
            {
                yield break;
            }

            var scene = root.TryGetProperty("scene", out var s) ? s.GetInt32() : 0;
            if (scenes[scene].TryGetProperty("nodes", out var nodes))
            {
                foreach (var n in nodes.EnumerateArray())
                {
                    yield return n.GetInt32();
                }
            }
        }

        public void Walk(int nodeIndex, Mat4d parent, List<TilePrimitive> into, int depth)
        {
            if (depth > 64)
            {
                throw new FormatException("glTF: a node hierarchy deeper than 64, or a cycle");
            }

            var node = root.GetProperty("nodes")[nodeIndex];
            var local = node.TryGetProperty("matrix", out var m)
                ? new Mat4d(Numbers(m))
                : Mat4d.FromTrs(Optional(node, "translation"), Optional(node, "rotation"), Optional(node, "scale"));
            var world = parent * local;
            if (node.TryGetProperty("mesh", out var mesh))
            {
                foreach (var p in root.GetProperty("meshes")[mesh.GetInt32()].GetProperty("primitives").EnumerateArray())
                {
                    if (p.TryGetProperty("mode", out var mode) && mode.GetInt32() != 4)
                    {
                        continue; // points and lines: nothing this demo draws
                    }

                    into.Add(Primitive(p, world));
                }
            }

            if (node.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    Walk(child.GetInt32(), world, into, depth + 1);
                }
            }
        }

        private TilePrimitive Primitive(JsonElement p, Mat4d world)
        {
            float[] positions;
            float[]? uvs;
            int[] indices;
            if (p.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("KHR_draco_mesh_compression", out var draco))
            {
                (positions, uvs, indices) = Draco(draco);
            }
            else
            {
                var attributes = p.GetProperty("attributes");
                positions = Floats(attributes.GetProperty("POSITION").GetInt32(), 3);
                uvs = attributes.TryGetProperty("TEXCOORD_0", out var uv) ? Floats(uv.GetInt32(), 2) : null;
                indices = p.TryGetProperty("indices", out var idx)
                    ? Indices(idx.GetInt32())
                    : Enumerable.Range(0, positions.Length / 3).ToArray();
            }

            var count = positions.Length / 3;
            foreach (var i in indices)
            {
                if ((uint)i >= (uint)count)
                {
                    throw new FormatException($"glTF: index {i} of {count} vertices");
                }
            }

            var worldPositions = new Vec3d[count];
            for (var i = 0; i < count; i++)
            {
                worldPositions[i] = world.TransformPoint(
                    new Vec3d(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]));
            }

            var (image, mime) = BaseColourImage(p);
            return new TilePrimitive(worldPositions, uvs, indices, image, mime);
        }

        private (float[] Positions, float[]? Uvs, int[] Indices) Draco(JsonElement draco)
        {
            var view = View(draco.GetProperty("bufferView").GetInt32());
            if (Openize.Drako.Draco.Decode(view.ToArray()) is not DracoMesh mesh)
            {
                throw new FormatException("Draco: a point cloud where a mesh was expected");
            }

            var ids = draco.GetProperty("attributes");
            var positions = DracoAttribute(mesh, ids.GetProperty("POSITION").GetInt32(), 3);
            var uvs = ids.TryGetProperty("TEXCOORD_0", out var uv) ? DracoAttribute(mesh, uv.GetInt32(), 2) : null;
            var indices = new int[mesh.NumFaces * 3];
            var face = new int[3];
            for (var f = 0; f < mesh.NumFaces; f++)
            {
                mesh.ReadFace(f, face);
                face.CopyTo(indices, f * 3);
            }

            return (positions, uvs, indices);
        }

        /// <summary>The values of the attribute with this Draco unique id, one per point, as floats.</summary>
        private static float[] DracoAttribute(DracoMesh mesh, int uniqueId, int components)
        {
            PointAttribute? attribute = null;
            for (var i = 0; i < mesh.NumAttributes; i++)
            {
                if (mesh.Attribute(i).UniqueId == uniqueId)
                {
                    attribute = mesh.Attribute(i);
                    break;
                }
            }

            if (attribute is null)
            {
                throw new FormatException($"Draco: no attribute with unique id {uniqueId}");
            }

            if (attribute.ComponentsCount < components)
            {
                throw new FormatException(
                    $"Draco: attribute {uniqueId} has {attribute.ComponentsCount} components, expected {components}");
            }

            var result = new float[mesh.NumPoints * components];
            var value = new float[attribute.ComponentsCount];
            for (var point = 0; point < mesh.NumPoints; point++)
            {
                attribute.GetValue(attribute.MappedIndex(point), value);
                Array.Copy(value, 0, result, point * components, components);
            }

            return result;
        }

        private (byte[]? Image, string? Mime) BaseColourImage(JsonElement p)
        {
            if (!p.TryGetProperty("material", out var material))
            {
                return (null, null);
            }

            var mat = root.GetProperty("materials")[material.GetInt32()];
            if (!mat.TryGetProperty("pbrMetallicRoughness", out var pbr) ||
                !pbr.TryGetProperty("baseColorTexture", out var tex))
            {
                return (null, null);
            }

            var texture = root.GetProperty("textures")[tex.GetProperty("index").GetInt32()];
            if (!texture.TryGetProperty("source", out var source))
            {
                return (null, null); // a KTX2 or WebP only texture: an extension this demo does not decode
            }

            var image = root.GetProperty("images")[source.GetInt32()];
            if (!image.TryGetProperty("bufferView", out var view))
            {
                return (null, null); // an image by URI: not fetched
            }

            var mime = image.TryGetProperty("mimeType", out var m) ? m.GetString() : null;
            return (View(view.GetInt32()).ToArray(), mime);
        }

        private ReadOnlySpan<byte> View(int index)
        {
            var view = root.GetProperty("bufferViews")[index];
            var offset = view.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;
            var length = view.GetProperty("byteLength").GetInt32();
            if (offset < 0 || length < 0 || offset + (long)length > bin.Length)
            {
                throw new FormatException("glTF: a bufferView runs past the binary chunk");
            }

            return bin.AsSpan(offset, length);
        }

        /// <summary>An accessor as floats, <paramref name="components"/> per element; integers that are marked
        /// normalized are scaled to [0, 1] or [-1, 1].</summary>
        private float[] Floats(int accessorIndex, int components)
        {
            var (start, stride, count, componentType, normalized, width) = Accessor(accessorIndex);
            var data = bin.AsSpan(start);
            if (width < components)
            {
                throw new FormatException($"glTF: accessor {accessorIndex} has {width} components, expected {components}");
            }

            var result = new float[count * components];
            for (var i = 0; i < count; i++)
            {
                for (var c = 0; c < components; c++)
                {
                    var at = data.Slice(i * stride + c * Size(componentType));
                    result[i * components + c] = componentType switch
                    {
                        5126 => BinaryPrimitives.ReadSingleLittleEndian(at),
                        5121 => normalized ? at[0] / 255f : at[0],
                        5120 => normalized ? Math.Max((sbyte)at[0] / 127f, -1f) : (sbyte)at[0],
                        5123 => normalized
                            ? BinaryPrimitives.ReadUInt16LittleEndian(at) / 65535f
                            : BinaryPrimitives.ReadUInt16LittleEndian(at),
                        5122 => normalized
                            ? Math.Max(BinaryPrimitives.ReadInt16LittleEndian(at) / 32767f, -1f)
                            : BinaryPrimitives.ReadInt16LittleEndian(at),
                        _ => throw new FormatException($"glTF: component type {componentType} for a float attribute"),
                    };
                }
            }

            return result;
        }

        private int[] Indices(int accessorIndex)
        {
            var (start, stride, count, componentType, _, _) = Accessor(accessorIndex);
            var data = bin.AsSpan(start);
            var result = new int[count];
            for (var i = 0; i < count; i++)
            {
                var at = data.Slice(i * stride);
                result[i] = componentType switch
                {
                    5121 => at[0],
                    5123 => BinaryPrimitives.ReadUInt16LittleEndian(at),
                    5125 => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(at)),
                    _ => throw new FormatException($"glTF: component type {componentType} for indices"),
                };
            }

            return result;
        }

        private (int Start, int Stride, int Count, int ComponentType, bool Normalized, int Width) Accessor(
            int index)
        {
            var accessor = root.GetProperty("accessors")[index];
            var componentType = accessor.GetProperty("componentType").GetInt32();
            var count = accessor.GetProperty("count").GetInt32();
            var width = accessor.GetProperty("type").GetString() switch
            {
                "SCALAR" => 1,
                "VEC2" => 2,
                "VEC3" => 3,
                "VEC4" => 4,
                var other => throw new FormatException($"glTF: accessor type {other}"),
            };
            if (!accessor.TryGetProperty("bufferView", out var bv))
            {
                throw new FormatException($"glTF: accessor {index} has no bufferView");
            }

            var viewEl = root.GetProperty("bufferViews")[bv.GetInt32()];
            var element = width * Size(componentType);
            var stride = viewEl.TryGetProperty("byteStride", out var s) ? s.GetInt32() : element;
            var offset = accessor.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;
            var view = View(bv.GetInt32());
            var viewStart = viewEl.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
            if (count < 0 || offset < 0 || (count > 0 && offset + (long)(count - 1) * stride + element > view.Length))
            {
                throw new FormatException($"glTF: accessor {index} runs past its bufferView");
            }

            var normalized = accessor.TryGetProperty("normalized", out var n) && n.GetBoolean();
            return (viewStart + offset, stride, count, componentType, normalized, width);
        }

        private static int Size(int componentType) => componentType switch
        {
            5120 or 5121 => 1,
            5122 or 5123 => 2,
            5125 or 5126 => 4,
            _ => throw new FormatException($"glTF: component type {componentType}"),
        };

        private static double[] Numbers(JsonElement array) => array.EnumerateArray().Select(n => n.GetDouble()).ToArray();

        private static double[]? Optional(JsonElement node, string name) =>
            node.TryGetProperty(name, out var v) ? Numbers(v) : null;
    }
}
