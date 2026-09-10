# AGENTS.md

This repository is maintained with Codex assistance. Keep changes practical, verified, and focused on the Raw Buffer Visualizer user workflow.

## New Session Orientation

The canonical working repository is the active Git worktree that contains this file. On this machine the current 2.x worktree is `C:\Git\RawBufferVisualizer_VSIX\RawBufferVisualizer_vs17.9_compat`. Do not treat `C:\Documents\RawBufferVisualizer`, an attachment directory, or a missing historical path as the source of truth.

Before implementation, release work, or documentation changes in a new conversation:

1. Resolve the worktree with `git rev-parse --show-toplevel`, then run `git status --short` and `git log --oneline -5` there.
2. Read [docs/README.md](docs/README.md) for the document map.
3. Read [docs/MAINTAINER_HANDOFF.md](docs/MAINTAINER_HANDOFF.md) for the current version, release state, completed work, known gaps, and next priority.
4. Read [docs/PRODUCT_DIRECTION_AND_ROADMAP.md](docs/PRODUCT_DIRECTION_AND_ROADMAP.md) before changing product scope or UX.
5. Read [docs/ARCHITECTURE_AND_VALIDATION.md](docs/ARCHITECTURE_AND_VALIDATION.md) before changing debugger transfer, viewer rendering, compatibility, packaging, or smoke tests.

If repository evidence differs from the handoff, trust the repository and current external state, then update the handoff in the same change. Do not copy old version numbers forward without checking the VSIX manifest, public Marketplace version, and latest CI result.

The product is an Image Watch-style C# machine-vision debugger visualizer centered on one docked Visual Studio window. Vision Replay Debugger, camera control, acquisition orchestration, PLC/I/O, and recipe execution are separate products and remain out of scope. Rendering implementation names are internal details and must not appear in user-facing copy.

## Visual Studio Compatibility Contract

- Keep shipped and development claims explicit. Exact Marketplace readback on 2026-09-04 confirms public `2.0.7.0`; that package is superseded because its `netstandard2.0` debugger ObjectSource is stale. Local `2.0.8.0` corrects the package path, passed exact-artifact qualification including Visual Studio 2022 `17.9.34902.65`, and remains a candidate until the owner authorizes each external step. Both retain Visual Studio 2022 `17.9+` plus stable Visual Studio 2026 `18.x` support targets.
- The former 2.0.7 feature qualification is invalidated for the Visual Studio 2022 direct debugger-host path by error `RBV-ERROR-20260904005014-8DA38D99`. Record the incident in [docs/release-qualification-2.0.7.md](docs/release-qualification-2.0.7.md) and the correction in [docs/release-qualification-2.0.8.md](docs/release-qualification-2.0.8.md). Reinstalling the same 2.0.7 VSIX is not remediation.
- The VSIX manifest range `[17.9,18.0)` remains the compatibility contract. Visual Studio 2026 uses the lower API-version bound for VSIX compatibility, supports Visual Studio API version 17.x, and ignores the upper bound. Any manifest, embedded release communication, or package-content change requires a new exact-package qualification.
- For every VSIX build, compare SHA-256 for `netstandard2.0/RawBufferVisualizer.Core.dll`, `RawBufferVisualizer.Sdk.dll`, `RawBufferVisualizer.VisualStudio.ObjectSource.dll`, and `RawBufferVisualizer.VisualStudio.ObjectSource.deps.json` against the same fresh routed Release output. Any mismatch is a release blocker; file presence and source-level tests are not substitutes for byte equality.
- Do not claim Visual Studio 2019, 32-bit Visual Studio, Preview/Insiders builds, or an untested exact VS2026 minor version as verified. Stable VS2026 `18.x` is a supported compatibility target; record the exact installed build only after the installed Tool Window, menu, debugger handoff, Automatic Inspector, and registered visualizer paths pass.
- Before publishing a new release, run the installed-VSIX matrix on current serviced VS2022 and stable VS2026 when both environments are available. If one environment is unavailable, retain the support target but state the missing runtime qualification in the release record and Marketplace copy.
- Before installing into VS2026, inspect both the per-user `18.0_<instance>\Extensions` root and the per-machine `Common7\IDE\VSExtensions` root for the Raw Buffer Visualizer extension ID. A migrated per-machine historical build must be removed or updated through Visual Studio Manage Extensions/Installer with administrator rights; never delete its `Program Files` directory manually or hide the conflict with a second extension ID.
- Authoritative Microsoft compatibility reference: https://learn.microsoft.com/en-us/visualstudio/extensibility/migration/extension-compatibility?view=visualstudio

## README Image Gate

Images used in `README.md`, Marketplace copy, or any first-impression GitHub documentation must pass a visual review before being committed or pushed.

Checklist:

- Capture from the current built version, not an old or unrelated run.
- Verify the screenshot visually before publishing.
- Do not include unrelated program UI, desktop clutter, file explorers, unrelated Visual Studio panes, chat tools, or private user data.
- Crop documentation screenshots to the Raw Buffer Visualizer surface unless surrounding Visual Studio context is intentionally needed.
- Confirm the screenshot shows the claimed feature accurately: loaded image, pixel values, diagnostics, large-image status, or error state.
- Keep before/after or review evidence under `artifacts/ui/` when replacing README images.
- If a screenshot fails review, regenerate or crop it before updating `README.md`.

For README-visible images, do not rely only on file existence or automated capture success. The image itself must be inspected.

## UI Change Review Gate

Before any UI, UX, layout, visible text, visual state, navigation, or workflow-affordance change, follow this gate. This gate is mandatory; do not implement a UI change without completing it.

1. **Review**: Describe the UI element to change and why the change is needed. Do not start implementation.
2. **Mock up**: Draw the target layout as an ASCII diagram or a simple sketch showing the new position, size, and relationship to existing controls. Do not write code.
3. **Confirm**: Wait for explicit user approval. If the user rejects or revises the mockup, repeat from step 1.
4. **Implement**: Only after approval, write the code exactly as approved.

This gate supersedes any direct request to "just make the change". When the user asks for a UI change, respond with the review description and the mockup first, and stop.
