using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ClearView.Export
{
    /// <summary>
    /// Forces export of ALL MEP system families (pipes, ducts, cable trays, conduits)
    /// from HOST + every loaded LINK. Special handling for curve-based elements
    /// whose geometry often fails to appear through CustomExporter alone.
    /// </summary>
    public static class MepGeometryFallback
    {
        public static void AppendMissing(
            Document hostDoc,
            View3D view,
            ExportSettings settings,
            RevitExportContext context)
        {
            var already = new HashSet<int>();
            foreach (var m in context.Meshes)
                already.Add(m.ElementId);

            int addedElements = 0;
            int addedMeshes = 0;
            int fittingHits = 0;
            int linkElements = 0;
            int pipeTrayHits = 0;

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

            // Two option sets:
            // 1) View-aware (host clipping)
            var optView = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine,
                View = view
            };
            // 2) Full geometry (needed for many linked system families)
            var optFull = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            // ---------- Host ----------
            ProcessDocument(
                hostDoc, Transform.Identity, hostDoc, view, settings, context,
                already, optView, optFull, sectionBox, hasSectionBox,
                ref addedElements, ref addedMeshes, ref fittingHits, ref pipeTrayHits, isLink: false);

            // ---------- Every loaded RVT link ----------
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

                    int before = addedElements;
                    ProcessDocument(
                        linkDoc, linkTr, hostDoc, view, settings, context,
                        already, optView, optFull, sectionBox, hasSectionBox,
                        ref addedElements, ref addedMeshes, ref fittingHits, ref pipeTrayHits, isLink: true);

                    linkElements += (addedElements - before);
                }
            }

            context.StatusMessage =
                "MEP pass: " + addedElements + " elements (" +
                fittingHits + " fittings, " + pipeTrayHits + " pipes/trays, " +
                linkElements + " from links), " + addedMeshes + " mesh parts";
        }

        static void ProcessDocument(
            Document doc,
            Transform toHost,
            Document hostDoc,
            View3D view,
            ExportSettings settings,
            RevitExportContext context,
            HashSet<int> already,
            Options optView,
            Options optFull,
            BoundingBoxXYZ sectionBox,
            bool hasSectionBox,
            ref int addedElements,
            ref int addedMeshes,
            ref int fittingHits,
            ref int pipeTrayHits,
            bool isLink)
        {
            // All MEP built-in categories – force every one
            foreach (var bic in MepCategories.BuiltIn)
            {
                IList<Element> elements;
                try
                {
                    elements = new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElements();
                }
                catch { continue; }

                foreach (Element el in elements)
                {
                    if (el == null) continue;
                    Category cat = el.Category;
                    if (cat == null) continue;

                    string catName = cat.Name ?? "";
                    bool isFitting = IsFittingCategory(catName);
                    bool isPipeOrTray = IsPipeOrTrayCategory(catName, bic);

                    // Always allow known MEP names even if the UI list missed the exact spelling
                    if (!settings.IsCategoryIncluded(catName) &&
                        !MepCategories.IsMepName(catName) &&
                        !isPipeOrTray)
                        continue;

                    if (!isLink)
                    {
                        try
                        {
                            if (view.GetCategoryHidden(cat.Id))
                                continue;
                        }
                        catch { }
                    }

                    int eid = el.Id.IntegerValue;

                    // Fittings: always re-extract (CustomExporter often fails on elbows/Y).
                    // Pipes/trays: keep existing mesh ONLY if it has real triangles;
                    // otherwise force re-extract (empty placeholder is useless).
                    if (isFitting)
                    {
                        context.Meshes.RemoveAll(m => m.ElementId == eid);
                        already.Remove(eid);
                    }
                    else if (already.Contains(eid))
                    {
                        bool hasTris = false;
                        for (int mi = 0; mi < context.Meshes.Count; mi++)
                        {
                            var m = context.Meshes[mi];
                            if (m.ElementId == eid && m.Indices != null && m.Indices.Count >= 3)
                            {
                                hasTris = true;
                                break;
                            }
                        }
                        if (hasTris)
                            continue;   // good mesh already present
                        // empty / broken mesh → remove and re-extract
                        context.Meshes.RemoveAll(m => m.ElementId == eid);
                        already.Remove(eid);
                    }

                    // Section-box cull – NEVER for pipes/trays (too easy to drop long runs).
                    // Fittings and other MEP still use a loose intersection test.
                    if (hasSectionBox && !isPipeOrTray && !IntersectsSectionBox(el, toHost, sectionBox, false))
                        continue;

                    string level = ResolveLevel(doc, el);
                    if (!settings.IsLevelIncluded(level) && level != "Unassigned")
                        continue;

                    var props = PropertyCollector.Collect(el);
                    Color color = ResolveColor(el, catName);

                    int before = context.Meshes.Count;

                    // Prefer full geometry options for linked system families
                    Options primary = isLink ? optFull : optView;
                    Options secondary = isLink ? optView : optFull;

                    if (el is FamilyInstance fi)
                        ExtractFamilyInstance(fi, primary, toHost, catName, level, props, color, context.Meshes);

                    if (context.Meshes.Count == before)
                        ExtractFromElementGeometry(el, primary, toHost, catName, level, props, color, context.Meshes);

                    // Second attempt with the other Options set
                    if (context.Meshes.Count == before)
                        ExtractFromElementGeometry(el, secondary, toHost, catName, level, props, color, context.Meshes);

                    // Special path for MEPCurve (pipes, ducts, trays, conduits)
                    if (context.Meshes.Count == before && el is MEPCurve mepCurve)
                        ExtractMepCurve(mepCurve, primary, toHost, catName, level, props, color, context.Meshes);

                    if (context.Meshes.Count == before && el is MEPCurve mepCurve2)
                        ExtractMepCurve(mepCurve2, secondary, toHost, catName, level, props, color, context.Meshes);

                    // Bounding-box fallback so the element is never completely invisible
                    if (context.Meshes.Count == before && (isFitting || isPipeOrTray))
                        AddBoundingBoxMesh(el, toHost, catName, level, props, color, context.Meshes);

                    int delta = context.Meshes.Count - before;
                    if (delta > 0)
                    {
                        already.Add(eid);
                        addedElements++;
                        addedMeshes += delta;
                        if (isFitting) fittingHits++;
                        if (isPipeOrTray) pipeTrayHits++;
                    }
                }
            }
        }

        static bool IsFittingCategory(string catName)
        {
            if (string.IsNullOrEmpty(catName)) return false;
            string n = catName.ToLowerInvariant();
            return n.Contains("fitting") || n.Contains("accessory") || n.Contains("terminal")
                || n.Contains("elbow") || n.Contains("tee") || n.Contains("wye")
                || n.Contains("union") || n.Contains("coupling") || n.Contains("reducer")
                || n.Contains("flange") || n.Contains("valve") || n.Contains("cap");
        }

        static bool IsPipeOrTrayCategory(string catName, BuiltInCategory bic)
        {
            if (bic == BuiltInCategory.OST_PipeCurves ||
                bic == BuiltInCategory.OST_FlexPipeCurves ||
                bic == BuiltInCategory.OST_CableTray ||
                bic == BuiltInCategory.OST_Conduit ||
                bic == BuiltInCategory.OST_DuctCurves ||
                bic == BuiltInCategory.OST_FlexDuctCurves ||
                bic == BuiltInCategory.OST_PlaceHolderPipes ||
                bic == BuiltInCategory.OST_PlaceHolderDucts)
                return true;

            if (string.IsNullOrEmpty(catName)) return false;
            string n = catName.ToLowerInvariant();
            return n.Contains("pipe") || n.Contains("cable tray") || n.Contains("conduit") ||
                   n.Contains("duct") && !n.Contains("fitting") && !n.Contains("accessor");
        }

        /// <summary>
        /// Conservative AABB test. For long pipes/trays we expand the tolerance
        /// so partially-inside elements are kept.
        /// </summary>
        static bool IntersectsSectionBox(Element el, Transform toHost, BoundingBoxXYZ sectionBox, bool isLongElement)
        {
            try
            {
                BoundingBoxXYZ bb = el.get_BoundingBox(null);
                if (bb == null) return true;

                XYZ[] corners = new XYZ[8];
                XYZ mn = bb.Min, mx = bb.Max;
                corners[0] = new XYZ(mn.X, mn.Y, mn.Z);
                corners[1] = new XYZ(mx.X, mn.Y, mn.Z);
                corners[2] = new XYZ(mx.X, mx.Y, mn.Z);
                corners[3] = new XYZ(mn.X, mx.Y, mn.Z);
                corners[4] = new XYZ(mn.X, mn.Y, mx.Z);
                corners[5] = new XYZ(mx.X, mn.Y, mx.Z);
                corners[6] = new XYZ(mx.X, mx.Y, mx.Z);
                corners[7] = new XYZ(mn.X, mx.Y, mx.Z);

                bool hasTr = toHost != null && !toHost.IsIdentity;
                for (int i = 0; i < 8; i++)
                    if (hasTr) corners[i] = toHost.OfPoint(corners[i]);

                Transform sbTr = sectionBox.Transform ?? Transform.Identity;
                XYZ sbMin = sectionBox.Min;
                XYZ sbMax = sectionBox.Max;

                // Expand section box slightly for long MEP runs
                double expand = isLongElement ? 2.0 : 0.1; // feet
                XYZ[] sbCorners = new XYZ[8];
                sbCorners[0] = sbTr.OfPoint(new XYZ(sbMin.X - expand, sbMin.Y - expand, sbMin.Z - expand));
                sbCorners[1] = sbTr.OfPoint(new XYZ(sbMax.X + expand, sbMin.Y - expand, sbMin.Z - expand));
                sbCorners[2] = sbTr.OfPoint(new XYZ(sbMax.X + expand, sbMax.Y + expand, sbMin.Z - expand));
                sbCorners[3] = sbTr.OfPoint(new XYZ(sbMin.X - expand, sbMax.Y + expand, sbMin.Z - expand));
                sbCorners[4] = sbTr.OfPoint(new XYZ(sbMin.X - expand, sbMin.Y - expand, sbMax.Z + expand));
                sbCorners[5] = sbTr.OfPoint(new XYZ(sbMax.X + expand, sbMin.Y - expand, sbMax.Z + expand));
                sbCorners[6] = sbTr.OfPoint(new XYZ(sbMax.X + expand, sbMax.Y + expand, sbMax.Z + expand));
                sbCorners[7] = sbTr.OfPoint(new XYZ(sbMin.X - expand, sbMax.Y + expand, sbMax.Z + expand));

                double eMinX = double.MaxValue, eMinY = double.MaxValue, eMinZ = double.MaxValue;
                double eMaxX = double.MinValue, eMaxY = double.MinValue, eMaxZ = double.MinValue;
                foreach (var c in corners)
                {
                    eMinX = Math.Min(eMinX, c.X); eMaxX = Math.Max(eMaxX, c.X);
                    eMinY = Math.Min(eMinY, c.Y); eMaxY = Math.Max(eMaxY, c.Y);
                    eMinZ = Math.Min(eMinZ, c.Z); eMaxZ = Math.Max(eMaxZ, c.Z);
                }

                double sMinX = double.MaxValue, sMinY = double.MaxValue, sMinZ = double.MaxValue;
                double sMaxX = double.MinValue, sMaxY = double.MinValue, sMaxZ = double.MinValue;
                foreach (var c in sbCorners)
                {
                    sMinX = Math.Min(sMinX, c.X); sMaxX = Math.Max(sMaxX, c.X);
                    sMinY = Math.Min(sMinY, c.Y); sMaxY = Math.Max(sMaxY, c.Y);
                    sMinZ = Math.Min(sMinZ, c.Z); sMaxZ = Math.Max(sMaxZ, c.Z);
                }

                if (eMaxX < sMinX || eMinX > sMaxX) return false;
                if (eMaxY < sMinY || eMinY > sMaxY) return false;
                if (eMaxZ < sMinZ || eMinZ > sMaxZ) return false;
                return true;
            }
            catch { return true; }
        }

        static void ExtractMepCurve(
            MEPCurve curve,
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
                GeometryElement ge = curve.get_Geometry(opt);
                if (ge != null)
                    WalkGeometry(ge, toHost, curve, catName, level, props, color, output);
            }
            catch { }

            // Second attempt – some linked system families return geometry only on retry
            try
            {
                if (!HasMeshFor(output, curve.Id.IntegerValue))
                {
                    GeometryElement ge2 = curve.get_Geometry(opt);
                    if (ge2 != null)
                        WalkGeometry(ge2, toHost, curve, catName, level, props, color, output);
                }
            }
            catch { }
        }

        static void ExtractFamilyInstance(
            FamilyInstance fi,
            Options opt,
            Transform outerToHost,
            string catName,
            string level,
            Dictionary<string, string> props,
            Color color,
            List<MeshRecord> output)
        {
            Transform instTr;
            try { instTr = fi.GetTransform(); }
            catch { instTr = Transform.Identity; }

            Transform combined = outerToHost.Multiply(instTr);

            // A) Instance geometry (already in place)
            try
            {
                GeometryElement ge = fi.get_Geometry(opt);
                if (ge != null)
                    WalkGeometryDeep(ge, outerToHost, fi, catName, level, props, color, output, 0);
            }
            catch { }

            if (HasMeshFor(output, fi.Id.IntegerValue))
                return;

            // B) Symbol geometry × instance transform (most reliable for elbows / Y / tees)
            try
            {
                FamilySymbol sym = fi.Symbol;
                if (sym != null)
                {
                    // Prefer Fine detail on the symbol
                    var symOpt = new Options
                    {
                        ComputeReferences = false,
                        IncludeNonVisibleObjects = true,
                        DetailLevel = ViewDetailLevel.Fine
                    };
                    GeometryElement sge = sym.get_Geometry(symOpt);
                    if (sge != null)
                        WalkGeometryDeep(sge, combined, fi, catName, level, props, color, output, 0);
                }
            }
            catch { }

            if (HasMeshFor(output, fi.Id.IntegerValue))
                return;

            // C) Explicit GeometryInstance expansion (nested fittings / nested families)
            try
            {
                GeometryElement ge = fi.get_Geometry(opt);
                if (ge == null) return;
                foreach (GeometryObject go in ge)
                {
                    var gi = go as GeometryInstance;
                    if (gi == null) continue;

                    GeometryElement instGe = null;
                    try { instGe = gi.GetInstanceGeometry(); } catch { }
                    if (instGe != null)
                        WalkGeometryDeep(instGe, outerToHost, fi, catName, level, props, color, output, 0);

                    if (HasMeshFor(output, fi.Id.IntegerValue))
                        return;

                    GeometryElement symGe = null;
                    try { symGe = gi.GetSymbolGeometry(); } catch { }
                    if (symGe != null)
                    {
                        Transform t = combined;
                        try
                        {
                            if (gi.Transform != null)
                                t = outerToHost.Multiply(gi.Transform);
                        }
                        catch { }
                        WalkGeometryDeep(symGe, t, fi, catName, level, props, color, output, 0);
                    }
                }
            }
            catch { }

            // D) Last resort – bounding box so the fitting is never invisible
            if (!HasMeshFor(output, fi.Id.IntegerValue))
                AddBoundingBoxMesh(fi, outerToHost, catName, level, props, color, output);
        }

        /// <summary>
        /// Recursive geometry walker that expands nested GeometryInstances
        /// (critical for complex plumbing fittings: elbows, Y, tees, reducers).
        /// </summary>
        static void WalkGeometryDeep(
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
            if (ge == null || depth > 6) return;
            foreach (GeometryObject go in ge)
            {
                if (go == null) continue;

                if (go is Solid solid)
                {
                    if (solid.Faces == null || solid.Faces.Size < 1) continue;
                    // Skip zero-volume noise
                    try { if (solid.Volume < 1e-9) continue; } catch { }
                    var rec = NewRecord(el, catName, level, props, color);
                    if (AppendSolid(solid, outer, rec))
                        output.Add(rec);
                }
                else if (go is Mesh mesh)
                {
                    if (mesh.NumTriangles < 1) continue;
                    var rec = NewRecord(el, catName, level, props, color);
                    if (AppendRevitMesh(mesh, outer, rec))
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
                        WalkGeometryDeep(nested, outer, el, catName, level, props, color, output, depth + 1);

                    try { nested = gi.GetSymbolGeometry(); } catch { nested = null; }
                    if (nested != null)
                        WalkGeometryDeep(nested, local, el, catName, level, props, color, output, depth + 1);
                }
            }
        }

        static void ExtractFromElementGeometry(
            Element el,
            Options opt,
            Transform outer,
            string catName,
            string level,
            Dictionary<string, string> props,
            Color color,
            List<MeshRecord> output)
        {
            try
            {
                GeometryElement ge = el.get_Geometry(opt);
                if (ge != null)
                    WalkGeometry(ge, outer, el, catName, level, props, color, output);
            }
            catch { }
        }

        static bool HasMeshFor(List<MeshRecord> list, int elementId)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].ElementId == elementId && list[i].Indices != null && list[i].Indices.Count >= 3)
                    return true;
            return false;
        }

        static void WalkGeometry(
            GeometryElement ge,
            Transform outer,
            Element el,
            string catName,
            string level,
            Dictionary<string, string> props,
            Color color,
            List<MeshRecord> output)
        {
            if (ge == null) return;
            foreach (GeometryObject go in ge)
            {
                if (go == null) continue;

                if (go is Solid solid)
                {
                    if (solid.Faces == null || solid.Faces.Size < 1) continue;
                    var rec = NewRecord(el, catName, level, props, color);
                    if (AppendSolid(solid, outer, rec))
                        output.Add(rec);
                }
                else if (go is Mesh mesh)
                {
                    if (mesh.NumTriangles < 1) continue;
                    var rec = NewRecord(el, catName, level, props, color);
                    if (AppendRevitMesh(mesh, outer, rec))
                        output.Add(rec);
                }
                else if (go is GeometryInstance gi)
                {
                    GeometryElement nested = null;
                    try { nested = gi.GetInstanceGeometry(); } catch { }
                    if (nested != null)
                        WalkGeometry(nested, outer, el, catName, level, props, color, output);

                    try { nested = gi.GetSymbolGeometry(); } catch { nested = null; }
                    if (nested != null)
                    {
                        Transform t = outer;
                        try
                        {
                            if (gi.Transform != null)
                                t = outer.Multiply(gi.Transform);
                        }
                        catch { }
                        WalkGeometry(nested, t, el, catName, level, props, color, output);
                    }
                }
            }
        }

        static void AddBoundingBoxMesh(
            Element el,
            Transform toHost,
            string catName,
            string level,
            Dictionary<string, string> props,
            Color color,
            List<MeshRecord> output)
        {
            BoundingBoxXYZ bb;
            try { bb = el.get_BoundingBox(null); }
            catch { return; }
            if (bb == null) return;

            XYZ mn = bb.Min;
            XYZ mx = bb.Max;
            double dx = Math.Max(mx.X - mn.X, 0.15);
            double dy = Math.Max(mx.Y - mn.Y, 0.15);
            double dz = Math.Max(mx.Z - mn.Z, 0.15);
            XYZ c = (mn + mx) * 0.5;
            mn = new XYZ(c.X - dx * 0.5, c.Y - dy * 0.5, c.Z - dz * 0.5);
            mx = new XYZ(c.X + dx * 0.5, c.Y + dy * 0.5, c.Z + dz * 0.5);

            bool hasTr = toHost != null && !toHost.IsIdentity;

            Func<double, double, double, double[]> V = (x, y, z) =>
            {
                XYZ p = new XYZ(x, y, z);
                if (hasTr) p = toHost.OfPoint(p);
                return new[] { p.X, p.Z, -p.Y };
            };

            var pts = new[]
            {
                V(mn.X, mn.Y, mn.Z), V(mx.X, mn.Y, mn.Z), V(mx.X, mx.Y, mn.Z), V(mn.X, mx.Y, mn.Z),
                V(mn.X, mn.Y, mx.Z), V(mx.X, mn.Y, mx.Z), V(mx.X, mx.Y, mx.Z), V(mn.X, mx.Y, mx.Z)
            };

            int[] quads =
            {
                0,1,2, 0,2,3,
                4,6,5, 4,7,6,
                0,4,5, 0,5,1,
                1,5,6, 1,6,2,
                2,6,7, 2,7,3,
                3,7,4, 3,4,0
            };

            var rec = NewRecord(el, catName, level, props, color);
            if (rec.Properties != null && !rec.Properties.ContainsKey("Geometry"))
                rec.Properties["Geometry"] = "BoundingBox fallback";

            for (int i = 0; i < pts.Length; i++)
            {
                rec.Positions.Add(pts[i][0]);
                rec.Positions.Add(pts[i][1]);
                rec.Positions.Add(pts[i][2]);
            }
            for (int i = 0; i < quads.Length; i++)
                rec.Indices.Add(quads[i]);

            output.Add(rec);
        }

        static MeshRecord NewRecord(
            Element el,
            string catName,
            string level,
            Dictionary<string, string> props,
            Color color)
        {
            return new MeshRecord
            {
                ElementId = el.Id.IntegerValue,
                UniqueId = el.UniqueId,
                Category = catName,
                Level = level,
                Name = el.Name ?? "",
                R = color.Red / 255f,
                G = color.Green / 255f,
                B = color.Blue / 255f,
                Opacity = 1f,
                Properties = props ?? new Dictionary<string, string>()
            };
        }

        static bool AppendSolid(Solid solid, Transform tr, MeshRecord rec)
        {
            bool any = false;
            foreach (Face face in solid.Faces)
            {
                Mesh mesh;
                try { mesh = face.Triangulate(); }
                catch { continue; }
                if (mesh == null || mesh.NumTriangles < 1) continue;
                if (AppendRevitMesh(mesh, tr, rec)) any = true;
            }
            return any;
        }

        static bool AppendRevitMesh(Mesh mesh, Transform tr, MeshRecord rec)
        {
            int n = mesh.NumTriangles;
            if (n < 1) return false;
            int baseIndex = rec.Positions.Count / 3;
            bool hasTr = tr != null && !tr.IsIdentity;

            for (int i = 0; i < n; i++)
            {
                MeshTriangle tri = mesh.get_Triangle(i);
                for (int c = 0; c < 3; c++)
                {
                    XYZ p = tri.get_Vertex(c);
                    if (hasTr) p = tr.OfPoint(p);
                    rec.Positions.Add(p.X);
                    rec.Positions.Add(p.Z);
                    rec.Positions.Add(-p.Y);
                }
                rec.Indices.Add(baseIndex + i * 3);
                rec.Indices.Add(baseIndex + i * 3 + 1);
                rec.Indices.Add(baseIndex + i * 3 + 2);
            }
            return true;
        }

        static Color ResolveColor(Element el, string catName)
        {
            try
            {
                Element sysTypeEl = null;
                try
                {
                    var p = el.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)
                         ?? el.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
                    if (p != null && p.HasValue)
                        sysTypeEl = el.Document.GetElement(p.AsElementId());
                }
                catch { }

                if (sysTypeEl != null)
                {
                    if (SystemColorMap.HasExactOverride(sysTypeEl.Name))
                    {
                        Color o = SystemColorMap.TryGetFromSystemName(sysTypeEl.Name);
                        if (o != null) return o;
                    }
                    Color fromRevit = SystemColorMap.TryGetFromSystemTypeElement(sysTypeEl);
                    if (fromRevit != null) return fromRevit;
                    Color byName = SystemColorMap.TryGetFromSystemName(sysTypeEl.Name);
                    if (byName != null) return byName;
                }

                foreach (var pname in new[] {
                    "System Classification", "System Type", "System Name",
                    "Piping System", "Duct System" })
                {
                    Parameter p = el.LookupParameter(pname);
                    if (p == null || !p.HasValue) continue;
                    string sn = p.AsValueString() ?? p.AsString();
                    if (string.IsNullOrWhiteSpace(sn)) continue;
                    Color c = SystemColorMap.TryGetFromSystemName(sn);
                    if (c != null) return c;
                }

                try
                {
                    var p = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
                    if (p != null && p.HasValue)
                    {
                        string sn = p.AsValueString() ?? p.AsString();
                        Color c = SystemColorMap.TryGetFromSystemName(sn);
                        if (c != null) return c;
                    }
                }
                catch { }
            }
            catch { }

            Color mapped = SystemColorMap.TryGet(catName);
            if (mapped != null) return mapped;

            if (catName != null)
            {
                string n = catName.ToLowerInvariant();
                if (n.Contains("duct")) return new Color(139, 90, 43);
                if (n.Contains("pipe") || n.Contains("sprinkler")) return new Color(34, 140, 60);
                if (n.Contains("cable")) return new Color(120, 130, 40);
                if (n.Contains("conduit")) return new Color(40, 130, 130);
            }
            return SystemColorMap.ColorFromHash(catName ?? "default");
        }

        static string ResolveLevel(Document doc, Element el)
        {
            try
            {
                if (el.LevelId != null && el.LevelId != ElementId.InvalidElementId)
                {
                    Level lv = doc.GetElement(el.LevelId) as Level;
                    if (lv != null) return lv.Name;
                }
                BuiltInParameter[] candidates =
                {
                    BuiltInParameter.RBS_START_LEVEL_PARAM,
                    BuiltInParameter.FAMILY_LEVEL_PARAM,
                    BuiltInParameter.SCHEDULE_LEVEL_PARAM,
                    BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM
                };
                foreach (var bip in candidates)
                {
                    try
                    {
                        Parameter p = el.get_Parameter(bip);
                        if (p == null || !p.HasValue) continue;
                        if (p.StorageType != StorageType.ElementId) continue;
                        Level lv = doc.GetElement(p.AsElementId()) as Level;
                        if (lv != null) return lv.Name;
                    }
                    catch { }
                }
            }
            catch { }
            return "Unassigned";
        }
    }
}
