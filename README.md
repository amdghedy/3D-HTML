# ClearView 3D — Revit 2020 Add-in (free / open)

Export the **current 3D view** to a self-contained HTML package. Recipients orbit the model, toggle levels/categories, **measure distances**, and use a **section cut** — no Revit required.

Free to use, modify, and share. No license key.

## Large models (5,000+ elements)

| Option | Default | Purpose |
|--------|---------|---------|
| Merge meshes | On | Collapse geometry by level+category+colour so the browser stays responsive |
| Tessellation | Auto-coarse if view has >5k elements | Fewer triangles via temporary view detail level |
| Skip small elements | 0.05 ft diagonal | Drops noise/detail that bloats the file |
| Max elements | 0 (unlimited) | Optional hard cap |

View detail level is **restored** after export.

## Viewer tools (in HTML)

- **Orbit** — default navigation  
- **Measure** — click two points → distance in **m / ft / mm** (model units = Revit feet)  
- **Section cut** — plane on X/Y/Z with position slider + flip  

## Build

See project setup in earlier README / `ClearView3D.csproj`. Target **.NET Framework 4.8**, Revit 2020 API, NuGet **Newtonsoft.Json**.

## Limits

- Facetted mesh, not BREP  
- Measure is point-to-point on visible mesh, not true BIM dimension snaps  
- Section is a GPU clip plane, not a Revit section box export  
- HTML still loads Three.js from CDN (needs internet) unless you embed three.min.js  
