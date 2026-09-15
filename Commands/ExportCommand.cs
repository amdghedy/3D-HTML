using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClearView.Export;
using ClearView.UI;

namespace ClearView.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                message = "Open a project first.";
                return Result.Failed;
            }

            var doc = uiDoc.Document;
            var view = doc.ActiveView as View3D;
            if (view == null || view.IsTemplate)
            {
                TaskDialog.Show(
                    "3D HTML Viewer",
                    "Activate a non-template 3D view, then run Export 3D Package.\n\n" +
                    "Exports the current 3D view only.");
                return Result.Cancelled;
            }

            var settings = ExportSettings.FromDocument(doc, view);
            var window = new ExportWindow(settings);
            var ok = window.ShowDialog();
            if (ok != true)
                return Result.Cancelled;

            settings = window.Settings;

            ViewDetailLevel originalDetail = view.DetailLevel;
            bool detailChanged = false;

            try
            {
                // Temporarily lower detail for large models (fewer triangles)
                ViewDetailLevel target = settings.Tessellation <= 4
                    ? ViewDetailLevel.Coarse
                    : settings.Tessellation <= 10
                        ? ViewDetailLevel.Medium
                        : ViewDetailLevel.Fine;

                if (target != originalDetail)
                {
                    using (var t = new Transaction(doc, "ClearView temp detail"))
                    {
                        t.Start();
                        view.DetailLevel = target;
                        t.Commit();
                        detailChanged = true;
                    }
                }

                var context = new RevitExportContext(doc, view, settings);
                var exporter = new CustomExporter(doc, context)
                {
                    IncludeGeometricObjects = true,
                    ShouldStopOnError = false
                };

                var progress = new ExportProgressWindow(context);
                progress.Show();

                exporter.Export((View)view);

                if (!context.CancelRequested)
                {
                    try
                    {
                        progress.SetStatus("Extracting MEP pipes / fittings / ducts…");
                        MepGeometryFallback.AppendMissing(doc, view, settings, context);
                        HostGeometryFallback.AppendMissing(doc, view, settings, context);
                    }
                    catch (Exception mepEx)
                    {
                        progress.SetStatus("MEP fallback warning: " + mepEx.Message);
                    }
                }

                if (!context.CancelRequested && context.Meshes.Count > 0)
                {
                    // Always combine faces of the same element (19k face-meshes → ~element count).
                    // Keeps per-element select + properties; required for browser stability.
                    progress.SetStatus("Combining meshes per element…");
                    var byElement = MeshMerger.MergeByElement(context.Meshes);
                    context.Meshes.Clear();
                    context.Meshes.AddRange(byElement);

                    // Optional aggressive merge (loses per-element select)
                    if (settings.MergeMeshes)
                    {
                        progress.SetStatus("Merging by category for smaller HTML…");
                        var byCat = MeshMerger.MergeByCategory(context.Meshes);
                        context.Meshes.Clear();
                        context.Meshes.AddRange(byCat);
                    }
                }

                progress.Close();

                if (context.CancelRequested)
                {
                    TaskDialog.Show("3D HTML Viewer", "Export cancelled.");
                    return Result.Cancelled;
                }

                PackageBuilder.Write(settings, context);
                TaskDialog.Show(
                    "3D HTML Viewer",
                    "Package written:\n" + settings.OutputPath +
                    "\n\nViewer nodes: " + context.Meshes.Count +
                    "\nTriangles: " + context.TriangleCount +
                    "\nElements scanned: " + context.ProcessedElements +
                    "\nSkipped (small): " + context.SkippedSmall +
                    "\n" + (context.StatusMessage ?? ""));
            }
            catch (OperationCanceledException)
            {
                TaskDialog.Show("3D HTML Viewer", "Export cancelled.");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("3D HTML Viewer", ex.Message);
                return Result.Failed;
            }
            finally
            {
                if (detailChanged)
                {
                    try
                    {
                        using (var t = new Transaction(doc, "ClearView restore detail"))
                        {
                            t.Start();
                            view.DetailLevel = originalDetail;
                            t.Commit();
                        }
                    }
                    catch { }
                }
            }

            return Result.Succeeded;
        }
    }
}
