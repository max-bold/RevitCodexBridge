---
name: revit-annotate-stairs-current-view
description: Create stair path annotations for every visible stair in the active Revit view through RevitCodexBridge. Use when the user asks to create annotations, stair paths, arrows, direction markers, or stair graphics for all stairs on the current view, including turning off "Show Up Text" on the created arrows.
---

# Revit Annotate Stairs Current View

## Workflow

Use repository scripts from the repo root. Run Revit bridge commands sequentially; parallel `/command` calls can be rejected with `ExternalEvent was not accepted: Pending`.

1. Check bridge and document:

```powershell
.\scripts\Test-Bridge.ps1
.\scripts\Wait-Document.ps1
```

2. Inspect visible stairs, existing stair paths, and available stair path types:

```powershell
$code = Get-Content -Raw .\.codex\skills\revit-annotate-stairs-current-view\scripts\inspect-active-view-stairs.cs
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

3. If API behavior is uncertain, run the rollback probe:

```powershell
$code = Get-Content -Raw .\.codex\skills\revit-annotate-stairs-current-view\scripts\probe-create-stair-path.cs
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

4. Create stair paths for visible stairs in the active view:

```powershell
$code = Get-Content -Raw .\.codex\skills\revit-annotate-stairs-current-view\scripts\create-stair-paths-current-view.cs
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

5. Verify resulting stair paths and `Show Up Text` values:

```powershell
$code = Get-Content -Raw .\.codex\skills\revit-annotate-stairs-current-view\scripts\verify-stair-paths-current-view.cs
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

## Notes

- `scripts/create-stair-paths-current-view.cs` modifies the model. Inspect first and do not run it unless the user requested creation.
- The create script uses the first available `StairsPathType`.
- `Show Up Text` is an instance parameter on `StairsPath`; in the observed project its parameter id is `-1006631`, storage type `Integer`, values `Yes/No`.
- `Up Text` is a separate string parameter (`-1006632`). Do not clear it when the request is only to hide up text.
- If existing stair paths are present, inspect them before creating more. Revit may allow duplicates; avoid duplicate annotations unless the user explicitly wants another set.
- Keep changes in the active view only. Do not save, synchronize, close, or overwrite the model unless explicitly requested.
