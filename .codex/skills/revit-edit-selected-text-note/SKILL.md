---
name: revit-edit-selected-text-note
description: Edit the currently selected Revit TextNote through RevitCodexBridge. Use when the user asks to rewrite, correct spelling, improve wording, expand bullet points, renumber instructions, translate, or otherwise modify a selected text block in an open Revit document.
---

# Revit Edit Selected Text Note

## Workflow

Use repository scripts from the repo root. Run Revit bridge commands sequentially; parallel `/command` calls can be rejected with `ExternalEvent was not accepted: Pending`.

1. Check bridge and document:

```powershell
.\scripts\Test-Bridge.ps1
.\scripts\Wait-Document.ps1
```

2. Inspect the current selection and confirm exactly one `TextNote` is selected:

```powershell
$code = Get-Content -Raw .\.codex\skills\revit-edit-selected-text-note\scripts\inspect-selected-text-note.cs
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

3. Draft the replacement text before changing the model.

4. Apply the text with UTF-8 base64 transfer. This avoids PowerShell or HTTP encoding corruption for Cyrillic and multiline text.

```powershell
$txt = @'
NEW TEXT HERE
'@.TrimEnd("`r", "`n")
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($txt))
$template = Get-Content -Raw .\.codex\skills\revit-edit-selected-text-note\scripts\update-selected-text-note.template.cs
$code = $template.Replace('__TEXT_B64__', $b64)
.\scripts\Invoke-RevitCSharp.ps1 -Code $code
```

5. Inspect the returned `text` field. If it contains `?` characters instead of Cyrillic, do not continue with normal string literals; reapply using the UTF-8 base64 pattern above.

## Text Editing Guidelines

- Preserve technical meaning and standards references such as GOST numbers, system names, abbreviations, and model-specific terminology.
- For Russian construction notes, prefer concise imperative wording.
- Fix spelling, punctuation, and numbering. Convert lettered inserts like `2a` into the normal sequence when the user asks to fix numbering.
- Expand terse items into complete instructions, but do not invent project-specific requirements that are not implied by the original text or user request.
- Keep exclamation marks only for prohibitions or safety-critical warnings already present in the source.
- Keep line breaks simple: heading line, then numbered lines separated by `\r`/newlines.

## Safety Rules

- `scripts/update-selected-text-note.template.cs` modifies the model. Inspect selection first and do not run it unless the user requested an edit.
- Modify the document only inside a Revit `Transaction`.
- Do not save, synchronize, close, or overwrite the model unless the user explicitly asks.
- If the selection is empty, contains multiple elements, or is not a `TextNote`, report that clearly and ask the user to select the target text block.
