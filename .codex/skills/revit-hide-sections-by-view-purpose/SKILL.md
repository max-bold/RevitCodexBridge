---
name: revit-hide-sections-by-view-purpose
description: Create or update Revit view-template filters that hide section markers by the project/shared parameter "Назначение вида" through RevitCodexBridge. Use when the user asks to hide sections from another discipline or package on a view, add a filter to a view template such as "КМД3. Планы", or filter Sections where "Назначение вида" is not the current discipline/package.
---

# Revit Hide Sections By View Purpose

## Workflow

Use repository scripts from the repo root. Run Revit bridge commands sequentially; parallel `/command` calls can be rejected with `ExternalEvent was not accepted: Pending`.

1. Check bridge and document:

```powershell
.\scripts\Test-Bridge.ps1
.\scripts\Wait-Document.ps1
```

2. Inspect the active view, its template, visible section/view markers, and `Назначение вида` values:

```powershell
$code = Get-Content -Raw .\.codex\skills\revit-hide-sections-by-view-purpose\scripts\inspect-visible-section-markers.cs
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

3. Determine whether filters should be applied to the view template or active view settings:

```powershell
$code = Get-Content -Raw .\.codex\skills\revit-hide-sections-by-view-purpose\scripts\resolve-filter-target.cs
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

4. Probe the negative purpose filter in a rollback transaction:

```powershell
$keepPurpose = 'КМД3'
$keepPurposeB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($keepPurpose))
$template = Get-Content -Raw .\.codex\skills\revit-hide-sections-by-view-purpose\scripts\probe-section-purpose-filter.template.cs
$code = $template.Replace('__KEEP_PURPOSE_B64__', $keepPurposeB64)
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

5. Apply the real filter after the probe succeeds:

```powershell
$templateName = 'КМД3. Планы'
$keepPurpose = 'КМД3'
$filterName = 'КМД3. Скрыть разрезы не КМД3'
$templateNameB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($templateName))
$keepPurposeB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($keepPurpose))
$filterNameB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($filterName))
$template = Get-Content -Raw .\.codex\skills\revit-hide-sections-by-view-purpose\scripts\apply-section-purpose-filter.template.cs
$code = $template.Replace('__TEMPLATE_NAME_B64__', $templateNameB64).Replace('__KEEP_PURPOSE_B64__', $keepPurposeB64).Replace('__FILTER_NAME_B64__', $filterNameB64)
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

6. Verify the filter exists on the actual target and `visibility` is `false`:

```powershell
$filterName = 'КМД3. Скрыть разрезы не КМД3'
$filterNameB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($filterName))
$template = Get-Content -Raw .\.codex\skills\revit-hide-sections-by-view-purpose\scripts\verify-section-purpose-filter.template.cs
$code = $template.Replace('__FILTER_NAME_B64__', $filterNameB64)
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

## Model-Specific Notes

- `scripts/apply-section-purpose-filter.template.cs` modifies the model. Inspect and run the rollback probe first.
- Prefer a negative rule when the user wants only the current package to remain visible: `Назначение вида != <target-purpose>`.
- In the observed project, `Назначение вида` has parameter id `146439` and string values like `КМД3` and `ВК`.
- Do not assume the parameter id is universal across projects. If the id is missing or the rule probe fails, inspect elements and shared parameters again before applying changes.
- `OST_Viewers` can appear in visible marker collectors, but use `OST_Sections`/category id `-2000200` for the view filter.
- A view template can leave `V/G Overrides Filters` uncontrolled. In API terms, `new ElementId(BuiltInParameter.VIS_GRAPHICS_FILTERS)` is present in `template.GetNonControlledTemplateParameterIds()`. In that case, add and hide the filter on the view itself.

## Safety Rules

- Use repository scripts and RevitCodexBridge; do not call Revit API from an external process directly.
- Make Revit changes only inside a `Transaction`.
- Use rollback probes for category/parameter/filter API uncertainty.
- Do not save, synchronize, close, or overwrite the model unless the user explicitly asks.
