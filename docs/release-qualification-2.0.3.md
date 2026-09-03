# Raw Buffer Visualizer 2.0.3 Release Qualification

## Status

`Complete` for the approved local 2.0.3 implementation, package, documentation, and installed-host verification scope. Owner review is the next gate. Commit, push, CI, Marketplace upload, public readback, tag, and GitHub Release were not performed.

## Qualified scope

- Direct bounded `ConcurrentDictionary<TKey,TValue>` debugger-visualizer registration on .NET Framework and current .NET.
- Dictionary keys retained as image-row names, supported entries opened independently, unsupported entries isolated, and the existing 256-entry transfer limit retained.
- Exact `Cressem.ImageModel.ImagePtr` first-use handoff with no prior Tool Window warm-up.
- Asynchronous Debugging-context package preload with a passive inbox listener. Package initialization arms the listener but does not open the Tool Window or scan the current frame.
- Product and VSIX version `2.0.3` / `2.0.3.0`.
- English Marketplace Overview, Korean owner-review copy, Marketplace release notes, changelog, README, release runbook, and checklist aligned to 2.0.3.
- Existing current industrial screenshots and GIFs retained because this patch does not change visible UI or the demonstrated workflows.

Excluded: camera acquisition/control, proprietary SDK redistribution, 3D/depth/point-cloud visualization, Visual Studio 2019, 32-bit Visual Studio, Preview/Insiders builds, commit/push, CI, publication, and public readback.

## Source state

```text
Repository: C:\Git\RawBufferVisualizer_vs17.9_compat
Branch: agent/vs2022-17.9-compat
Baseline commit: e236278f642ea061e7a49e32256ad0e953544084
State: local uncommitted 2.0.3 review candidate
Public Marketplace baseline checked on 2026-08-24: 2.0.2.0
```

## Exact review VSIX

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.3\20260824\candidate-final-20260824-1\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Length: 2,493,578 bytes
SHA-256: EADAF23808B1EE73DCDD061E8C37BA1D2DEBC56F902730A8F96B24A7FE936D99
Manifest version: 2.0.3.0
Installation range: [17.9,18.0)
CoreEditor prerequisite: [17.9,)
Entries: 72
```

The package guard verified the isolated hybrid ownership:

- `RawBufferVisualizer.VisualStudio.Vssdk.dll` and its `.pkgdef` own the in-process package, commands, menus, and Tool Window.
- `OutOfProc/RawBufferVisualizer.VisualStudio.Extensibility.dll` owns the debugger visualizer providers.
- `.vsextension/extension.json` contains seven provider contracts and three `ConcurrentDictionary` target occurrences: the open generic target plus the exact .NET Framework and current .NET registrations.
- The VSSDK package preloads asynchronously in the Debugging UI context, and initialization does not directly open or scan the Tool Window.
- The package contains one current package/menu/ToolWindow registration and rejects the retired package GUID.

Do not rebuild or substitute this VSIX after owner review. Any product source, manifest, embedded release-note, or package-content change creates new bytes and requires a new hash plus installed qualification.

## Verification

| Check | Result |
| --- | --- |
| PowerShell parse | Pass for package, installed-smoke, installed-update, Marketplace-publish, and release-communication scripts |
| Release solution restore/build | Pass; 0 errors. The final incremental build reported 0 warnings; candidate packaging also reported the 18 pre-existing `VSTHRD010` warnings in `ImageTypeRecognizer.cs` and 0 errors. |
| Aggregate `RawBufferVisualizer.Tests` | Pass, including the exact ImagePtr fixture and order-independent ConcurrentDictionary assertions |
| Legacy library matrix | Pass for .NET Framework Bitmap, five OpenCvSharp package versions, and five Emgu CV package versions |
| Release communication contract | Pass for `2.0.3` |
| Environment Check source/UI contract | Pass |
| VSIX publish/package guard | Pass |
| Installed payload validator | Pass on both profiles: version `2.0.3.0`, valid registration payload, no legacy split extension, no per-machine conflict, and no stale manual CodeBase key |
| Marketplace publisher dry run | Pass with the exact VSIX, English 2.0.3 Overview, and six current media assets; no upload executed |
| `git diff --check` | Pass |

The initial final-VSIX VS2026 ConcurrentDictionary automation attempt reached the breakpoint but its synthetic visualizer click was lost and no handoff was published. The package and ActivityLog contained no product error. An immediate retry with the same VSIX, coordinates, profile, and scenario passed; this is retained as test-automation evidence rather than counted as a product failure.

## Installed-host results

The same unchanged VSIX recorded above was installed and validated on both current stable hosts:

| Host | Scenario | Result |
| --- | --- | --- |
| Visual Studio 2022 Community `17.14.37516.0` (`419f0858`) | `ConcurrentDictionary` | Pass; key `[concurrent-snapshot]`, one `640 x 484 BGR24` snapshot, 0 errors, one Open command, one Scan command, 0 protocol errors |
| Visual Studio 2026 Community `18.8.12105.206` (`19923728`) | `ConcurrentDictionary` | Pass on retry; same collection result, command counts, and zero-error protocol result |
| Visual Studio 2022 Community `17.14.37516.0` (`419f0858`) | true-cold `ImagePtrColdStart` | Pass; Tool Window closed before invocation, one `640 x 484 BGR24` image, stride `1920`, 0 errors, `InboxWatcher` wake path |
| Visual Studio 2026 Community `18.8.12105.206` (`19923728`) | true-cold `ImagePtrColdStart` | Pass; same cold-start dimensions, format, stride, zero-error result, and `InboxWatcher` wake path |

Both desktop runs dynamically selected and verified the smaller left monitor `\\.\DISPLAY2`, bounds `Left=-1920, Top=365, Width=1920, Height=1080`. The Visual Studio window rectangle was `Left=-1900, Top=385, Width=1880, Height=1040` and intersected that monitor.

## Visual Studio 17.9 boundary

The package keeps the isolated `net472` VSSDK 17.9 in-process project, the `net8.0-windows8.0` out-of-process provider, manifest range `[17.9,18.0)`, CoreEditor prerequisite `[17.9,)`, and qualified `Microsoft.VisualStudio.Threading 17.9.0.0` contract. The previously installed Visual Studio 2022 Professional 17.9 test host was removed, so the exact 2.0.3 bytes were not run on 17.9. The support claim therefore rests on the unchanged 17.9 package boundary plus earlier runtime evidence; it is not an exact 2.0.3-on-17.9 runtime claim.

## Evidence

```text
VS2022 ConcurrentDictionary:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.3\20260824\exact-final-installed\vs2022-concurrent\ConcurrentDictionary-installed-vsix.json

