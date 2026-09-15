using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClearView.Export;
using Microsoft.Win32;

namespace ClearView.UI
{
    public partial class ExportWindow : Window
    {
        public ExportSettings Settings { get; private set; }

        private readonly Dictionary<string, CheckBox> _levelBoxes = new Dictionary<string, CheckBox>();
        private readonly Dictionary<string, CheckBox> _catBoxes = new Dictionary<string, CheckBox>();

        public ExportWindow(ExportSettings settings)
        {
            InitializeComponent();
            Settings = settings;

            ViewLabel.Text = "Current 3D view: " + settings.ViewName + "  ·  large models: merge + coarse tessellation";
            BrandBox.Text = settings.Brand ?? "3D HTML Viewer";
            PathBox.Text = settings.OutputPath ?? "";
            TessSlider.Value = settings.Tessellation;
            ColorsCheck.IsChecked = settings.UseElementColors;
            LinksCheck.IsChecked = settings.IncludeLinks;
            if (MergeCheck != null) MergeCheck.IsChecked = settings.MergeMeshes;
            if (ToolsCheck != null) ToolsCheck.IsChecked = settings.EnableViewerTools;
            if (MinSizeBox != null) MinSizeBox.Text = settings.MinElementSizeFt.ToString("0.###");
            UpdateTessLabel();

            foreach (var name in settings.AvailableLevels)
            {
                var cb = new CheckBox
                {
                    Content = name,
                    IsChecked = settings.SelectedLevels.Contains(name)
                };
                _levelBoxes[name] = cb;
                LevelsPanel.Children.Add(cb);
            }

            foreach (var name in settings.AvailableCategories)
            {
                var cb = new CheckBox
                {
                    Content = name,
                    IsChecked = settings.SelectedCategories.Contains(name)
                };
                _catBoxes[name] = cb;
                CategoriesPanel.Children.Add(cb);
            }
        }

        void TessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            UpdateTessLabel();

        void UpdateTessLabel()
        {
            if (TessLabel == null || TessSlider == null) return;
            var v = (int)TessSlider.Value;
            string q = v <= 4 ? "Coarse · best for 5k+ elements" : v <= 10 ? "Medium" : "Fine · large HTML";
            TessLabel.Text = v + "  ·  " + q;
        }

        void LevelsAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _levelBoxes.Values) cb.IsChecked = true;
        }

        void LevelsNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _levelBoxes.Values) cb.IsChecked = false;
        }

        void CatsAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _catBoxes.Values) cb.IsChecked = true;
        }

        void CatsNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _catBoxes.Values) cb.IsChecked = false;
        }

        void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "HTML package (*.html)|*.html",
                FileName = System.IO.Path.GetFileName(PathBox.Text),
                InitialDirectory = System.IO.Path.GetDirectoryName(PathBox.Text)
            };
            if (dlg.ShowDialog() == true)
                PathBox.Text = dlg.FileName;
        }

        void SystemColors_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SystemColorsWindow(Settings.SystemColorOverrides)
            {
                Owner = this
            };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                Settings.SystemColorOverrides = dlg.Result;
            }
        }

        void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        void Export_Click(object sender, RoutedEventArgs e)
        {
            Settings.Brand = BrandBox.Text?.Trim();
            Settings.OutputPath = PathBox.Text?.Trim();
            Settings.Tessellation = (int)TessSlider.Value;
            Settings.UseElementColors = ColorsCheck.IsChecked == true;
            Settings.IncludeLinks = LinksCheck.IsChecked == true;
            Settings.MergeMeshes = MergeCheck == null || MergeCheck.IsChecked == true;
            Settings.EnableViewerTools = ToolsCheck == null || ToolsCheck.IsChecked == true;
            if (MinSizeBox != null && double.TryParse(MinSizeBox.Text, out var minSz))
                Settings.MinElementSizeFt = minSz;

            Settings.SelectedLevels = new HashSet<string>(
                _levelBoxes.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key));
            Settings.SelectedCategories = new HashSet<string>(
                _catBoxes.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key));

            // Push user system colours into the static map used during export
            SystemColorMap.Overrides = Settings.SystemColorOverrides;

            if (string.IsNullOrWhiteSpace(Settings.OutputPath))
            {
                MessageBox.Show("Choose an output path.", "ClearView 3D");
                return;
            }

            if (Settings.SelectedLevels.Count == 0 || Settings.SelectedCategories.Count == 0)
            {
                MessageBox.Show("Select at least one level and one category.", "ClearView 3D");
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
