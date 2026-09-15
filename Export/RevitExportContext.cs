using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ClearView.Export
{
    /// <summary>
    /// Tessellates the current 3D view.
    /// Robust linked-model support (Case 1) + Section Box respect (Case 2).
    /// Element resolution searches ALL open documents (host + every loaded link).
    /// </summary>
    public class RevitExportContext : IExportContext
    {
        private readonly Document _hostDoc;
        private readonly View3D _view;
        private readonly ExportSettings _settings;

        private readonly Stack<Transform> _transformStack = new Stack<Transform>();
        private readonly Stack<ElementId> _elementStack = new Stack<ElementId>();

        private Color _currentColor = new Color(180, 180, 180);
        private double _currentTransparency;
        private int _processed;
        private int _skippedSmall;
        private int _skippedFilter;
        private readonly Dictionary<string, Dictionary<string, string>> _propCache =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private readonly BoundingBoxXYZ _sectionBoxHost;
        private readonly bool _hasSectionBox;

        public List<MeshRecord> Meshes { get; } = new List<MeshRecord>();
        public int TriangleCount { get; private set; }
        public bool CancelRequested { get; set; }
        public int ProcessedElements => _processed;
        public int SkippedSmall => _skippedSmall;
        public string StatusMessage { get; set; } = "Starting…";

        public event Action ProgressChanged;

        public RevitExportContext(Document doc, View3D view, ExportSettings settings)
        {
            _hostDoc = doc;
            _view = view;
            _settings = settings;

            _transformStack.Push(Transform.Identity);

            _hasSectionBox = false;
            _sectionBoxHost = null;
            try
            {
                if (view.IsSectionBoxActive)
                {
                    var sb = view.GetSectionBox();
                    if (sb != null)
                    {
                        _sectionBoxHost = sb;
                        _hasSectionBox = true;
                    }
                }
            }
            catch { }
        }

        public bool Start()
        {
            StatusMessage = "Export started";
            RaiseProgress();
            return true;
        }

        public void Finish()
        {
            StatusMessage = CancelRequested
                ? "Cancelled"
                : "Finished · meshes " + Meshes.Count + " · skipped small " + _skippedSmall;
            RaiseProgress();
        }

        public bool IsCanceled() => CancelRequested;

        public RenderNodeAction OnViewBegin(ViewNode node) => RenderNodeAction.Proceed;
        public void OnViewEnd(ElementId elementId) { }
        public void OnRPC(RPCNode node) { }
        public void OnLight(LightNode node) { }

        public void OnMaterial(MaterialNode node)
        {
            if (!_settings.UseElementColors) return;
            try
            {
                _currentColor = node.Color;
                _currentTransparency = Math.Max(0, Math.Min(100, node.Transparency)) / 100.0;
            }
            catch
            {
                _currentColor = new Color(180, 180, 180);
                _currentTransparency = 0;
            }
        }

        /// <summary>
        /// Critical: ElementIds that belong to linked documents return null from host.GetElement.
        /// We therefore search every open document (host + all loaded links).
        /// </summary>
        Element ResolveElement(ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId) return null;

            // Fast path – host
            try
            {
                var el = _hostDoc.GetElement(id);
                if (el != null) return el;
            }
            catch { }

            // Search every open document (includes all loaded RVT links)
            try
            {
                foreach (Document d in _hostDoc.Application.Documents)
                {
                    if (d == null || d == _hostDoc) continue;
                    try
                    {
                        var el = d.GetElement(id);
                        if (el != null) return el;
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            if (CancelRequested) return RenderNodeAction.Skip;

            if (_settings.MaxElements > 0 && _processed >= _settings.MaxElements)
            {
                CancelRequested = true;
                StatusMessage = "Max element cap reached (" + _settings.MaxElements + ")";
                RaiseProgress();
                return RenderNodeAction.Skip;
            }

            _elementStack.Push(elementId);
            _processed++;
            if (_processed % 100 == 0)
            {
                StatusMessage = "Elements " + _processed + " · tris " + TriangleCount;
                RaiseProgress();
            }

            Element el = ResolveElement(elementId);

            // Linked-file elements sometimes resolve to null here but still have
            // valid geometry nodes downstream — always Proceed so CustomExporter
            // can emit their solids (especially pipes / trays in links).
            if (el == null)
                return RenderNodeAction.Proceed;

            var cat = el.Category;
            if (cat == null)
                return RenderNodeAction.Proceed;

            bool isMep = MepCategories.IsMepElement(el);

            if (!isMep && cat.CategoryType != CategoryType.Model)
            {
                _skippedFilter++;
                return RenderNodeAction.Skip;
            }

            if (!_settings.IsCategoryIncluded(cat.Name))
            {
                if (!isMep)
                {
                    _skippedFilter++;
                    return RenderNodeAction.Skip;
                }
            }

            // Size cull never applies to MEP (pipes / trays / ducts / fittings)
            if (!isMep && _settings.MinElementSizeFt > 0)
            {
                try
                {
                    var box = el.get_BoundingBox(_view);
                    if (box != null)
                    {
                        var d = box.Max - box.Min;
                        double diag = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
                        if (diag < _settings.MinElementSizeFt)
                        {
                            _skippedSmall++;
                            return RenderNodeAction.Skip;
                        }
                    }
                }
                catch { }
            }

            // Section-box pre-cull is unreliable for long thin pipes / trays —
            // skip it for all MEP and let geometry + fallback decide.
            if (!isMep && _hasSectionBox && !IsElementPossiblyInsideSectionBox(el, _transformStack.Peek()))
            {
                _skippedFilter++;
                return RenderNodeAction.Skip;
            }

            ApplyElementViewColor(el);
            return RenderNodeAction.Proceed;
        }

        public void OnElementEnd(ElementId elementId)
        {
            if (_elementStack.Count > 0)
                _elementStack.Pop();
        }

        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            if (CancelRequested) return RenderNodeAction.Skip;
            try
            {
                Transform t = node.GetTransform();
                _transformStack.Push(_transformStack.Peek().Multiply(t ?? Transform.Identity));
            }
            catch
            {
                _transformStack.Push(_transformStack.Peek());
            }
            return RenderNodeAction.Proceed;
        }

        public void OnInstanceEnd(InstanceNode node)
        {
            if (_transformStack.Count > 1)
                _transformStack.Pop();
        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            if (!_settings.IncludeLinks || CancelRequested)
                return RenderNodeAction.Skip;

            try
            {
                // Push the link placement transform. Geometry that follows will be
                // transformed by the accumulated stack in OnPolymesh.
                Transform linkTr = node.GetTransform() ?? Transform.Identity;
                _transformStack.Push(_transformStack.Peek().Multiply(linkTr));
                return RenderNodeAction.Proceed;
            }
            catch
            {
                return RenderNodeAction.Skip;
            }
        }

        public void OnLinkEnd(LinkNode node)
        {
            if (_transformStack.Count > 1)
                _transformStack.Pop();
        }

        public RenderNodeAction OnFaceBegin(FaceNode node) => RenderNodeAction.Proceed;
        public void OnFaceEnd(FaceNode node) { }

        public void OnPolymesh(PolymeshTopology mesh)
        {
            if (CancelRequested || _elementStack.Count == 0) return;

            ElementId eid = _elementStack.Peek();
            Element el = ResolveElement(eid);
            if (el == null) return;

            Document elDoc = el.Document;

            var level = ResolveLevel(el, elDoc);
            if (!_settings.IsLevelIncluded(level) && level != "Unassigned") return;

            var catName = el.Category?.Name ?? "Uncategorized";

            var col = _currentColor;
            var systemCol = TryResolveSystemColor(el, catName, elDoc);
            if (systemCol != null)
            {
                col = systemCol;
            }
            else if (_settings.UseElementColors)
            {
                var overrideCol = TryGetViewOverrideColor(el);
                if (overrideCol != null && IsNearGrey(col))
                    col = overrideCol;
            }

            string cacheKey = el.UniqueId ?? (elDoc.Title + "|" + eid.IntegerValue);
            if (!_propCache.TryGetValue(cacheKey, out var props))
            {
                props = PropertyCollector.Collect(el);
                _propCache[cacheKey] = props;
            }

            var rec = new MeshRecord
            {
                ElementId = eid.IntegerValue,
                UniqueId = el.UniqueId,
                Category = catName,
                Level = level,
                Name = el.Name ?? "",
                R = col.Red / 255f,
                G = col.Green / 255f,
                B = col.Blue / 255f,
                Opacity = (float)(1.0 - _currentTransparency),
                Properties = props
            };

            Transform tr = _transformStack.Peek();
            bool hasTr = tr != null && !tr.IsIdentity;

            int nPts = mesh.NumberOfPoints;
            if (rec.Positions.Capacity < nPts * 3)
                rec.Positions.Capacity = nPts * 3;

            foreach (var p in mesh.GetPoints())
            {
                XYZ pt = p;
                if (hasTr) pt = tr.OfPoint(p);

                // Revit Z-up → glTF Y-up
                rec.Positions.Add(pt.X);
                rec.Positions.Add(pt.Z);
                rec.Positions.Add(-pt.Y);
            }

            foreach (var f in mesh.GetFacets())
            {
                rec.Indices.Add(f.V1);
                rec.Indices.Add(f.V2);
                rec.Indices.Add(f.V3);
            }

            TriangleCount += mesh.NumberOfFacets;
            Meshes.Add(rec);
        }

        // ------------------------------------------------------------------
        bool IsElementPossiblyInsideSectionBox(Element el, Transform elementToHost)
        {
            if (!_hasSectionBox || _sectionBoxHost == null) return true;
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

                bool hasTr = elementToHost != null && !elementToHost.IsIdentity;
                for (int i = 0; i < 8; i++)
                    if (hasTr) corners[i] = elementToHost.OfPoint(corners[i]);

                Transform sbTr = _sectionBoxHost.Transform ?? Transform.Identity;
                XYZ sbMin = _sectionBoxHost.Min;
                XYZ sbMax = _sectionBoxHost.Max;

                XYZ[] sbCorners = new XYZ[8];
                sbCorners[0] = sbTr.OfPoint(new XYZ(sbMin.X, sbMin.Y, sbMin.Z));
                sbCorners[1] = sbTr.OfPoint(new XYZ(sbMax.X, sbMin.Y, sbMin.Z));
                sbCorners[2] = sbTr.OfPoint(new XYZ(sbMax.X, sbMax.Y, sbMin.Z));
                sbCorners[3] = sbTr.OfPoint(new XYZ(sbMin.X, sbMax.Y, sbMin.Z));
                sbCorners[4] = sbTr.OfPoint(new XYZ(sbMin.X, sbMin.Y, sbMax.Z));
                sbCorners[5] = sbTr.OfPoint(new XYZ(sbMax.X, sbMin.Y, sbMax.Z));
                sbCorners[6] = sbTr.OfPoint(new XYZ(sbMax.X, sbMax.Y, sbMax.Z));
                sbCorners[7] = sbTr.OfPoint(new XYZ(sbMin.X, sbMax.Y, sbMax.Z));

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

        void ApplyElementViewColor(Element el)
        {
            if (!_settings.UseElementColors)
            {
                _currentColor = new Color(180, 180, 180);
                _currentTransparency = 0;
                return;
            }

            var ovr = TryGetViewOverrideColor(el);
            if (ovr != null)
            {
                _currentColor = ovr;
                _currentTransparency = 0;
                return;
            }

            try
            {
                if (el.Category != null)
                {
                    var matId = el.Category.Material?.Id ?? ElementId.InvalidElementId;
                    if (matId != ElementId.InvalidElementId)
                    {
                        var mat = _hostDoc.GetElement(matId) as Material;
                        if (mat != null)
                        {
                            _currentColor = mat.Color;
                            _currentTransparency = mat.Transparency / 100.0;
                            return;
                        }
                    }
                    var line = el.Category.LineColor;
                    if (line != null)
                    {
                        _currentColor = line;
                        _currentTransparency = 0;
                        return;
                    }
                }
            }
            catch { }

            _currentColor = new Color(180, 180, 180);
            _currentTransparency = 0;
        }

        Color TryGetViewOverrideColor(Element el)
        {
            try
            {
                var ogs = _view.GetElementOverrides(el.Id);
                try
                {
                    Color c = ogs.ProjectionLineColor;
                    if (c != null && IsUsableColor(c)) return c;
                }
                catch { }
                try
                {
                    Color c = ogs.CutLineColor;
                    if (c != null && IsUsableColor(c)) return c;
                }
                catch { }
            }
            catch { }
            return null;
        }

        static bool IsUsableColor(Color c)
        {
            if (c == null) return false;
            return c.Red > 0 || c.Green > 0 || c.Blue > 0;
        }

        static bool IsNearGrey(Color c)
        {
            int r = c.Red, g = c.Green, b = c.Blue;
            return Math.Abs(r - g) < 12 && Math.Abs(g - b) < 12 && r >= 160 && r <= 200;
        }

        Color TryResolveSystemColor(Element el, string catName, Document doc)
        {
            try
            {
                // Priority 1: Read colour directly from the MEP system type element
                // (Revit LineColor / FillColor as set in Type Properties → Graphic Overrides)
                Element sysTypeEl = null;
                try
                {
                    var p = el.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)
                         ?? el.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
                    if (p != null && p.HasValue)
                        sysTypeEl = doc.GetElement(p.AsElementId());
                }
                catch { }

                if (sysTypeEl != null)
                {
                    // User override by system type name takes precedence
                    var byName = SystemColorMap.TryGetFromSystemName(sysTypeEl.Name);
                    // Prefer actual Revit graphic colour when no explicit user override matched exactly
                    var fromRevit = SystemColorMap.TryGetFromSystemTypeElement(sysTypeEl);
                    if (fromRevit != null)
                    {
                        // If user overrode this exact name, TryGetFromSystemName already applied it
                        if (SystemColorMap.HasExactOverride(sysTypeEl.Name))
                            return byName;
                        return fromRevit;
                    }
                    if (byName != null) return byName;

                    try
                    {
                        var cls = sysTypeEl.LookupParameter("System Classification")
                               ?? sysTypeEl.LookupParameter("Classification");
                        if (cls != null && cls.HasValue)
                        {
                            string cn = cls.AsValueString() ?? cls.AsString();
                            var cc = SystemColorMap.TryGetFromSystemName(cn);
                            if (cc != null) return cc;
                        }
                    }
                    catch { }
                }

                // Priority 2: String parameters on the element
                foreach (var pname in new[] {
                    "System Classification", "System Type", "System Name",
                    "Piping System", "Duct System" })
                {
                    var p = el.LookupParameter(pname);
                    if (p == null || !p.HasValue) continue;
                    string sn = p.AsValueString() ?? p.AsString();
                    if (string.IsNullOrWhiteSpace(sn)) continue;
                    var c = SystemColorMap.TryGetFromSystemName(sn);
                    if (c != null) return c;
                }

                // Priority 3: RBS system classification built-in
                try
                {
                    var p = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
                    if (p != null && p.HasValue)
                    {
                        string sn = p.AsValueString() ?? p.AsString();
                        var c = SystemColorMap.TryGetFromSystemName(sn);
                        if (c != null) return c;
                    }
                }
                catch { }
            }
            catch { }

            return SystemColorMap.TryGet(catName);
        }

        string ResolveLevel(Element el, Document doc)
        {
            try
            {
                if (el.LevelId != null && el.LevelId != ElementId.InvalidElementId)
                {
                    var lv = doc.GetElement(el.LevelId) as Level;
                    if (lv != null) return lv.Name;
                }

                BuiltInParameter[] candidates =
                {
                    BuiltInParameter.FAMILY_LEVEL_PARAM,
                    BuiltInParameter.SCHEDULE_LEVEL_PARAM,
                    BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                    BuiltInParameter.RBS_START_LEVEL_PARAM,
                };

                foreach (var bip in candidates)
                {
                    try
                    {
                        var p = el.get_Parameter(bip);
                        if (p == null || !p.HasValue) continue;
                        if (p.StorageType != StorageType.ElementId) continue;
                        var lv = doc.GetElement(p.AsElementId()) as Level;
                        if (lv != null) return lv.Name;
                    }
                    catch { }
                }

                foreach (var pname in new[] { "Reference Level", "Level", "Schedule Level", "Base Level" })
                {
                    var p = el.LookupParameter(pname);
                    if (p == null || !p.HasValue) continue;
                    if (p.StorageType != StorageType.ElementId) continue;
                    var lv = doc.GetElement(p.AsElementId()) as Level;
                    if (lv != null) return lv.Name;
                }
            }
            catch { }
            return "Unassigned";
        }

        void RaiseProgress()
        {
            try
            {
                ProgressChanged?.Invoke();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => { }));
            }
            catch { }
        }
    }
}