VS2026 ConcurrentDictionary:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.3\20260824\exact-final-installed\vs2026-concurrent-retry\ConcurrentDictionary-installed-vsix.json

VS2022 true-cold ImagePtr:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.3\20260824\exact-final-installed\vs2022-imageptr-cold\ImagePtrColdStart-installed-vsix.json

VS2026 true-cold ImagePtr:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.3\20260824\exact-final-installed\vs2026-imageptr-cold\ImagePtrColdStart-installed-vsix.json

Marketplace dry run:
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.3\20260824\marketplace-dry-run\vs-publish.json
```

## Closure record

```text
Status: Complete
Scope: Local Raw Buffer Visualizer 2.0.3 candidate with direct ConcurrentDictionary visualization, first-use ImagePtr handoff repair, aligned English/Korean Marketplace copy, exact VSIX generation, and installed validation on the two currently installed stable IDEs.
Acceptance criteria: ConcurrentDictionary registered and bounded on .NET Framework/current .NET -> pass; exact ImagePtr works without Tool Window warm-up -> pass; version/VSIX/Overview/release notes agree on 2.0.3 -> pass; final VSIX package guards -> pass; exact final bytes installed and changed scenarios pass on VS17.14/VS18.8 -> pass; Marketplace dry run without publication -> pass.
Verification: Release build and aggregate self-tests, Bitmap/OpenCvSharp/Emgu compatibility matrix, source/package contracts, installed payload validation, exact installed ConcurrentDictionary and true-cold ImagePtr scenarios, and Marketplace dry run all passed as recorded above.
Evidence: Exact VSIX path/hash and D-drive result JSON, screenshots, package logs, ActivityLogs, and dry-run manifest listed above.
Boundary / next dependency: Owner review of the exact VSIX and English/Korean Overview is required before any commit, push, CI, Marketplace upload, tag, or release. Exact 2.0.3 runtime on Visual Studio 2022 17.9 remains unverified because that host is not installed.
```
