# Repository Instructions

## Orientation

Resolve the current checkout with `git rev-parse --show-toplevel`; run `git status --short` and `git log --oneline -5`. No fixed C: checkout directory is required.

Follow [docs/README.md](docs/README.md), then [PRODUCT_CONCEPT.md](PRODUCT_CONCEPT.md) and [architecture and validation](docs/ARCHITECTURE_AND_VALIDATION.md). Read only the additional route needed for the task.

Source and generated manifests own implementation/version facts. Distinguish source, compiled artifacts, installed binaries and published packages. Never infer current publication from a source version or an old local installer.

## Product And Ownership

Raw Buffer Visualizer is a C# 2D image/buffer debugger centered on one docked Visual Studio window. Camera control, acquisition, PLC/I/O, recipe execution and 3D inspection are outside this product.

Reuse the current owner. Do not reopen a completed structural change without a requirement, reproducible defect or changed dependency boundary. Read the owner map before changing debugger transfer, rendering, storage or packaging.

## Compatibility And Validation

- Read the VSIX manifest for the installation contract. Supported targets are VS2022 17.9+ x64 and stable VS2026 18.x; exact-host verification must name the tested binary and host. Do not claim VS2019, 32-bit or Preview/Insiders compatibility.
- Before using a VSIX, compare its four `netstandard2.0` Core/SDK/ObjectSource/deps payloads against the fresh routed build. `scripts/Publish-VisualStudioExtension.ps1` owns these hash and registration checks. Presence alone does not prove freshness.
- Classic loading is one contract: `Extension` candidate location, the Classic DLL's full assembly identity (version/culture/token included), the `Microsoft.VisualStudio.DotnetCustomVisualizer` manifest asset, both self-targeted attributes in `ClassicVisualizerRegistration.cs`, and the four fresh `netstandard2.0` payloads. Preserve all five parts. VS indexes installation paths by `UI type + "-" + Assembly.FullName`; a short assembly name causes `KeyNotFoundException` even when every DLL is present. Read the identity from the installed Classic DLL, never hard-code a version or recreate lookup keys from short names in tests. `ResolveAssembly` does not supply the debuggee ObjectSource probe path; never substitute `Debuggee` location or copy DLLs into the IDE's global Visualizers directory as a fix. Read the [loading contract and regression command](docs/ARCHITECTURE_AND_VALIDATION.md#classic-objectsource-loading-contract) before changing registration, assembly IDs, SDK references or packaging. Run `Test-VisualizerAssemblyPath.ps1` with the real VS decoder and the focused Classic tests; when checking an installed failure, also pass the actual IDE attribute cache read-only. A build, file-presence check or manually reconstructed registration map is insufficient.
- Preserve complete native reads, validated layout bounds, explicit pointer lifetimes and cancellation/session admission. An address is not object identity.
- Use the focused checks in the documentation. Keep source checks, unit tests, actual UI interaction and installed-IDE verification distinct. Never silently install or restart to obtain evidence.
- Keep generated test data on D: when available, otherwise an explicit isolated fallback. Respect the workstation monitor rule when launching desktop test windows.
- Consult [vendor policy](docs/vendor-sdk-license-policy.md) before any proprietary SDK work.

## UI And Documentation

Before changing UI layout, text or workflow: describe the problem, show the proposed layout, obtain explicit approval, then implement. Preserve previously approved scope. Review README-visible images before changing or publishing them; do not add private desktop content or represent old screenshots as a new binary's proof.

Keep public documentation factual: product behavior, build instructions, contracts and licenses. Keep internal plans, review logs, work contracts and machine-specific artifacts outside the public change set. Preserve them separately before removing public references. Use the source version and an explicit date for verification snapshots.

## External Actions

Editing, committing, branch pushing, tagging, publishing and installing are separate authorization boundaries. Generic continuation does not authorize external communication. Before a push, present the exact changed-file list, commit message, account, repository, branch, visibility and triggered workflows. Preserve local changes until their ownership and scope are established.
