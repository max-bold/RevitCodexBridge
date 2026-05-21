---
name: revit-measure-sweep-path
description: Measure the path length of one or more Revit Sweep elements through RevitCodexBridge. Use when the user asks to calculate, find, measure, report, or verify the length of a selected sweep path, sweep trajectory, sweep curve loop, handrail/profile sweep, or family sweep in the active Revit document.
---

# Revit Measure Sweep Path

## Workflow

Use the repository bridge scripts. Do not call Revit API from an external process directly.

1. Check the bridge and active document:
   - Run `.\scripts\Test-Bridge.ps1`.
   - Run `.\scripts\Wait-Document.ps1` if document state is uncertain.
2. Run one `Invoke-RevitCSharp` command at a time. Revit `ExternalEvent` can reject parallel `/command` calls with `Pending`.
3. Prefer selected `Sweep` elements from `app.ActiveUIDocument.Selection.GetElementIds()`.
4. If no selected sweep is visible to the API:
   - If exactly one `Sweep` exists, measure it.
   - If multiple sweeps exist, report lengths for all sweeps and state that selection was not visible through the API.
5. Do not modify the document. No transaction is required for measuring.

## Measurement Snippet

Run the bundled C# snippet:

```powershell
$code = Get-Content -Raw .\.codex\skills\revit-measure-sweep-path\scripts\measure-sweep-path.cs
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

The script is inserted into the bridge `run-csharp` wrapper, so it assumes the repository's default Revit API usings are available.

## Reporting

Return the length in millimeters and meters. Include the `ElementId` whenever more than one sweep is reported.

If `measuredMode` is `all-sweeps-selection-not-visible`, explicitly tell the user that the active selection was not visible to the API and that the output lists all sweep candidates.
