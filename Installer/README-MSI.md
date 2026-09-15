# ClearView 3D — optional installer notes (free build)

No license files. Installer is optional; many users just copy the DLL + `.addin`.

## Manual install (simplest)

1. Put `ClearView.dll` and `Newtonsoft.Json.dll` in any folder, e.g.
   `C:\ClearView3D\`
2. Edit `ClearView.addin` so `<Assembly>` points to that DLL.
3. Copy the `.addin` to:
   `%APPDATA%\Autodesk\Revit\Addins\2020\ClearView.addin`
4. Restart Revit.

## Optional MSI / WiX

If you want a one-click installer for your team:

1. Install DLL(s) under `C:\Program Files\ClearView3D\`
2. Write `.addin` to `%APPDATA%\Autodesk\Revit\Addins\2020\` or `%ProgramData%\Autodesk\Revit\Addins\2020\`
3. On uninstall, remove both the folder and the `.addin` file

No trial.dat, no license.key, no DRM.
