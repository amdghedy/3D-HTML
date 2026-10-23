<div align="center">

# ClearView 3D

### Revit 3D views → interactive, shareable HTML model packages

**A lightweight coordination viewer for teams who need to review a 3D model without opening the authoring application.**

[Features](#features) · [Tool reference](#complete-tool-and-function-reference) · [Export workflow](#export-workflow) · [Build](#build-from-source) · [Limitations](#known-limitations)

</div>

---

## Overview

ClearView 3D is a C# add-in for **Revit 2020** that exports the active, non-template 3D view into an interactive HTML model package. It is intended for coordination and review between model authors and project stakeholders who may not have Revit installed.

The exported viewer provides 3D navigation, visibility controls, point-to-point measurement, section clipping and element information. Export settings let users control included levels and categories, geometry detail, colours, linked models and large-model optimisations.

> **Scope:** the exporter processes the current 3D view. It is not a full native-model conversion tool. Exported geometry is tessellated mesh data, not editable BIM geometry.

## At a glance

| Item | Details |
|---|---|
| Application | Autodesk Revit 2020 add-in |
| Language / UI | C# with WPF windows |
| Target framework | .NET Framework 4.8 |
| Main command | Export 3D Package |
| Output | HTML model viewer/package |
| JSON dependency | Newtonsoft.Json |
| Intended use | Model review, coordination, sharing and lightweight inspection |

## Features

- **Browser-based 3D review** — share an exported model with users who do not have Revit installed.
- **Orbit navigation** — rotate and inspect the model from different angles.
- **Visibility control** — toggle exported levels and categories in the viewer.
- **Distance measurement** — pick two visible mesh points and read the distance in metres, feet or millimetres.
- **Section cut** — clip the model along X, Y or Z with a position slider and direction flip.
- **Element properties** — retain element-level records for selection and inspection of available exported properties.
- **Linked model support** — include linked-model geometry when enabled.
- **Element and system colours** — use element colours or configure system colour overrides for MEP coordination.
- **Large-model options** — mesh merging, adjustable tessellation, minimum element-size filtering and an optional element cap.
- **Export progress** — display progress/status during geometry extraction.
- **Shareable HTML deliverable** — open the output in a browser without a separate viewer installation.

## Complete tool and function reference

This catalogue describes tools and behaviours identified in the current source tree. Viewer controls depend on export settings and the data present in the active view.

### Revit add-in and export workflow

| Tool / function | What it does | Source |
|---|---|---|
| **Export 3D Package** | Starts exporting the active non-template 3D view. | Commands/ExportCommand.cs |
| Active-view validation | Stops when no project is open or the active view is not a valid 3D view. | Commands/ExportCommand.cs |
| Export settings window | Collects output path, branding, levels, categories, colours and geometry options. | UI/ExportWindow.xaml |
| Output file browser | Lets the user choose the HTML output path. | UI/ExportWindow.xaml.cs |
| Level selection | Includes selected levels in the package. | Export settings window |
| Select all levels | Selects all available levels. | Export settings window |
| Clear level selection | Clears the level selection; an empty selection means no level filter in the current settings logic. | Export settings window |
| Category selection | Includes selected categories in the package. | Export settings window |
| Select all categories | Selects all available categories. | Export settings window |
| Clear category selection | Clears the category selection; an empty selection means no category filter in the current settings logic. | Export settings window |
| Tessellation/detail slider | Adjusts geometry detail; lower settings favour smaller packages, higher settings retain more detail. | Export settings window |
| Use element colours | Controls whether element colours are used in the export. | Export settings window |
| Include linked models | Enables linked-model geometry export. | Export settings window |
| Merge meshes | Combines compatible mesh data to reduce browser overhead on large models. | Export settings window |
| Viewer tools toggle | Enables or disables measurement and section tools in the HTML viewer. | Export settings window |
| Minimum element size | Filters elements below the configured bounding-box diagonal threshold. | ExportSettings |
| Maximum elements | Optional cap on processed source elements; 0 means unlimited. | ExportSettings |
| Export progress window | Reports status while geometry is collected and written. | UI/ExportProgressWindow.xaml |
| Cancellation handling | Checks the export context's cancellation state before later export stages. | Export context / progress workflow |
| Temporary detail-level optimisation | May temporarily lower the active view detail level to reduce tessellation cost, then attempts to restore the original setting. | Commands/ExportCommand.cs |
| MEP geometry fallback | Attempts a secondary pass for pipe, fitting and duct geometry missed by the main export. | Export/MepGeometryFallback.cs |
| Host geometry fallback | Attempts to recover additional host geometry missed by the main export. | Export/HostGeometryFallback.cs |
| Mesh merging by level/category/colour | Groups compatible geometry to reduce mesh count while retaining element-level records where supported. | Export/MeshMerger.cs; Export/PackageBuilder.cs |
| Element property collection | Collects properties/metadata to accompany exported geometry. | Export/PropertyCollector.cs |
| glTF writer | Writes glTF-related geometry data used by the export pipeline. | Export/GltfWriter.cs |
| HTML package builder | Assembles model data and viewer output. | Export/PackageBuilder.cs |
| Revit custom export context | Handles geometry and element callbacks from Revit's custom exporter. | Export/RevitExportContext.cs |

### HTML viewer tools

| Viewer tool | Function | Notes |
|---|---|---|
| **Orbit / 3D navigation** | Rotate and inspect the exported model. | Default viewer navigation |
| **Level visibility** | Toggle exported levels. | Only levels included in the package are available |
| **Category visibility** | Toggle exported categories. | Only exported categories are available |
| **Measure** | Pick two visible mesh points and calculate distance. | Not a native BIM dimension snap |
| **Units display** | Show measurements in metres, feet or millimetres. | Source coordinates originate from Revit feet |
| **Section cut** | Clip the displayed model along an X, Y or Z plane. | Viewer-side clipping |
| **Section position** | Move the section plane with a slider. | When viewer tools are enabled |
| **Section flip** | Reverse the clipping direction. | When viewer tools are enabled |
| **Element selection / inspection** | Select exported geometry and inspect retained properties where provided. | Limited to exported metadata |

### MEP and colour coordination

| Tool / function | What it does |
|---|---|
| MEP category handling | Recognises common mechanical, electrical and plumbing categories for export. |
| MEP geometry fallback | Attempts a secondary geometry pass for pipes, fittings and ducts missing from the primary pass. |
| System colour mapping | Maps system classifications to display colours. |
| System colour editor | Opens the colour editor for system classifications. |
| Colour swatches | Click a colour swatch to change the corresponding system colour. |
| Hex colour display | Shows the current RGB colour as a hexadecimal value. |
| Default system colours | Seeds the editor with built-in defaults, then applies existing or detected system colours. |
| Element colour mode | Uses element-level colours when enabled in export options. |

### Large-model performance controls

| Setting | Behaviour |
|---|---|
| **Merge meshes** | Reduces the number of separate meshes by combining compatible geometry. |
| **Tessellation** | Lower values use coarser geometry; higher values retain more detail but can increase package size and browser workload. |
| **Minimum element size** | Skips elements whose bounding-box diagonal is below the configured threshold. |
| **Maximum elements** | Limits source elements processed; 0 means no explicit cap. |
| **Temporary view detail adjustment** | Uses a coarser Revit view detail level when indicated by the tessellation setting and attempts to restore the original setting. |
| **Progress reporting** | Provides status updates during export. |

For large views, start with mesh merging enabled and coarse or medium tessellation. Apply a minimum size only if small details are not required. Always inspect the HTML output before issuing it for project use.

## Export workflow

1. Open the Revit project and activate the **3D view** to share. Use a non-template 3D view.
2. Run **Export 3D Package**.
3. Choose the output path and optionally edit package branding.
4. Select the required levels and categories.
5. Configure linked models, element colours, tessellation, mesh merging, minimum element size, element cap and viewer tools.
6. Start the export and monitor the progress window.
7. Open the generated HTML file in a supported desktop browser and review navigation, visibility, measurements and section clipping.
8. Share the deliverable with coordination participants. Check project data-sharing rules before distributing it externally.

## Build from source

### Requirements

- Windows
- Visual Studio with .NET desktop development components
- .NET Framework 4.8 targeting pack
- Autodesk Revit 2020 and its API assemblies
- NuGet restore for Newtonsoft.Json

### Build steps

1. Clone or download this repository.
2. Open ClearView3D.sln in Visual Studio.
3. Check the Revit2020Path property in ClearView3D.csproj. By default it points to C:\Program Files\Autodesk\Revit 2020.
4. Restore the NuGet packages.
5. Build the solution for the required configuration.
6. Configure ClearView.addin to point to the built assembly, then install the manifest in the Revit 2020 Addins folder for your user or deployment environment.
7. Launch Revit 2020 and test with a non-production sample project before using the tool on a live project.

> The checked-in add-in manifest currently contains a fixed example assembly path. Update it for your own build/deployment location before installing.

## Repository structure

| Path | Purpose |
|---|---|
| App.cs | Registers the add-in and its Revit UI integration. |
| Commands/ExportCommand.cs | Entry point for the export command and export lifecycle. |
| Export/ExportSettings.cs | Export options, filters and defaults. |
| Export/RevitExportContext.cs | Revit custom-export callbacks and geometry capture. |
| Export/PackageBuilder.cs | Builds the viewer package from captured data. |
| Export/GltfWriter.cs | Writes glTF geometry data. |
| Export/PropertyCollector.cs | Collects element properties. |
| Export/MeshRecord.cs | Represents mesh data in the export pipeline. |
| Export/MeshMerger.cs | Merges compatible mesh data. |
| Export/MepCategories.cs | MEP category classification helpers. |
| Export/MepGeometryFallback.cs | Secondary MEP geometry collection path. |
| Export/HostGeometryFallback.cs | Secondary host geometry collection path. |
| Export/SystemColorMap.cs | Default and configurable system colour mapping. |
| UI/ExportWindow.xaml and .xaml.cs | Export configuration interface and actions. |
| UI/ExportProgressWindow.xaml and .xaml.cs | Export progress and status interface. |
| UI/SystemColorsWindow.xaml and .xaml.cs | System colour editor. |
| ClearView.addin | Revit add-in registration manifest. |
| ClearView3D.csproj | Project build settings and Revit API references. |
| ClearView3D.sln | Visual Studio solution. |
| Installer/README-MSI.md | Notes for the optional MSI installer approach. |

## Known limitations

- **Revit version:** the project references the Revit 2020 API. Other versions need a compatible build and API references; do not assume one DLL works across versions.
- **Mesh geometry:** the viewer displays tessellated/facetted geometry, not native editable BIM objects or exact BREP surfaces.
- **Measurement accuracy:** measurements are point-to-point on visible mesh geometry and are not equivalent to Revit dimensioning or object-snap tools.
- **Section clipping:** the section cut is a browser-side GPU clipping plane, not a Revit section box or generated section view.
- **Export scope:** only the active 3D view is exported, subject to selected filters and visibility/geometry supported by the exporter.
- **Geometry completeness:** fallback passes attempt to recover missing host and MEP geometry, but every family, link, material or geometry case is not guaranteed to export identically.
- **External dependency:** the HTML viewer currently loads Three.js from a CDN, so it needs internet access unless the library is embedded locally.
- **Validation:** verify geometry, element properties, linked content and browser performance on representative models before relying on an export for contractual or construction decisions.

## Troubleshooting

| Symptom | Checks |
|---|---|
| Export command reports no valid view | Open a project and activate a non-template 3D view. |
| Output is too large or slow | Enable mesh merging, lower tessellation, increase minimum element size cautiously, or set a maximum element count. |
| Some geometry is missing | Check active-view visibility, filters, section box, selected levels/categories and linked-model setting. Test the affected family in a small sample. |
| Measurement or section tools are absent | Confirm Viewer tools is enabled in export settings. |
| Viewer does not load offline | The current viewer depends on Three.js from a CDN; embed the library for offline use. |
| Build cannot resolve Revit API | Verify Revit2020Path and confirm RevitAPI.dll and RevitAPIUI.dll exist there. |
| Add-in does not appear | Verify the .addin manifest assembly path and installation directory. |

## Contributing

Bug reports and focused improvements are welcome. Include the Revit version, steps to reproduce, relevant error text, and a small sample model where safe. Avoid committing private project models, credentials, generated binaries or other sensitive project data.

## License and use

The existing short README describes the tool as free to use, modify and share. A formal repository license file was not identified during this documentation update; check with the repository owner before redistributing the project or incorporating it into a commercial product.

---

<div align="center">

**ClearView 3D** · Lightweight 3D model sharing for BIM coordination

</div>
