# Codex Instructions for RevitCodexBridge

This repository contains a Revit 2025 add-in that exposes a local bridge for Codex and automation scripts.

## Fresh machine install

When asked to install the bridge from a clone, use the provided installer instead of manually writing manifests:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Install-Bridge.ps1 -ValidateOnly
.\scripts\Install-Bridge.ps1
```

What the installer does:

- verifies the repository, .NET SDK 8, and Revit executable path;
- runs `dotnet restore`;
- publishes the add-in into `deploy/<timestamp>`;
- copies Roslyn runtime dependencies needed by `run-csharp`;
- writes `%APPDATA%\Autodesk\Revit\Addins\2025\RevitCodexBridge.addin`.

After installation, Revit must be restarted because add-ins are loaded only at startup.

If Revit is installed in a non-default location, pass it explicitly:

```powershell
.\scripts\Install-Bridge.ps1 -RevitExe 'D:\Apps\Autodesk\Revit 2025\Revit.exe'
```

## Runtime model

- Revit must be running with the `Revit Codex Bridge` add-in loaded.
- The bridge listens on `http://127.0.0.1:17825`.
- Use `GET /health` to check that the bridge process is alive.
- Use `POST /command` for all Revit API work. Commands are executed through Revit `ExternalEvent`, so code runs in a valid Revit API context.
- Do not call Revit API from an external process directly. Send commands to the bridge.
- Do not save, synchronize, close, or overwrite a model unless the user explicitly asks.
- Assume opened projects can be large. Wait patiently for `doc-info.hasDocument == true` before running model commands.

## Built-in commands

Send JSON to `/command` with a `command` field:

- `app-info`: returns Revit version and add-in id.
- `doc-info`: returns active document title, path, modified state, family flag, and element count.
- `selection`: returns currently selected elements.
- `collect`: returns sample elements. Optional fields: `category`, `limit`.
- `delete-line-patterns`: deletes line patterns by prefix. Fields: `prefix`, `dryRun`.
- `run-csharp`: compiles and executes C# code through Roslyn. Field: `code`.

## Preferred scripts

Use scripts from `scripts/` instead of hand-writing HTTP calls:

```powershell
.\scripts\Test-Bridge.ps1
.\scripts\Wait-Document.ps1
.\scripts\Get-DocumentInfo.ps1
.\scripts\Invoke-RevitCSharp.ps1 -Code 'return app.ActiveUIDocument.Document.Title;'
.\scripts\Remove-LinePatterns.ps1 -Prefix IMPORT -DryRun
```

For destructive operations, run dry-run first unless the user clearly asked for deletion.

## Repository skills

Project-specific Codex skills live in `.codex/skills/`. Prefer these repo-local skills over user-level skills when a request matches them, so repeated Revit workflows stay versioned with the bridge.

- `revit-edit-selected-text-note`: use when asked to rewrite, correct, expand, renumber, translate, or otherwise edit the currently selected Revit text block.
- `revit-hide-sections-by-view-purpose`: use when asked to hide section markers from another discipline/package by `Назначение вида` and add the filter to a Revit view template.
- `revit-annotate-stairs-current-view`: use when asked to create stair annotations, stair paths, or stair direction arrows for all stairs on the current Revit view.

## `run-csharp` contract

The submitted code is inserted into:

```csharp
public static object? Run(UIApplication app)
{
    // your code here
}
```

Default usings are available:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
```

Return simple anonymous objects or DTO-shaped values so JSON serialization can report results cleanly.

Wrap document modifications in a Revit `Transaction`. Use `Transaction.RollBack()` for probes and tests that should not modify the model.

Example:

```powershell
.\scripts\Invoke-RevitCSharp.ps1 -Code @'
var doc = app.ActiveUIDocument?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };
return new
{
    title = doc.Title,
    linePatternCount = new FilteredElementCollector(doc)
        .OfClass(typeof(LinePatternElement))
        .Cast<LinePatternElement>()
        .Count()
};
'@
```

## Development workflow

- Source repo: `D:\Code\RevitCodexBridge` in the original local setup, but scripts should work from any clone path.
- Build and deploy locally with `.\scripts\Publish-Bridge.ps1`.
- Revit loads add-ins only at startup. Rebuilds usually require restarting Revit or pointing the manifest at a new deployment folder before restart.
- Keep deploy outputs out of git. `bin/`, `obj/`, and `deploy/` are ignored.
