using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace ClearView.Export
{
    public class ExportSettings
    {
        public string ProjectName { get; set; }
        public string ProjectNumber { get; set; }
        public string ViewName { get; set; }
        public string Brand { get; set; } = "3D HTML Viewer";
        public string OutputPath { get; set; }
        public bool IncludeLinks { get; set; } = true;   // default ON – critical for linked models
        public bool UseElementColors { get; set; } = true;
        /// <summary>2 = coarse … 16 = fine. Lower = fewer triangles (better for large models).</summary>
        public int Tessellation { get; set; } = 13;
        /// <summary>Merge meshes that share level + category + colour (required for 5k+ elements).</summary>
        public bool MergeMeshes { get; set; } = false;
        /// <summary>Skip elements whose bbox diagonal is below this (feet). 0 = off.</summary>
        public double MinElementSizeFt { get; set; } = 0.001;
        /// <summary>Soft cap on source elements processed; 0 = unlimited.</summary>
        public int MaxElements { get; set; } = 0;
        /// <summary>Enable measure + section tools in the HTML viewer.</summary>
        public bool EnableViewerTools { get; set; } = true;

        public HashSet<string> SelectedCategories { get; set; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> SelectedLevels { get; set; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public List<string> AvailableLevels { get; set; } = new List<string>();
        public List<string> AvailableCategories { get; set; } = new List<string>();

        /// <summary>
        /// User-chosen colour per system classification name (key = classification / system type).
        /// Values are packed as 0xRRGGBB (int). Empty = use built-in defaults.
        /// </summary>
        public Dictionary<string, int> SystemColorOverrides { get; set; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public bool IsLevelIncluded(string name) =>
            SelectedLevels.Count == 0 || SelectedLevels.Contains(name);

        public bool IsCategoryIncluded(string name) =>
            SelectedCategories.Count == 0 || SelectedCategories.Contains(name);

        // Core categories that must always be offered / pre-selected for a useful 3D package
        static readonly string[] CoreModelCategories =
        {
            "Walls", "Floors", "Roofs", "Ceilings", "Doors", "Windows",
            "Stairs", "Railings", "Ramps", "Columns", "Structural Columns",
            "Structural Framing", "Structural Foundations", "Foundation Slab",
            "Curtain Panels", "Curtain Wall Mullions", "Curtain Systems",
            "Generic Models", "Furniture", "Casework", "Specialty Equipment",
            "Parking", "Site", "Topography", "Pads", "Planting",
            "Mass", "Entourage", "Shaft Openings", "Areas",
            // MEP (also covered by MepCategories, but keep explicit)
            "Ducts", "Flex Ducts", "Duct Fittings", "Duct Accessories", "Air Terminals",
            "Pipes", "Flex Pipes", "Pipe Fittings", "Pipe Accessories", "Plumbing Fixtures",
            "Cable Trays", "Cable Tray Fittings", "Conduits", "Conduit Fittings",
            "Mechanical Equipment", "Electrical Equipment", "Electrical Fixtures",
            "Lighting Fixtures", "Data Devices", "Fire Alarm Devices", "Security Devices",
            "Sprinklers", "Communication Devices", "Telephone Devices",
            "Gutters", "Fascias", "Roof Soffits"
        };

        public static ExportSettings FromDocument(Document doc, View3D view)
        {
            var s = new ExportSettings
            {
                ProjectName = doc.ProjectInformation?.Name ?? doc.Title,
                ProjectNumber = doc.ProjectInformation?.Number ?? "",
                ViewName = view.Name,
                OutputPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Sanitize(doc.Title) + "-3D-HTML-Viewer.html"),
                IncludeLinks = true
            };

            int approx = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .GetElementCount();
            if (approx > 5000)
            {
                s.Tessellation = 13;
                s.MergeMeshes = false;
                s.MinElementSizeFt = 0.001;
            }

            // ---- Levels: host + every loaded link ----
            CollectLevels(doc, s.AvailableLevels);
            foreach (RevitLinkInstance li in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)))
            {
                Document linkDoc = null;
                try { linkDoc = li.GetLinkDocument(); } catch { }
                if (linkDoc != null)
                    CollectLevels(linkDoc, s.AvailableLevels);
            }
            if (!s.AvailableLevels.Any(n => n.Equals("Unassigned", StringComparison.OrdinalIgnoreCase)))
                s.AvailableLevels.Add("Unassigned");

            s.AvailableLevels = s.AvailableLevels
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var n in s.AvailableLevels)
                s.SelectedLevels.Add(n);

            // ---- Categories: host + links + core list + MEP ----
            var catSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // From host
            foreach (var n in CollectAllModelCategories(doc))
                catSet.Add(n);

            // From every loaded link
            foreach (RevitLinkInstance li in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)))
            {
                Document linkDoc = null;
                try { linkDoc = li.GetLinkDocument(); } catch { }
                if (linkDoc == null) continue;
                foreach (var n in CollectAllModelCategories(linkDoc))
                    catSet.Add(n);
            }

            // Force-add every core architectural / structural / MEP name
            foreach (var n in CoreModelCategories)
                catSet.Add(n);

            // Force-add resolved MEP names from host (and links if possible)
            foreach (var n in MepCategories.ResolveNames(doc))
                catSet.Add(n);

            s.AvailableCategories = catSet
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // ---- Pre-select: everything that is useful ----
            // 1) Categories that actually appear in the host view
            var inView = CategoriesPresentInView(doc, view);
            foreach (var n in inView)
                s.SelectedCategories.Add(n);

            // 2) Every core category
            foreach (var n in CoreModelCategories)
                s.SelectedCategories.Add(n);

            // 3) Every MEP category
            foreach (var n in MepCategories.ResolveNames(doc))
                s.SelectedCategories.Add(n);
            foreach (var n in s.AvailableCategories)
            {
                if (MepCategories.IsMepName(n))
                    s.SelectedCategories.Add(n);
            }

            // 4) Everything that was discovered (so the user sees a full list and most are checked)
            //    For very large models the UI can still uncheck, but default is inclusive.
            foreach (var n in s.AvailableCategories)
                s.SelectedCategories.Add(n);

            // Auto-detect system classification colours from host + links
            try
            {
                s.SystemColorOverrides = SystemColorMap.DetectFromDocument(doc);
            }
            catch
            {
                s.SystemColorOverrides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            return s;
        }

        static void CollectLevels(Document doc, List<string> target)
        {
            try
            {
                foreach (Level lv in new FilteredElementCollector(doc).OfClass(typeof(Level)))
                {
                    if (!string.IsNullOrWhiteSpace(lv.Name))
                        target.Add(lv.Name);
                }
            }
            catch { }
        }

        /// <summary>
        /// Every model category registered in the document (built-in + subcategory parents).
        /// </summary>
        public static List<string> CollectAllModelCategories(Document doc)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (Category cat in doc.Settings.Categories)
                {
                    if (cat == null) continue;
                    if (cat.CategoryType != CategoryType.Model) continue;
                    if (string.IsNullOrWhiteSpace(cat.Name)) continue;
                    if (IsExcludedCategoryName(cat.Name)) continue;

                    names.Add(cat.Name);

                    try
                    {
                        if (cat.SubCategories != null)
                        {
                            foreach (Category sub in cat.SubCategories)
                            {
                                if (sub == null || string.IsNullOrWhiteSpace(sub.Name)) continue;
                                if (IsExcludedCategoryName(sub.Name)) continue;
                                names.Add(sub.Name);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // Also fold in any model category that appears on placed elements
            try
            {
                foreach (Element e in new FilteredElementCollector(doc)
                             .WhereElementIsNotElementType())
                {
                    var c = e.Category;
                    if (c == null) continue;
                    if (c.CategoryType != CategoryType.Model) continue;
                    if (string.IsNullOrWhiteSpace(c.Name)) continue;
                    if (IsExcludedCategoryName(c.Name)) continue;
                    names.Add(c.Name);
                }
            }
            catch { }

            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static HashSet<string> CategoriesPresentInView(Document doc, View view)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (Element e in new FilteredElementCollector(doc, view.Id)
                             .WhereElementIsNotElementType())
                {
                    var c = e.Category;
                    if (c == null || c.CategoryType != CategoryType.Model) continue;
                    if (string.IsNullOrWhiteSpace(c.Name)) continue;
                    set.Add(c.Name);
                }
            }
            catch { }
            return set;
        }

        static bool IsExcludedCategoryName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            string n = name.Trim();
            if (n.Equals("Cameras", StringComparison.OrdinalIgnoreCase)) return true;
            if (n.Equals("Views", StringComparison.OrdinalIgnoreCase)) return true;
            if (n.StartsWith("<", StringComparison.Ordinal)) return true;
            return false;
        }

        static string Sanitize(string n)
        {
            if (string.IsNullOrEmpty(n)) return "Model";
            foreach (var c in Path.GetInvalidFileNameChars())
                n = n.Replace(c, '-');
            return n;
        }
    }
}
