using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ClearView.Export
{
    /// <summary>
    /// Forces export of architectural host objects that CustomExporter often under-exports:
    /// Ceilings, Floors, Roofs, Walls (especially compound ceilings / thin faces).
    /// </summary>
    public static class HostGeometryFallback
    {
        static readonly BuiltInCategory[] HostCats =
        {
            BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Walls
        };

        public static void AppendMissing(
            Document hostDoc,
            View3D view,
            ExportSettings settings,
            RevitExportContext context)
        {
            var already = new HashSet<int>();
            foreach (var m in context.Meshes)
                already.Add(m.ElementId);

            int added = 0;
            int meshes = 0;

            BoundingBoxXYZ sectionBox = null;
            bool hasSectionBox = false;
            try
            {
                if (view.IsSectionBoxActive)
                {
                    sectionBox = view.GetSectionBox();
                    hasSectionBox = sectionBox != null;
                }
            }
            catch { }

            var optView = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine,
                View = view
            };
            var optFull = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            ProcessDoc(hostDoc, Transform.Identity, view, settings, context,
                already, optView, optFull, sectionBox, hasSectionBox, ref added, ref meshes);

            if (settings.IncludeLinks)
            {
                foreach (RevitLinkInstance linkInst in new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(RevitLinkInstance)))
                {
                    Document linkDoc = null;
                    try { linkDoc = linkInst.GetLinkDocument(); } catch { }
                    if (linkDoc == null) continue;

                    Transform linkTr = Transform.Identity;
                    try { linkTr = linkInst.GetTotalTransform() ?? Transform.Identity; } catch { }

                    ProcessDoc(linkDoc, linkTr, view, settings, context,
                        already, optView, optFull, sectionBox, hasSectionBox, ref added, ref meshes);
                }
            }

            context.StatusMessage = "Host pass: " + added + " ceilings/floors/roofs/walls, " + meshes + " mesh parts";
        }

        static void ProcessDoc(
            Document doc,
            Transform toHost,
            View3D view,
            ExportSettings settings,
            RevitExportContext context,
            HashSet<int> already,
            Options optView,
            Options optFull,
            BoundingBoxXYZ sectionBox,
            bool hasSectionBox,
            ref int added,
            ref int meshes)
        {
            foreach (var bic in HostCats)
            {
                IList<Element> elems;
                try
                {
                    elems = new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElements();
                }
                catch { continue; }

                foreach (Element el in elems)
                {
                    if (el == null) continue;
                    int eid = el.Id.IntegerValue;
                    if (already.Contains(eid)) continue;

                    string catName = null;
                    try { catName = el.Category?.Name; } catch { }
                    if (string.IsNullOrEmpty(catName)) catName = bic.ToString().Replace("OST_", "");

                    if (!settings.IsCategoryIncluded(catName))
                    {
                        // Always allow ceilings/floors/roofs/walls by built-in category name
                        string lower = catName.ToLowerInvariant();
                        if (!(lower.Contains("ceiling") || lower.Contains("floor") ||
                              lower.Contains("roof") || lower.Contains("wall")))
                            continue;
                    }

                    string level = ResolveLevel(el, doc);
                    if (!settings.IsLevelIncluded(level) && level != "Unassigned")
                        continue;

                    if (hasSectionBox && !Intersects(el, toHost, sectionBox))
                        continue;

                    Color color = ResolveHostColor(el, catName);
                    var props = PropertyCollector.Collect(el);

                    int before = context.Meshes.Count;
                    ExtractHost(el, optView, toHost, catName, level, props, color, context.Meshes);
                    if (context.Meshes.Count == before)
                        ExtractHost(el, optFull, toHost, catName, level, props, color, context.Meshes);

                    // Last resort: tessellate from HostObject solid faces
                    if (context.Meshes.Count == before && el is HostObject)
                        ExtractHostObjectFaces(el as HostObject, toHost, catName, level, props, color, context.Meshes);

                    if (context.Meshes.Count == before)
                        AddBoundingBoxMesh(el, toHost, catName, level, props, color, context.Meshes);

                    if (context.Meshes.Count > before)
                    {
                        already.Add(eid);
                        added++;
                        meshes += (context.Meshes.Count - before);
                    }
                }
            }
        }

        static void ExtractHost(
            Element el,
            Options opt,
            Transform toHost,
            string catName,
            string level,
            Dictionary<string, string> props,
            Color color,
            List<MeshRecord> output)
        {
            try
            {
                GeometryElement ge = el.get_Geometry(opt);
                if (ge == null) return;
                Walk(ge, toHost, el, catName, level, props, color, output, 0);
            }
            catch { }
        }

        /// <summary>
        /// Compound ceilings sometimes expose geometry only through GetGeometryObjectFromReference
        /// or solid faces on the HostObject – walk solids directly.
        /// </summary>
        static void ExtractHostObjectFaces(
            HostObject host,
            Transform toHost,
            string catName,
            string level,
            Dictionary<string, string> props,
            Color color,
            List<MeshRecord> output)
        {
            try
            {
                var opt = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = true,
                    DetailLevel = ViewDetailLevel.Fine
                };
                GeometryElement ge = host.get_Geometry(opt);
                if (ge == null) return;
                Walk(ge, toHost, host, catName, level, props, color, output, 0);
            }
            catch { }
        }

        static void Walk(
            GeometryElement ge,
            Transform outer,
            Element el,
            string catName,
            string level,
            Dictionary<string, string> props,
            Color color,
            List<MeshRecord> output,
            int depth)
        {
            if (ge == null || depth > 8) return;
            foreach (GeometryObject go in ge)
            {
                if (go == null) continue;
                if (go is Solid solid)
                {
                    // Accept zero-volume thin solids (ceilings are often near-zero thickness)
                    if (solid.Faces == null || solid.Faces.Size < 1) continue;
                    var rec = NewRec(el, catName, level, props, color);
                    if (AppendSolid(solid, outer, rec))
                        output.Add(rec);
                }
                else if (go is Mesh mesh)
                {
                    if (mesh.NumTriangles < 1) continue;
                    var rec = NewRec(el, catName, level, props, color);
                    if (AppendMesh(mesh, outer, rec))
                        output.Add(rec);
                }
                else if (go is GeometryInstance gi)
                {
                    Transform local = outer;
                    try
                    {
                        if (gi.Transform != null)
                            local = outer.Multiply(gi.Transform);
                    }
                    catch { }
                    GeometryElement nested = null;
                    try { nested = gi.GetInstanceGeometry(); } catch { }
                    if (nested != null)
                        Walk(nested, outer, el, catName, level, props, color, output, depth + 1);
                    try { nested = gi.GetSymbolGeometry(); } catch { nested = null; }
                    if (nested != null)
                        Walk(nested, local, el, catName, level, props, color, output, depth + 1);
                }
            }
        }

        static bool AppendSolid(Solid solid, Transform t, MeshRecord rec)
        {
            int start = rec.Positions.Count / 3;
            int faces = 0;
            foreach (Face face in solid.Faces)
            {
                Mesh mesh = null;
                try { mesh = face.Triangulate(); } catch { }
                if (mesh == null || mesh.NumTriangles < 1) continue;
                int baseIdx = rec.Positions.Count / 3;
                for (int i = 0; i < mesh.NumTriangles; i++)
                {
                    MeshTriangle tri = mesh.get_Triangle(i);
                    for (int v = 0; v < 3; v++)
                    {
                        XYZ p = tri.get_Vertex(v);
                        if (t != null && !t.IsIdentity) p = t.OfPoint(p);
                        // Revit internal feet → glTF (X, Z, -Y)
                        rec.Positions.Add(p.X);
                        rec.Positions.Add(p.Z);
                        rec.Positions.Add((-p.Y));
                    }
                    rec.Indices.Add(baseIdx + i * 3);
                    rec.Indices.Add(baseIdx + i * 3 + 1);
                    rec.Indices.Add(baseIdx + i * 3 + 2);
                    faces++;
                }
            }
            return faces > 0;
        }

        static bool AppendMesh(Mesh mesh, Transform t, MeshRecord rec)
        {
            int baseIdx = rec.Positions.Count / 3;
            for (int i = 0; i < mesh.NumTriangles; i++)
            {
                MeshTriangle tri = mesh.get_Triangle(i);
                for (int v = 0; v < 3; v++)
                {
                    XYZ p = tri.get_Vertex(v);
                    if (t != null && !t.IsIdentity) p = t.OfPoint(p);
                    rec.Positions.Add(p.X);
                    rec.Positions.Add(p.Z);
                    rec.Positions.Add((-p.Y));
                }
                rec.Indices.Add(baseIdx + i * 3);
                rec.Indices.Add(baseIdx + i * 3 + 1);
                rec.Indices.Add(baseIdx + i * 3 + 2);
            }
            return mesh.NumTriangles > 0;
        }

        static void AddBoundingBoxMesh(
            Element el, Transform toHost, string catName, string level,
            Dictionary<string, string> props, Color color, List<MeshRecord> output)
        {
            try
            {
                BoundingBoxXYZ bb = el.get_BoundingBox(null);
                if (bb == null) return;
                XYZ mn = bb.Min, mx = bb.Max;
                // Thin box so ceiling is still visible as a slab
                var corners = new[]
                {
                    new XYZ(mn.X, mn.Y, mn.Z), new XYZ(mx.X, mn.Y, mn.Z),
                    new XYZ(mx.X, mx.Y, mn.Z), new XYZ(mn.X, mx.Y, mn.Z),
                    new XYZ(mn.X, mn.Y, mx.Z), new XYZ(mx.X, mn.Y, mx.Z),
                    new XYZ(mx.X, mx.Y, mx.Z), new XYZ(mn.X, mx.Y, mx.Z)
                };
                var rec = NewRec(el, catName, level, props, color);
                int[] faces =
                {
                    0,1,2, 0,2,3, 4,6,5, 4,7,6,
                    0,4,5, 0,5,1, 1,5,6, 1,6,2,
                    2,6,7, 2,7,3, 3,7,4, 3,4,0
                };
                for (int i = 0; i < 8; i++)
                {
                    XYZ p = corners[i];
                    if (toHost != null && !toHost.IsIdentity) p = toHost.OfPoint(p);
                    rec.Positions.Add(p.X);
                    rec.Positions.Add(p.Z);
                    rec.Positions.Add((-p.Y));
                }
                foreach (int ix in faces) rec.Indices.Add(ix);
                output.Add(rec);
            }
            catch { }
        }

        static MeshRecord NewRec(
            Element el, string catName, string level,
            Dictionary<string, string> props, Color color)
        {
            return new MeshRecord
            {
                ElementId = el.Id.IntegerValue,
                UniqueId = el.UniqueId,
                Name = el.Name,
                Category = catName,
                Level = level ?? "Unassigned",
                Properties = props ?? new Dictionary<string, string>(),
                R = color != null ? color.Red / 255f : 0.82f,
                G = color != null ? color.Green / 255f : 0.82f,
                B = color != null ? color.Blue / 255f : 0.84f,
                Positions = new List<double>(),
                Indices = new List<int>()
            };
        }

        static Color ResolveHostColor(Element el, string catName)
        {
            try
            {
                // Material colour if available
                var matId = el.GetMaterialIds(false);
                // Prefer system/category map
            }
            catch { }
            var mapped = SystemColorMap.TryGet(catName);
            if (mapped != null) return mapped;
            string n = (catName ?? "").ToLowerInvariant();
            if (n.Contains("ceiling")) return new Color(210, 210, 215);
            if (n.Contains("floor")) return new Color(190, 175, 140);
            if (n.Contains("roof")) return new Color(120, 100, 90);
            if (n.Contains("wall")) return new Color(170, 170, 170);
            return new Color(180, 180, 180);
        }

        static string ResolveLevel(Element el, Document doc)
        {
            try
            {
                if (el.LevelId != null && el.LevelId != ElementId.InvalidElementId)
                {
                    var lv = doc.GetElement(el.LevelId) as Level;
                    if (lv != null) return lv.Name;
                }
                var p = el.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                     ?? el.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
                     ?? el.LookupParameter("Level");
                if (p != null && p.HasValue)
                {
                    string s = p.AsValueString() ?? p.AsString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            catch { }
            return "Unassigned";
        }

        static bool Intersects(Element el, Transform toHost, BoundingBoxXYZ sectionBox)
        {
            try
            {
                BoundingBoxXYZ bb = el.get_BoundingBox(null);
                if (bb == null) return true;
                XYZ mn = bb.Min, mx = bb.Max;
                if (toHost != null && !toHost.IsIdentity)
                {
                    mn = toHost.OfPoint(mn);
                    mx = toHost.OfPoint(mx);
                }
                // Expand slightly for thin ceilings
                double tol = 0.5;
                XYZ smin = sectionBox.Transform.OfPoint(sectionBox.Min);
                XYZ smax = sectionBox.Transform.OfPoint(sectionBox.Max);
                double minx = Math.Min(smin.X, smax.X) - tol;
                double maxx = Math.Max(smin.X, smax.X) + tol;
                double miny = Math.Min(smin.Y, smax.Y) - tol;
                double maxy = Math.Max(smin.Y, smax.Y) + tol;
                double minz = Math.Min(smin.Z, smax.Z) - tol;
                double maxz = Math.Max(smin.Z, smax.Z) + tol;

                double eminx = Math.Min(mn.X, mx.X);
                double emaxx = Math.Max(mn.X, mx.X);
                double eminy = Math.Min(mn.Y, mx.Y);
                double emaxy = Math.Max(mn.Y, mx.Y);
                double eminz = Math.Min(mn.Z, mx.Z);
                double emaxz = Math.Max(mn.Z, mx.Z);

                return !(emaxx < minx || eminx > maxx || emaxy < miny || eminy > maxy || emaxz < minz || eminz > maxz);
            }
            catch { return true; }
        }
    }
}
