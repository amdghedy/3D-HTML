using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ClearView.Export
{
    /// <summary>
    /// Assigns a unique solid colour per MEP system classification / system type.
    /// User overrides (from the System Colours settings window) take priority.
    /// </summary>
    public static class SystemColorMap
    {
        /// <summary>Active session overrides set by the export dialog (key = system name).</summary>
        public static Dictionary<string, int> Overrides { get; set; }

        // Canonical list shown in the System Colours settings window
        public static readonly string[] EditableSystems =
        {
            "Fire Protection",
            "Sprinkler",
            "Domestic Hot Water",
            "Domestic Cold Water",
            "Hydronic Supply",
            "Hydronic Return",
            "Heating Hot Water",
            "Chilled Water",
            "Condenser Water",
            "Sanitary",
            "Waste",
            "Vent",
            "Storm",
            "Drain",
            "Gas",
            "Natural Gas",
            "Medical Gas",
            "Supply Air",
            "Return Air",
            "Exhaust Air",
            "Outside Air",
            "Power",
            "Lighting",
            "Data",
            "Fire Alarm",
            "Cable Tray",
            "Conduit",
            "Other"
        };

        static readonly Dictionary<string, Color> ClassificationPalette =
            new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Fire Protection",           new Color(220, 20,  20) },
            { "FireProtection",            new Color(220, 20,  20) },
            { "Sprinkler",                 new Color(230, 40,  40) },
            { "Standpipe",                 new Color(180,  0,  30) },
            { "Fire Suppression",          new Color(200, 30,  50) },
            { "Domestic Hot Water",        new Color(220, 40, 120) },
            { "Hot Water",                 new Color(220, 40, 120) },
            { "Domestic Cold Water",       new Color(30, 100, 220) },
            { "Cold Water",                new Color(30, 100, 220) },
            { "Potable Water",             new Color(40, 120, 200) },
            { "Domestic Water",            new Color(50, 130, 190) },
            { "Hydronic Supply",           new Color(0, 160,  80) },
            { "Hydronic Return",           new Color(0, 120,  60) },
            { "Heating Hot Water",         new Color(200, 60,  20) },
            { "Chilled Water",             new Color(0, 180, 200) },
            { "Chilled Water Supply",      new Color(0, 190, 210) },
            { "Chilled Water Return",      new Color(0, 150, 170) },
            { "Condenser Water",           new Color(0, 140, 160) },
            { "Cooling Tower",             new Color(20, 130, 150) },
            { "Sanitary",                  new Color(110, 70,  40) },
            { "Waste",                     new Color(100, 65,  35) },
            { "Soil",                      new Color(90,  60,  30) },
            { "Vent",                      new Color(140, 100, 70) },
            { "Storm",                     new Color(70,  90,  50) },
            { "Rain Water",                new Color(60, 100, 80) },
            { "Drain",                     new Color(95,  70,  45) },
            { "Gas",                       new Color(140, 40, 180) },
            { "Natural Gas",               new Color(130, 30, 170) },
            { "LPG",                       new Color(150, 50, 160) },
            { "Medical Gas",               new Color(100, 60, 200) },
            { "Medical Air",               new Color(80,  80, 210) },
            { "Oxygen",                    new Color(60, 100, 220) },
            { "Vacuum",                    new Color(90,  50, 140) },
            { "Other",                     new Color(160, 160, 80) },
            { "Process",                   new Color(150, 120, 50) },
            { "Fuel Oil",                  new Color(80,  50,  20) },
            { "Grease",                    new Color(120, 90,  40) },
            { "Supply Air",                new Color(160, 100, 40) },
            { "Return Air",                new Color(130, 80,  30) },
            { "Exhaust Air",               new Color(100, 60,  20) },
            { "Outside Air",               new Color(180, 140, 60) },
            { "Relief Air",                new Color(140, 110, 50) },
            { "Air",                       new Color(150, 95,  45) },
            { "Power",                     new Color(230, 190, 20) },
            { "Lighting",                  new Color(240, 200, 40) },
            { "Data",                      new Color(180, 180, 50) },
            { "Fire Alarm",                new Color(200, 80,  20) },
            { "Security",                  new Color(100, 100, 120) },
            { "Communications",            new Color(160, 160, 40) },
            { "Telephone",                 new Color(170, 150, 30) },
            { "Nurse Call",                new Color(190, 100, 100) },
            { "Cable Tray",                new Color(120, 130, 40) },
            { "Conduit",                   new Color(40, 130, 130) },
        };

        static readonly Color[] HashPalette =
        {
            new Color(220,  50,  50), new Color( 50, 160,  50), new Color( 50,  80, 220),
            new Color(220, 140,  20), new Color(160,  40, 180), new Color( 20, 170, 170),
            new Color(200,  40, 120), new Color(100, 140,  20), new Color( 40, 100, 160),
            new Color(180, 100,  40), new Color( 80,  60, 160), new Color( 40, 140, 100),
            new Color(200,  80,  60), new Color( 60, 120, 180), new Color(140,  80,  20),
            new Color(100,  40, 120), new Color( 20, 140, 140), new Color(180,  60, 100),
            new Color(120, 160,  40), new Color( 60,  80, 180), new Color(160, 120,  30),
            new Color( 90,  50, 140), new Color( 30, 150, 110), new Color(200, 100,  40),
        };

        static readonly Color BrownDuct        = new Color(139,  90,  43);
        static readonly Color OrangeFlexDuct   = new Color(210, 120,  40);
        static readonly Color RedFire          = new Color(220,  20,  20);
        static readonly Color GreenWater       = new Color( 34, 140,  60);
        static readonly Color BlueDomesticCold = new Color( 30, 100, 220);
        static readonly Color CyanChilled      = new Color(  0, 180, 200);
        static readonly Color MagentaHotWater  = new Color(220,  40, 120);
        static readonly Color PurpleGas        = new Color(140,  40, 180);
        static readonly Color YellowElectrical = new Color(230, 190,  20);
        static readonly Color OliveCableTray   = new Color(120, 130,  40);
        static readonly Color TealConduit      = new Color( 40, 130, 130);
        static readonly Color GreyArch         = new Color(170, 170, 170);
        static readonly Color DarkStruct       = new Color( 90,  95, 100);
        static readonly Color SandFloor        = new Color(190, 175, 140);
        static readonly Color LightCeil        = new Color(210, 210, 215);

        public static int Pack(Color c) =>
            (c.Red << 16) | (c.Green << 8) | c.Blue;

        public static Color Unpack(int rgb) =>
            new Color((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));

        /// <summary>Default colour for a known editable system name (for the settings UI).</summary>
        public static Color GetDefaultColor(string systemName)
        {
            if (string.IsNullOrWhiteSpace(systemName))
                return new Color(160, 160, 160);
            if (ClassificationPalette.TryGetValue(systemName.Trim(), out var c))
                return c;
            return ColorFromHash(systemName.Trim());
        }

        /// <summary>Effective colour: user override → palette → keywords → hash.</summary>

        public static bool HasExactOverride(string systemName)
        {
            if (Overrides == null || string.IsNullOrWhiteSpace(systemName)) return false;
            return Overrides.ContainsKey(systemName.Trim());
        }

        /// <summary>
        /// Read LineColor / FillColor from a Revit MEPSystemType (Piping / Duct system type).
        /// Returns null if colour is invalid or pure black (often means "not set").
        /// </summary>
        public static Color TryGetFromSystemTypeElement(Element sysTypeEl)
        {
            if (sysTypeEl == null) return null;
            try
            {
                var mep = sysTypeEl as MEPSystemType;
                if (mep != null)
                {
                    Color line = null;
                    Color fill = null;
                    try { line = mep.LineColor; } catch { }
                    try { fill = mep.FillColor; } catch { }

                    if (IsUsable(line)) return line;
                    if (IsUsable(fill)) return fill;
                }
            }
            catch { }

            // Fallback: graphic override parameters if present
            try
            {
                foreach (var pname in new[] { "Line Color", "Color", "System Color" })
                {
                    var p = sysTypeEl.LookupParameter(pname);
                    if (p == null || !p.HasValue) continue;
                    // Some builds store colour as integer
                    try
                    {
                        int v = p.AsInteger();
                        if (v != 0)
                        {
                            byte r = (byte)(v & 0xFF);
                            byte g = (byte)((v >> 8) & 0xFF);
                            byte b = (byte)((v >> 16) & 0xFF);
                            var c = new Color(r, g, b);
                            if (IsUsable(c)) return c;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        static bool IsUsable(Color c)
        {
            if (c == null) return false;
            // Revit often returns black when no override is set
            if (c.Red == 0 && c.Green == 0 && c.Blue == 0) return false;
            return true;
        }

        /// <summary>
        /// Scan host + links for all piping/duct system types and return name → packed RGB.
        /// Uses Revit LineColor/FillColor when available, otherwise built-in defaults.
        /// </summary>
        public static Dictionary<string, int> DetectFromDocument(Document doc)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Seed editable defaults
            foreach (var name in EditableSystems)
                map[name] = Pack(GetDefaultColor(name));

            void Scan(Document d)
            {
                if (d == null) return;
                try
                {
                    foreach (Element e in new FilteredElementCollector(d).OfClass(typeof(MEPSystemType)))
                    {
                        if (e == null || string.IsNullOrWhiteSpace(e.Name)) continue;
                        var revitCol = TryGetFromSystemTypeElement(e);
                        if (revitCol != null)
                            map[e.Name] = Pack(revitCol);
                        else if (!map.ContainsKey(e.Name))
                            map[e.Name] = Pack(GetDefaultColor(e.Name));
                    }
                }
                catch { }
            }

            Scan(doc);
            try
            {
                foreach (RevitLinkInstance li in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)))
                {
                    Document ld = null;
                    try { ld = li.GetLinkDocument(); } catch { }
                    Scan(ld);
                }
            }
            catch { }

            return map;
        }

        public static Color TryGetFromSystemName(string systemName)
        {
            if (string.IsNullOrWhiteSpace(systemName))
                return null;

            string n = systemName.Trim();

            // 1) Exact user override
            if (Overrides != null)
            {
                if (Overrides.TryGetValue(n, out int packed))
                    return Unpack(packed);
                // Partial match on override keys
                foreach (var kv in Overrides)
                {
                    if (n.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        kv.Key.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                        return Unpack(kv.Value);
                }
            }

            // 2) Built-in palette
            foreach (var kv in ClassificationPalette)
            {
                if (n.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kv.Value;
            }

            // 3) Keywords
            string lower = n.ToLowerInvariant();
            if (ContainsAny(lower, "fire", "sprinkler", "standpipe"))
                return RedFire;
            if (ContainsAny(lower, "domestic hot", "hot water", "dhw", "hws"))
                return MagentaHotWater;
            if (ContainsAny(lower, "domestic cold", "cold water", "potable", "cws", "dcw"))
                return BlueDomesticCold;
            if (ContainsAny(lower, "chilled", "chw"))
                return CyanChilled;
            if (ContainsAny(lower, "condenser", "cooling tower"))
                return CyanChilled;
            if (ContainsAny(lower, "hydronic", "heating hot", "hhw"))
                return GreenWater;
            if (ContainsAny(lower, "sanitary", "waste", "soil", "sewer", "drain"))
                return new Color(110, 70, 40);
            if (ContainsAny(lower, "storm", "rain"))
                return new Color(70, 90, 50);
            if (ContainsAny(lower, "vent"))
                return new Color(140, 100, 70);
            if (ContainsAny(lower, "gas", "lpg", "medical gas", "oxygen", "vacuum"))
                return PurpleGas;
            if (ContainsAny(lower, "supply air", "return air", "exhaust", "outside air", "relief"))
                return BrownDuct;
            if (ContainsAny(lower, "power", "lighting", "electrical"))
                return YellowElectrical;
            if (ContainsAny(lower, "cable tray", "cabletray"))
                return OliveCableTray;
            if (ContainsAny(lower, "conduit"))
                return TealConduit;

            return ColorFromHash(n);
        }

        public static Color TryGet(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                return null;

            string n = categoryName.Trim().ToLowerInvariant();

            if (ContainsAny(n, "fire", "sprinkler", "standpipe", "hydrant", "fire protection", "firefighting"))
                return RedFire;
            if (ContainsAny(n, "duct", "air terminal", "vav", "ahu"))
            {
                if (ContainsAny(n, "flex")) return OrangeFlexDuct;
                return BrownDuct;
            }
            if (ContainsAny(n, "pipe", "plumbing"))
            {
                if (ContainsAny(n, "fire", "sprinkler")) return RedFire;
                if (ContainsAny(n, "hot", "return hot", "hw ")) return MagentaHotWater;
                if (ContainsAny(n, "cold", "domestic", "potable", "cw ")) return BlueDomesticCold;
                if (ContainsAny(n, "chilled", "chw", "chiller")) return CyanChilled;
                if (ContainsAny(n, "condenser", "cooling tower")) return CyanChilled;
                if (ContainsAny(n, "gas", "natural gas", "lpg", "medical gas")) return PurpleGas;
                if (ContainsAny(n, "drain", "waste", "sewer", "sanitary", "storm", "rain"))
                    return new Color(110, 70, 40);
                return GreenWater;
            }
            if (ContainsAny(n, "cable tray", "cabletray")) return OliveCableTray;
            if (ContainsAny(n, "conduit")) return TealConduit;
            if (ContainsAny(n, "electrical", "lighting", "data device", "fire alarm",
                            "security device", "nurse call", "telephone"))
                return YellowElectrical;
            if (ContainsAny(n, "wall")) return GreyArch;
            if (ContainsAny(n, "floor", "slab")) return SandFloor;
            if (ContainsAny(n, "ceiling", "roof")) return LightCeil;
            if (ContainsAny(n, "structural column", "structural framing",
                            "structural foundation", "stair", "railing"))
                return DarkStruct;
            return null;
        }

        public static Color ColorFromHash(string key)
        {
            if (string.IsNullOrEmpty(key))
                return new Color(160, 160, 160);
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char c in key.ToLowerInvariant())
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                int idx = Math.Abs(hash) % HashPalette.Length;
                return HashPalette[idx];
            }
        }

        static bool ContainsAny(string hay, params string[] needles)
        {
            foreach (var n in needles)
            {
                if (hay.IndexOf(n, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }
    }
}
