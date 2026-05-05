# RevitCodexBridge

Local HTTP bridge for driving Autodesk Revit 2025 from Codex or other automation tools.

The add-in starts a loopback server on `127.0.0.1:17825`. Revit API work is queued through `ExternalEvent`, so bridge commands run inside a valid Revit API context.

## Install locally

Requirements:

- Autodesk Revit 2025
- .NET SDK 8
- PowerShell

From a fresh clone, let the installer validate the machine and install the add-in:

```powershell
.\scripts\Install-Bridge.ps1 -ValidateOnly
.\scripts\Install-Bridge.ps1
```

The installer restores NuGet packages, publishes the add-in, copies required Roslyn runtime DLLs, and writes the Revit manifest.

Then restart Revit. Revit loads add-ins only at startup.

If you only need to republish an already configured installation, run:

```powershell
.\scripts\Publish-Bridge.ps1
```

To publish into a named deployment folder:

```powershell
.\scripts\Publish-Bridge.ps1 -DeployName v1
```

The script writes:

- `deploy/<name>/RevitCodexBridge.dll`
- `%APPDATA%\Autodesk\Revit\Addins\2025\RevitCodexBridge.addin`

If PowerShell blocks local scripts, run from this repository:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

## Quick checks

```powershell
.\scripts\Test-Bridge.ps1
.\scripts\Wait-Document.ps1
.\scripts\Get-DocumentInfo.ps1
```

Run dynamic C# in the active Revit document:

```powershell
.\scripts\Invoke-RevitCSharp.ps1 -Code @'
var doc = app.ActiveUIDocument?.Document;
return new
{
    hasDocument = doc != null,
    title = doc?.Title,
    elementCount = doc == null ? 0 : new FilteredElementCollector(doc)
        .WhereElementIsNotElementType()
        .GetElementCount()
};
'@
```

Dry-run deletion of imported line patterns:

```powershell
.\scripts\Remove-LinePatterns.ps1 -Prefix IMPORT -DryRun
```

Delete after checking the dry-run:

```powershell
.\scripts\Remove-LinePatterns.ps1 -Prefix IMPORT -Confirm:$false
```

## Bridge commands

`POST /command` accepts JSON with a `command` field:

- `app-info`
- `doc-info`
- `selection`
- `collect`
- `delete-line-patterns`
- `run-csharp`

Use the scripts in `scripts/` for day-to-day work instead of hand-writing HTTP calls.

## Codex usage

See `AGENTS.md` for operating rules and examples intended for Codex agents.
