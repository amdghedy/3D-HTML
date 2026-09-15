using System;
using System.Collections.Generic;

namespace ClearView.Export
{
    public static class MeshMerger
    {
        /// <summary>
        /// One mesh per Revit element (all faces combined). Keeps element selection + properties
        /// while cutting node count from ~6×elements down to 1×elements.
        /// </summary>
        public static List<MeshRecord> MergeByElement(IList<MeshRecord> source)
        {
            var map = new Dictionary<int, MeshRecord>();
            foreach (var m in source)
            {
                if (m.Positions == null || m.Positions.Count < 9 || m.Indices == null || m.Indices.Count < 3)
                    continue;

                int key = m.ElementId;
                if (!map.TryGetValue(key, out var acc))
                {
                    acc = new MeshRecord
                    {
                        ElementId = m.ElementId,
                        UniqueId = m.UniqueId,
                        Category = m.Category,
                        Level = m.Level,
                        Name = m.Name,
                        R = m.R,
                        G = m.G,
                        B = m.B,
                        Opacity = m.Opacity,
                        Properties = m.Properties ?? new Dictionary<string, string>(),
                        Positions = new List<double>(m.Positions.Count),
                        Indices = new List<int>(m.Indices.Count)
                    };
                    map[key] = acc;
                }

                int baseIndex = acc.Positions.Count / 3;
                acc.Positions.AddRange(m.Positions);
                for (int i = 0; i < m.Indices.Count; i++)
                    acc.Indices.Add(baseIndex + m.Indices[i]);
            }

            return new List<MeshRecord>(map.Values);
        }

        /// <summary>
        /// Aggressive merge by level + category + quantised colour (large models / file size).
        /// Selection becomes a whole group, not a single element.
        /// </summary>
        public static List<MeshRecord> MergeByCategory(IList<MeshRecord> source)
        {
            var map = new Dictionary<string, MeshRecord>(StringComparer.Ordinal);
            foreach (var m in source)
            {
                if (m.Positions == null || m.Positions.Count < 9 || m.Indices == null || m.Indices.Count < 3)
                    continue;

                int rq = (int)(m.R * 32f);
                int gq = (int)(m.G * 32f);
                int bq = (int)(m.B * 32f);
                int oq = (int)(m.Opacity * 8f);
                string key = m.Level + "|" + m.Category + "|" + rq + "," + gq + "," + bq + "," + oq;

                if (!map.TryGetValue(key, out var acc))
                {
                    acc = new MeshRecord
                    {
                        ElementId = 0,
                        UniqueId = "merged",
                        Category = m.Category,
                        Level = m.Level,
                        Name = m.Category,
                        R = m.R,
                        G = m.G,
                        B = m.B,
                        Opacity = m.Opacity,
                        Properties = new Dictionary<string, string>
                        {
                            ["Category"] = m.Category ?? "",
                            ["Level"] = m.Level ?? "",
                            ["Note"] = "Merged group (enable per-element export for full properties)"
                        },
                        Positions = new List<double>(m.Positions.Count),
                        Indices = new List<int>(m.Indices.Count)
                    };
                    map[key] = acc;
                }

                int baseIndex = acc.Positions.Count / 3;
                acc.Positions.AddRange(m.Positions);
                for (int i = 0; i < m.Indices.Count; i++)
                    acc.Indices.Add(baseIndex + m.Indices[i]);
            }

            return new List<MeshRecord>(map.Values);
        }

        // Back-compat name used by ExportCommand
        public static List<MeshRecord> Merge(IList<MeshRecord> source) => MergeByCategory(source);
    }
}
