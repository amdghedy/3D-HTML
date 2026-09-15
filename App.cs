using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace ClearView
{
    public class App : IExternalApplication
    {
        public static UIControlledApplication UIApp { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            UIApp = application;
            const string tab = "BIM TOOLS";
            try { application.CreateRibbonTab(tab); }
            catch { /* tab already exists from another add-in */ }

            RibbonPanel panel = null;
            foreach (var p in application.GetRibbonPanels(tab))
            {
                if (p.Name.Equals("Share", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("3D HTML Viewer", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("3D HTML", StringComparison.OrdinalIgnoreCase))
                {
                    panel = p;
                    break;
                }
            }
            if (panel == null)
                panel = application.CreateRibbonPanel(tab, "3D HTML Viewer");

            var asm = Assembly.GetExecutingAssembly().Location;
            var data = new PushButtonData(
                "ClearViewExport",
                "3D HTML Viewer",
                asm,
                "ClearView.Commands.ExportCommand")
            {
                ToolTip = "Export the current 3D view to an interactive HTML package.",
                LongDescription =
                    "Exports the active 3D view to HTML. Recipients can orbit, toggle levels/categories, " +
                    "select elements, measure, and section-cut — no Revit required."
            };

            var icon = Path.Combine(Path.GetDirectoryName(asm) ?? "", "icon32.png");
            if (File.Exists(icon))
                data.LargeImage = new BitmapImage(new Uri(icon, UriKind.Absolute));

            panel.AddItem(data);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
    }
}
