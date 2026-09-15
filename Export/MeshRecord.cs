using System.Collections.Generic;

namespace ClearView.Export
{
    public class MeshRecord
    {
        public int ElementId;
        public string UniqueId;
        public string Category;
        public string Level;
        public string Name;
        /// <summary>sRGB 0–1</summary>
        public float R, G, B;
        public float Opacity = 1f;
        public List<double> Positions = new List<double>();
        public List<int> Indices = new List<int>();
        /// <summary>Readable instance parameters for the HTML properties panel.</summary>
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
