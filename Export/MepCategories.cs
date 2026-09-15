using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace ClearView.Export
{
    /// <summary>
    /// Canonical MEP category set so pipes, fittings, ducts, trays, etc. are never missed.
    /// </summary>
    public static class MepCategories
    {
        public static readonly BuiltInCategory[] BuiltIn =
        {
            // Ducts
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_DuctTerminal,
            BuiltInCategory.OST_DuctInsulations,
            BuiltInCategory.OST_DuctLinings,
            BuiltInCategory.OST_PlaceHolderDucts,
            // Pipes
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_FlexPipeCurves,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_PipeInsulations,
            BuiltInCategory.OST_PlaceHolderPipes,
            BuiltInCategory.OST_Sprinklers,
            BuiltInCategory.OST_PlumbingFixtures,
            // Electrical raceway
            BuiltInCategory.OST_CableTray,
            BuiltInCategory.OST_CableTrayFitting,
            BuiltInCategory.OST_Conduit,
            BuiltInCategory.OST_ConduitFitting,
            // Equipment / devices often needed with systems
            BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_ElectricalEquipment,
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_DataDevices,
            BuiltInCategory.OST_FireAlarmDevices,
            BuiltInCategory.OST_SecurityDevices,
            BuiltInCategory.OST_TelephoneDevices,
            BuiltInCategory.OST_CommunicationDevices,
            BuiltInCategory.OST_NurseCallDevices,
        };

        public static readonly string[] NameHints =
        {
            "Duct", "Pipe", "Cable Tray", "Conduit", "Sprinkler",
            "Air Terminal", "Flex Duct", "Flex Pipe",
            "Mechanical Equipment", "Plumbing Fixture",
            "Electrical", "Lighting", "Fire Alarm", "Data Device"
        };

        public static HashSet<string> ResolveNames(Document doc)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var bic in BuiltIn)
            {
                try
                {
                    var cat = Category.GetCategory(doc, bic);
                    if (cat != null && !string.IsNullOrWhiteSpace(cat.Name))
                        set.Add(cat.Name);
                }
                catch { /* category not in this discipline template */ }
            }
            return set;
        }

        public static bool IsMepName(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return false;
            string n = categoryName.Trim();
            foreach (var h in NameHints)
            {
                if (n.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        public static bool IsMepElement(Element el)
        {
            if (el?.Category == null) return false;
            try
            {
                int id = el.Category.Id.IntegerValue;
                foreach (var bic in BuiltIn)
                {
                    if (id == (int)bic) return true;
                }
            }
            catch { }
            return IsMepName(el.Category.Name);
        }
    }
}
