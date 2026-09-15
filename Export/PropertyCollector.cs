using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;

namespace ClearView.Export
{
    /// <summary>
    /// Collects instance (+ type) parameters for the HTML properties panel.
    /// Caps count so large models do not explode the HTML size.
    /// </summary>
    public static class PropertyCollector
    {
        const int MaxParams = 40;

        public static Dictionary<string, string> Collect(Element el)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void Add(string key, string val)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(val)) return;
                key = key.Trim();
                if (key.StartsWith("EXT_", StringComparison.OrdinalIgnoreCase)) return;
                if (map.Count >= MaxParams) return;
                if (!map.ContainsKey(key))
                    map[key] = val.Trim();
            }

            try
            {
                Add("Name", el.Name);
                Add("Element Id", el.Id.IntegerValue.ToString(CultureInfo.InvariantCulture));
                Add("Unique Id", el.UniqueId);
                if (el.Category != null)
                    Add("Category", el.Category.Name);

                // Instance parameters first
                foreach (Parameter p in el.Parameters)
                    TryAddParam(p, Add);

                // Type parameters
                var typeId = el.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    var type = el.Document.GetElement(typeId);
                    if (type != null)
                    {
                        Add("Type Name", type.Name);
                        foreach (Parameter p in type.Parameters)
                            TryAddParam(p, Add);
                    }
                }
            }
            catch { }

            return map;
        }

        static void TryAddParam(Parameter p, Action<string, string> add)
        {
            try
            {
                if (p == null || !p.HasValue) return;
                if (p.IsReadOnly && p.StorageType == StorageType.None) return;
                string name = p.Definition?.Name;
                if (string.IsNullOrEmpty(name)) return;

                string val = null;
                switch (p.StorageType)
                {
                    case StorageType.String:
                        val = p.AsString();
                        if (string.IsNullOrEmpty(val)) val = p.AsValueString();
                        break;
                    case StorageType.Integer:
                        val = p.AsValueString() ?? p.AsInteger().ToString(CultureInfo.InvariantCulture);
                        break;
                    case StorageType.Double:
                        val = p.AsValueString() ?? p.AsDouble().ToString("0.###", CultureInfo.InvariantCulture);
                        break;
                    case StorageType.ElementId:
                        var id = p.AsElementId();
                        if (id == null || id == ElementId.InvalidElementId) return;
                        var refEl = p.Element?.Document?.GetElement(id);
                        val = refEl != null ? refEl.Name : id.IntegerValue.ToString(CultureInfo.InvariantCulture);
                        break;
                }
                if (!string.IsNullOrWhiteSpace(val) && val != "null")
                    add(name, val);
            }
            catch { }
        }
    }
}
