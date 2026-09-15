using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ClearView.Export
{
    /// <summary>
    /// Minimal glTF 2.0 writer. One mesh + node per MeshRecord.
    /// extras.level / extras.category / extras.color drive the HTML layer UI.
    /// </summary>
    public static class GltfWriter
    {
        public static JObject Build(IList<MeshRecord> meshes)
        {
            var bin = new List<byte>();
            var accessors = new JArray();
            var bufferViews = new JArray();
            var glMeshes = new JArray();
            var nodes = new JArray();
            var materials = new JArray();
            var sceneNodes = new JArray();

            for (int i = 0; i < meshes.Count; i++)
            {
                var m = meshes[i];
                if (m.Positions.Count < 9 || m.Indices.Count < 3)
                    continue;

                var posBytes = ToFloatBytes(m.Positions);
                var idxBytes = ToUshortOrUintBytes(m.Indices, out int componentType);

                int posOff = bin.Count;
                bin.AddRange(posBytes);
                Pad4(bin);
                int idxOff = bin.Count;
                bin.AddRange(idxBytes);
                Pad4(bin);

                int bvPos = bufferViews.Count;
                bufferViews.Add(new JObject
                {
                    ["buffer"] = 0,
                    ["byteOffset"] = posOff,
                    ["byteLength"] = posBytes.Length,
                    ["target"] = 34962
                });
                int bvIdx = bufferViews.Count;
                bufferViews.Add(new JObject
                {
                    ["buffer"] = 0,
                    ["byteOffset"] = idxOff,
                    ["byteLength"] = idxBytes.Length,
                    ["target"] = 34963
                });

                MinMax(m.Positions, out var min, out var max);
                int accPos = accessors.Count;
                accessors.Add(new JObject
                {
                    ["bufferView"] = bvPos,
                    ["componentType"] = 5126,
                    ["count"] = m.Positions.Count / 3,
                    ["type"] = "VEC3",
                    ["min"] = new JArray(min),
                    ["max"] = new JArray(max)
                });
                int accIdx = accessors.Count;
                accessors.Add(new JObject
                {
                    ["bufferView"] = bvIdx,
                    ["componentType"] = componentType,
                    ["count"] = m.Indices.Count,
                    ["type"] = "SCALAR"
                });

                int matIndex = materials.Count;
                var opaque = m.Opacity >= 0.999f;
                materials.Add(new JObject
                {
                    ["name"] = m.Category,
                    ["pbrMetallicRoughness"] = new JObject
                    {
                        ["baseColorFactor"] = new JArray(m.R, m.G, m.B, m.Opacity),
                        ["metallicFactor"] = 0.05,
                        ["roughnessFactor"] = 0.75
                    },
                    ["alphaMode"] = opaque ? "OPAQUE" : "BLEND",
                    ["doubleSided"] = !opaque
                });

                glMeshes.Add(new JObject
                {
                    ["primitives"] = new JArray(new JObject
                    {
                        ["attributes"] = new JObject { ["POSITION"] = accPos },
                        ["indices"] = accIdx,
                        ["material"] = matIndex
                    })
                });

                int nodeIndex = nodes.Count;
                nodes.Add(new JObject
                {
                    ["name"] = m.Category + ":" + m.ElementId,
                    ["mesh"] = glMeshes.Count - 1,
                    ["extras"] = BuildExtras(m)
                });
                sceneNodes.Add(nodeIndex);
            }

            var b64 = Convert.ToBase64String(bin.ToArray());
            return new JObject
            {
                ["asset"] = new JObject
                {
                    ["version"] = "2.0",
                    ["generator"] = "ClearView 3D Path B"
                },
                ["scene"] = 0,
                ["scenes"] = new JArray(new JObject { ["nodes"] = sceneNodes }),
                ["nodes"] = nodes,
                ["meshes"] = glMeshes,
                ["materials"] = materials,
                ["accessors"] = accessors,
                ["bufferViews"] = bufferViews,
                ["buffers"] = new JArray(new JObject
                {
                    ["byteLength"] = bin.Count,
                    ["uri"] = "data:application/octet-stream;base64," + b64
                })
            };
        }

        static byte[] ToFloatBytes(List<double> src)
        {
            var buf = new byte[src.Count * 4];
            for (int i = 0; i < src.Count; i++)
                BitConverter.GetBytes((float)src[i]).CopyTo(buf, i * 4);
            return buf;
        }

        static byte[] ToUshortOrUintBytes(List<int> src, out int componentType)
        {
            int max = 0;
            for (int i = 0; i < src.Count; i++)
                if (src[i] > max) max = src[i];

            if (max <= ushort.MaxValue)
            {
                componentType = 5123; // UNSIGNED_SHORT
                var buf = new byte[src.Count * 2];
                for (int i = 0; i < src.Count; i++)
                    BitConverter.GetBytes((ushort)src[i]).CopyTo(buf, i * 2);
                return buf;
            }

            componentType = 5125; // UNSIGNED_INT
            var buf32 = new byte[src.Count * 4];
            for (int i = 0; i < src.Count; i++)
                BitConverter.GetBytes(src[i]).CopyTo(buf32, i * 4);
            return buf32;
        }

        static void Pad4(List<byte> bin)
        {
            while (bin.Count % 4 != 0) bin.Add(0);
        }

        static void MinMax(List<double> p, out float[] min, out float[] max)
        {
            min = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
            max = new[] { float.MinValue, float.MinValue, float.MinValue };
            for (int i = 0; i < p.Count; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    var v = (float)p[i + k];
                    if (v < min[k]) min[k] = v;
                    if (v > max[k]) max[k] = v;
                }
            }
        }

        static JObject BuildExtras(MeshRecord m)
        {
            var extras = new JObject
            {
                ["level"] = m.Level ?? "",
                ["category"] = m.Category ?? "",
                ["name"] = m.Name ?? "",
                ["elementId"] = m.ElementId,
                ["uniqueId"] = m.UniqueId ?? "",
                ["color"] = new JArray(m.R, m.G, m.B, m.Opacity)
            };
            if (m.Properties != null && m.Properties.Count > 0)
            {
                var props = new JObject();
                foreach (var kv in m.Properties)
                    props[kv.Key] = kv.Value;
                extras["properties"] = props;
            }
            return extras;
        }
    }
}
