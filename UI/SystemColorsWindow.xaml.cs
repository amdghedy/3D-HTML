using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ClearView.Export;
using MediaColor = System.Windows.Media.Color;
using RevitColor = Autodesk.Revit.DB.Color;

namespace ClearView.UI
{
    public partial class SystemColorsWindow : Window
    {
        readonly Dictionary<string, int> _colors;
        readonly Dictionary<string, Border> _swatches = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Result after Apply; null if cancelled.</summary>
        public Dictionary<string, int> Result { get; private set; }

        public SystemColorsWindow(Dictionary<string, int> existing)
        {
            InitializeComponent();

            _colors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Seed with defaults
            foreach (var name in SystemColorMap.EditableSystems)
            {
                var def = SystemColorMap.GetDefaultColor(name);
                _colors[name] = SystemColorMap.Pack(def);
            }
            // Merge auto-detected Revit system types (and any previous user picks)
            if (existing != null)
            {
                foreach (var kv in existing)
                    _colors[kv.Key] = kv.Value;
            }

            BuildRows();
        }

        void BuildRows()
        {
            RowsPanel.Children.Clear();
            _swatches.Clear();

            var names = new List<string>(_colors.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                int packed = _colors[name];
                byte r = (byte)((packed >> 16) & 0xFF);
                byte g = (byte)((packed >> 8) & 0xFF);
                byte b = (byte)(packed & 0xFF);

                var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3), LastChildFill = true };

                var swatch = new Border
                {
                    Width = 36,
                    Height = 28,
                    CornerRadius = new CornerRadius(4),
                    BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0xCC, 0xCC, 0xCC)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(MediaColor.FromRgb(r, g, b)),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(0, 0, 10, 0),
                    ToolTip = "Click to change colour"
                };
                swatch.MouseLeftButtonUp += (s, e) => PickColor(name, swatch);
                DockPanel.SetDock(swatch, Dock.Left);
                row.Children.Add(swatch);
                _swatches[name] = swatch;

                var hexBtn = new Button
                {
                    Content = string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b),
                    Width = 78,
                    Height = 28,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Background = new SolidColorBrush(MediaColor.FromRgb(0xF0, 0xF0, 0xF0)),
                    Foreground = new SolidColorBrush(MediaColor.FromRgb(0x1A, 0x1A, 0x1A)),
                    BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0xCC, 0xCC, 0xCC)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(0, 0, 0, 0),
                    ToolTip = "Click to change colour"
                };
                hexBtn.Click += (s, e) => PickColor(name, swatch);
                DockPanel.SetDock(hexBtn, Dock.Right);
                // Keep hex on the right
                var rightPanel = new StackPanel { Orientation = Orientation.Horizontal };
                rightPanel.Children.Add(hexBtn);
                DockPanel.SetDock(rightPanel, Dock.Right);
                row.Children.Add(rightPanel);

                var label = new TextBlock
                {
                    Text = name,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 13
                };
                row.Children.Add(label);

                // Store hex button on the border tag for later update
                swatch.Tag = hexBtn;

                RowsPanel.Children.Add(row);
            }
        }

        void PickColor(string name, Border swatch)
        {
            int packed = _colors.ContainsKey(name) ? _colors[name] : 0xA0A0A0;
            byte r = (byte)((packed >> 16) & 0xFF);
            byte g = (byte)((packed >> 8) & 0xFF);
            byte b = (byte)(packed & 0xFF);

            // Use WinForms ColorDialog (available via System.Windows.Forms reference)
            using (var dlg = new System.Windows.Forms.ColorDialog())
            {
                dlg.Color = System.Drawing.Color.FromArgb(r, g, b);
                dlg.FullOpen = true;
                dlg.AnyColor = true;
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                r = dlg.Color.R;
                g = dlg.Color.G;
                b = dlg.Color.B;
            }

            _colors[name] = (r << 16) | (g << 8) | b;
            swatch.Background = new SolidColorBrush(MediaColor.FromRgb(r, g, b));
            var hexBtn = swatch.Tag as Button;
            if (hexBtn != null)
                hexBtn.Content = string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b);
        }

        void Reset_Click(object sender, RoutedEventArgs e)
        {
            var keys = new List<string>(_colors.Keys);
            foreach (var name in keys)
            {
                var def = SystemColorMap.GetDefaultColor(name);
                _colors[name] = SystemColorMap.Pack(def);
            }
            BuildRows();
        }

        void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
            Close();
        }

        void Apply_Click(object sender, RoutedEventArgs e)
        {
            Result = new Dictionary<string, int>(_colors, StringComparer.OrdinalIgnoreCase);
            DialogResult = true;
            Close();
        }
    }
}
