namespace GeoTiles;

using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Openize.Drako;

/// <summary>Writes a small GLB tile, plain or Draco-compressed. It exists for the synthetic tileset (a demo that
/// needs no key and no network) and for the tests of <see cref="GlbReader"/>.</summary>
public static class GlbWriter
{
    public static byte[] Write(float[] positions, float[]? uvs, int[] indices, double[]? nodeMatrix = null,
        bool draco = false, string? copyright = null, byte[]? image = null, string? imageMime = null)
    {
        var bin = new MemoryStream();
        var views = new JsonArray();
        var accessors = new JsonArray();

        int View(ReadOnlySpan<byte> data)
        {
            while (bin.Length % 4 != 0)
            {
                bin.WriteByte(0);
            }

            views.Add(new JsonObject { ["buffer"] = 0, ["byteOffset"] = bin.Length, ["byteLength"] = data.Length });
            bin.Write(data);
            return views.Count - 1;
        }

        int Accessor(int? view, int componentType, int count, string type, float[]? bounds = null)
        {
            var a = new JsonObject { ["componentType"] = componentType, ["count"] = count, ["type"] = type };
            if (view is { } v)
            {
                a["bufferView"] = v;
            }

            if (bounds is not null)
            {
                var min = new JsonArray();
                var max = new JsonArray();
                for (var c = 0; c < 3; c++)
                {
                    min.Add(Enumerable.Range(0, count).Min(i => bounds[i * 3 + c]));
                    max.Add(Enumerable.Range(0, count).Max(i => bounds[i * 3 + c]));
                }

                a["min"] = min;
                a["max"] = max;
            }

            accessors.Add(a);
            return accessors.Count - 1;
        }

        var vertexCount = positions.Length / 3;
        var primitive = new JsonObject();
        var attributes = new JsonObject();
        var extensionsUsed = new JsonArray();
        if (draco)
        {
            var mesh = new DracoMesh();
            var posId = mesh.AddAttribute(PointAttribute.Wrap(AttributeType.Position, 3, positions));
            var uvId = uvs is null ? -1 : mesh.AddAttribute(PointAttribute.Wrap(AttributeType.TexCoord, 2, uvs));
            mesh.NumPoints = vertexCount;
            for (var f = 0; f < indices.Length; f += 3)
            {
                mesh.AddFace([indices[f], indices[f + 1], indices[f + 2]]);
            }

            var bytes = Draco.Encode(mesh, new DracoEncodeOptions { PositionBits = 16, TextureCoordinateBits = 12 });
            var ids = new JsonObject { ["POSITION"] = (int)mesh.Attribute(posId).UniqueId };
            attributes["POSITION"] = Accessor(null, 5126, vertexCount, "VEC3", positions);
            if (uvs is not null)
            {
                ids["TEXCOORD_0"] = (int)mesh.Attribute(uvId).UniqueId;
                attributes["TEXCOORD_0"] = Accessor(null, 5126, vertexCount, "VEC2");
            }

            primitive["indices"] = Accessor(null, 5125, indices.Length, "SCALAR");
            primitive["extensions"] = new JsonObject
            {
                ["KHR_draco_mesh_compression"] = new JsonObject { ["bufferView"] = View(bytes), ["attributes"] = ids },
            };
            extensionsUsed.Add("KHR_draco_mesh_compression");
        }
        else
        {
            attributes["POSITION"] = Accessor(View(Bytes(positions)), 5126, vertexCount, "VEC3", positions);
            if (uvs is not null)
            {
                attributes["TEXCOORD_0"] = Accessor(View(Bytes(uvs)), 5126, vertexCount, "VEC2");
            }

            var raw = new byte[indices.Length * 4];
            for (var i = 0; i < indices.Length; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(i * 4), (uint)indices[i]);
            }

            primitive["indices"] = Accessor(View(raw), 5125, indices.Length, "SCALAR");
        }

        primitive["attributes"] = attributes;
        var asset = new JsonObject { ["version"] = "2.0" };
        if (copyright is not null)
        {
            asset["copyright"] = copyright;
        }

        var node = new JsonObject { ["mesh"] = 0 };
        if (nodeMatrix is not null)
        {
            node["matrix"] = new JsonArray(nodeMatrix.Select(d => (JsonNode)d).ToArray());
        }

        var gltf = new JsonObject
        {
            ["asset"] = asset,
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(0) }),
            ["nodes"] = new JsonArray(node),
            ["meshes"] = new JsonArray(new JsonObject { ["primitives"] = new JsonArray(primitive) }),
        };
        if (image is not null)
        {
            primitive["material"] = 0;
            gltf["materials"] = new JsonArray(new JsonObject
            {
                ["pbrMetallicRoughness"] = new JsonObject { ["baseColorTexture"] = new JsonObject { ["index"] = 0 } },
            });
            gltf["textures"] = new JsonArray(new JsonObject { ["source"] = 0 });
            gltf["images"] = new JsonArray(new JsonObject { ["bufferView"] = View(image), ["mimeType"] = imageMime });
        }

        gltf["accessors"] = accessors;
        gltf["bufferViews"] = views;
        gltf["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = bin.Length });
        if (extensionsUsed.Count > 0)
        {
            gltf["extensionsUsed"] = extensionsUsed;
            gltf["extensionsRequired"] = extensionsUsed.DeepClone();
        }

        var json = Pad(JsonSerializer.SerializeToUtf8Bytes(gltf), (byte)' ');
        var binary = Pad(bin.ToArray(), 0);
        var glb = new byte[12 + 8 + json.Length + 8 + binary.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(glb, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8), (uint)glb.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), (uint)json.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), 0x4E4F534A);
        json.CopyTo(glb, 20);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(20 + json.Length), (uint)binary.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(24 + json.Length), 0x004E4942);
        binary.CopyTo(glb, 28 + json.Length);
        return glb;
    }

    private static byte[] Bytes(float[] values)
    {
        var raw = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(i * 4), values[i]);
        }

        return raw;
    }

    private static byte[] Pad(byte[] data, byte with)
    {
        var padded = new byte[(data.Length + 3) / 4 * 4];
        data.CopyTo(padded, 0);
        padded.AsSpan(data.Length).Fill(with);
        return padded;
    }
}
