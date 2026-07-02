# RevitCodexBridge

RevitCodexBridge gives Codex access to a running Autodesk Revit session.

It is a small Revit 2025 add-in that opens a local bridge between an AI coding
agent and the active Revit application, project model, selected elements, and
views. With this bridge, Codex can inspect the current document, run Revit API
code in the proper Revit context, and help automate parts of the design process
that are usually repetitive, manual, or hard to standardize.

In practical terms, this repository is the connection layer that lets you bring
AI into Revit workflows.

## Why this exists

Revit is a powerful design environment, but many day-to-day project tasks are
still repetitive:

- checking model state before issuing drawings;
- renaming, filtering, or cleaning project data;
- creating annotations or view-specific graphics;
- validating that elements follow office or project rules;
- extracting quantities, geometry, or parameters for review;
- turning a one-off manual operation into a repeatable project command.

Codex can already write code. RevitCodexBridge makes that useful inside an open
Revit model by giving Codex a safe local route into the Revit API.

The bridge does not replace Revit, BIM standards, or engineering judgement. It
helps automate the mechanical parts of the work so designers and BIM specialists
can spend less time repeating the same operations by hand.

## What this repository is

This repository contains:

- a Revit add-in that starts a local bridge server;
- PowerShell scripts for installing, publishing, and testing the bridge;
- a command path for running small C# snippets inside Revit through the Revit
  API;
- a place for local Codex skills that describe repeatable automation workflows.

Once installed, Revit runs the bridge locally on your machine. Codex talks to
that bridge, and the bridge executes commands inside Revit using the correct
Revit API execution model.

## What this repository is not

This is not a finished automation package.

Apart from test and example commands, the repository does not ship ready-made
tools for your design processes. It gives Codex the access needed to build those
tools with you.

That distinction is important: every company, project, discipline, and model
standard is different. The useful automation usually has to be created around
your real workflows, naming rules, templates, shared parameters, view structure,
and QA process.

## How automation grows from this

The recommended workflow is:

1. Pick one repetitive Revit task.
2. Ask Codex to inspect the current model or view through the bridge.
3. Let Codex create a small script or command that performs the task.
4. Test it on the active model.
5. When the workflow becomes useful, save it as a local Codex skill in
   `.codex/skills/`.

Local skills are the best way to make automation repeatable. A skill can contain
the instructions, C# scripts, checks, and project-specific assumptions needed
for one workflow. After that, you can ask Codex for the same task in natural
language, and Codex can reuse the skill instead of rediscovering the process
from scratch.

For example, a local skill might describe how to:

- update selected text notes according to your documentation style;
- hide section markers from another discipline on a view template;
- create stair direction annotations in the current view;
- measure selected sweep paths;
- validate sheets before issue.

These skills live with the repository, so the automation process can be reviewed,
improved, versioned, and shared with the team like any other project code.

## Installation

Use the installer from this repository:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Install-Bridge.ps1 -ValidateOnly
.\scripts\Install-Bridge.ps1
```

Restart Revit after installation. Revit loads add-ins only at startup.

If Revit is installed in a custom location, pass the path explicitly:

```powershell
.\scripts\Install-Bridge.ps1 -RevitExe 'D:\Apps\Autodesk\Revit 2025\Revit.exe'
```

## Optional Revit SDK docs

Installing the official Revit SDK for the same Revit version is strongly
recommended when you plan to ask Codex for Revit API work. The SDK includes the
local `RevitAPI.chm` reference, which lets the agent search the API on disk
instead of spending time and tokens looking up documentation online.

Codex agents should handle extracting and searching the SDK docs inside the
project-local `APIdocs` folder. The folder itself is versioned so scripts have a
stable path, while the extracted SDK files inside it are ignored.

After downloading and installing the Revit SDK, send Codex a message like:

```text
The Revit SDK is installed at [path to SDK]. Please extract the API docs.
```

Example:

```text
The Revit SDK is installed at D:\Revit 2025.3 SDK. Please extract the API docs.
```

Official Autodesk Revit API developer overview:
https://aps.autodesk.com/developer/overview/revit-api

## Basic check

With Revit running and a model open, you can check that the bridge is available:

```powershell
.\scripts\Test-Bridge.ps1
.\scripts\Wait-Document.ps1
.\scripts\Get-DocumentInfo.ps1
```

These commands are mostly smoke tests. Real automation should be created around
your actual project tasks and then captured as local skills.

## Working with Codex

When asking Codex to work with Revit through this repository, describe the design
task in normal language:

> Check the current view for visible section markers from another package and
> create a repeatable skill that hides them through the view template.

or:

> Inspect the selected text note, rewrite it as a clear numbered instruction,
> update it in Revit, and save the workflow as a reusable local skill.

Codex should use the bridge to inspect the live Revit document, write small C#
commands when needed, test changes carefully, and avoid saving or synchronizing
the model unless explicitly asked.

Detailed operating rules for Codex agents are kept in `AGENTS.md`.

## Repository layout

- `BridgeApplication.cs` - the Revit add-in and local bridge implementation.
- `scripts/` - install, publish, test, and command helper scripts.
- `scripts/Search-RevitApiDocs.ps1` - local search helper for extracted Revit SDK API docs.
- `.codex/skills/` - local Codex skills for repeatable Revit workflows.
- `AGENTS.md` - technical instructions for AI agents working in this repository.

## License

See `LICENSE`.
