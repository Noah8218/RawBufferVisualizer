# Release Qualification: 2.0.0

Last verified: 2026-08-06 KST.

## Release State

Visual Studio Marketplace still serves public `1.0.53.0`. The current local `2.0.0.0` candidate below now passes the complete consolidated local release matrix on the same exact bytes: source/build guards, five Emgu and five OpenCvSharp versions, package/build/install identity, Break-to-Continue safety, Connect Doctor, and installed regressions on Visual Studio 2022 and Visual Studio 2026.

Matching source/evidence is integrated on `origin/main` at `0d3ac2011c1bfd5104645e1d16cdf72f9eae9b26`, and GitHub Actions run [#31059894280](https://github.com/Noah8218/RawBufferVisualizer/actions/runs/31059894280) succeeded. Qualification, source push, CI, clean installation, and the exact public `1.0.53.0 -> 2.0.0.0` in-place update gate are complete. Explicit owner approval remains required before upload. This record does not claim that Marketplace serves 2.0.0 and does not authorize upload, tag, GitHub Release creation, or public readback.

## Current Consolidated Local Release Candidate

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-connect-doctor-docs-20260806\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Manifest version: 2.0.0.0
Size: 1,923,731 bytes
SHA-256: D65C8B559A0E5C4A62FCDDEAE345A625DC76F71C4C9FE19BDB0DDE180EDEFC4C
Marketplace extension ID: RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f
Source basis: origin/main 0d3ac2011c1bfd5104645e1d16cdf72f9eae9b26; GitHub Actions 31059894280 succeeded
Disposition: qualification, source push, CI, and pre-publish clean/update gate complete; hold upload until the owner explicitly approves
```

The feature stays inside the existing Connect Your Buffer dialog. **Diagnose interpretation** toggles a ranked result list; selecting a candidate changes the visible member-role draft and preview only, and only **Save Mapping** persists. Current width/height/stride format alternatives are retained in the top-eight visible set without altering their original scores. An interpretation that cannot be represented by the current members may preview, but Save is blocked with an explicit mismatch reason.

### Consolidated Installed Identity And Runtime

| IDE | Exact host | Installed extension root | Consolidated result |
| --- | --- | --- | --- |
| Visual Studio 2022 Community | `17.14.37516.0`, x64, instance `f2675563` | `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_f2675563\Extensions\xoxlqjuj.zve` | Seven assemblies match; P0 Break-to-Continue, Connect Doctor, Buffer Doctor, Automatic Collections, Multi-Library Hybrid, and Environment Check passed |
| Visual Studio 2026 Community | `18.8.12023.21`, x64, instance `19923728` | `%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_19923728\Extensions\m4sjmomx.z2w` | Same package equality and all six installed scenarios passed |

Both runs used the active leftmost monitor `\\.\DISPLAY2`, bounds `-1920,365,1920,1080`, with verified Visual Studio window rectangle `Left=-1900, Top=385, Right=-20, Bottom=1425`. Each run found exactly one Open command and one Scan command, logged zero package protocol errors, and restored the pre-test user mapping state.

Consolidated evidence:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\consolidated-local-release-verdict.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\candidate-build-installed-hashes.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\installed-vs2022
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\installed-vs2026
```

## Preserved P0 Safety Baseline

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-p0-20260805\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Manifest version: 2.0.0.0
Size: 1,917,791 bytes
SHA-256: 3C2DCC1E9E38990D1C17547331E15C5EE344ABEA07D3936B722747B0670AE7EE
Marketplace extension ID: RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f
Source basis: origin/main 3d88894 plus the current P0 worktree change set
```

Do not overwrite this candidate or the superseded candidate. The later source change uses the separately named consolidated candidate above. The P0 package remains the exact full-safety evidence baseline, but it no longer represents current source and must not be uploaded as the current product.

## Superseded Pre-P0 Candidate

```text
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\candidate-frozen\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 1,914,538 bytes
SHA-256: 2A6D94016B03430BDF2EF5ECCF6282D32896C02AEFB3A9EB8F5519AFE4B13512
Disposition: historical evidence only; do not upload
```

The preserved candidate passed the earlier package, release-panel, registered/mapped, and dual-IDE matrix. On 2026-08-06 KST it was reinstalled as the closest reproducible pre-change UI baseline. VS2022 showed `640x484 Mono8 live 1 tiles` at Break and continued to show the same live status after the debuggee exited. That result is the defect baseline, not release evidence for the current candidate.

## P0 Safety Scope

The P0 change set closes three safety boundaries without adding another pane or vendor-specific adapter:

1. Descriptor arithmetic and enum validation use checked bounds and reject undefined pixel-format/byte-order values before allocation, transfer, or rendering.
2. `ProcessMemoryRawImageSource` owns an idempotent lifetime cancellation boundary; disposal prevents another debuggee read, including a read already crossing the process-memory boundary.
3. Run Mode invalidates all live process-backed documents, cancels progressive work, keeps only already-rendered pixels visible, and marks the rows `Unavailable`. A break-generation gate rejects delayed handoffs queued before Continue and does not revive them in the next Break session.

Copied managed-array documents are intentionally not invalidated because their bytes are already owned by the Visual Studio process.

## Consolidated Installed Package Identity

| IDE | Exact host | Installed extension root | Result |
| --- | --- | --- | --- |
| Visual Studio 2022 Community | `17.14.37516.0`, x64, instance `f2675563` | `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_f2675563\Extensions\xoxlqjuj.zve` | Manifest/registration validation passed; seven Raw Buffer Visualizer assemblies match the exact candidate and current Release build by length and SHA-256 |
| Visual Studio 2026 Community | `18.8.12023.21`, x64, instance `19923728` | `%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_19923728\Extensions\m4sjmomx.z2w` | Manifest/registration validation passed; the same seven assemblies match the exact candidate and current Release build by length and SHA-256 |

The installer logged expected exit code `1002` while attempting to remove an absent legacy split ToolWindow extension, then completed the hybrid-package install and metadata validation. No registry repair or `/ResetSkipPkgs` was used.

`candidate-build-installed-hashes.json` proves 28 exact equality comparisons for Core, SDK, OpenGL canvas, shared Visual Studio, ObjectSource, VSSDK ToolWindow, and Extensibility assemblies across candidate, current build, and both installations. A fresh publish produced the same 94 extracted files after normalizing the generated `extensionDir` value in `catalog.json` and `manifest.json`; the outer ZIP hash differs because VSIX generation assigns a fresh extension directory/archive identity and is evidence only, not a replacement candidate.

## Installed Runtime Matrix

The same `--smart-type-mapper-debug` debuggee scenario was used in both IDEs.

| Check | VS2022 | VS2026 |
| --- | --- | --- |
| Break and automatic inspection | Pass: `8 detected: 6 opened, 1 need mapping, 1 failed` | Pass: same result |
| Active live image before Continue | Pass: `640x484 Mono8 live 1 tiles` | Pass: same result |
| Continue/process-exit invalidation | Pass: five live documents became `Unavailable`; status `Live source unavailable` | Pass: same result |
| Copied managed-array document | Pass: `companyArrayFrame`, `64 x 48 Mono8`, remained available | Pass: same result |
| Delayed handoff session boundary | Deterministic self-test passed | Deterministic self-test passed; same binary |
| Connect Doctor | Pass: draft-only candidate selection/preview, second-click close, save/reopen, zero errors | Pass: same result |
| Buffer Doctor | Pass: five bounded candidates and applied preview | Pass: same result |
| Automatic Collections | Pass: five opened, two isolated failures, duplicate-free rescan, preference restored | Pass: same result |
| Multi-Library Hybrid | Pass: nine documents, zero errors | Pass: same result |
| Environment toggle regression | Pass: three runtime `[Ready]` rows; second click closed | Pass: same result |
| Monitor placement | Pass: window `(-1900,385,-20,1425)` intersects leftmost `\\.\DISPLAY2` `(-1920,365,1920,1080)` | Pass: same bounds |

The invalidated live rows were `parameterFrame`, `mono8Owner`, `bgr24Owner`, `companyFrame`, and `nestedCompanyFrame`. The one copied row was intentionally preserved. Each canonical `AutomaticVisionInspector` result required exactly one package-log entry containing `Run mode entered; invalidated 5 live source(s)` and zero protocol errors.

The after images retain the last successfully rendered pixels by design. The authoritative state is the accessible row summary and `Live source unavailable` status; the viewer does not read the disposed debuggee source again.

## Automated Validation

| Check | Result | Evidence |
| --- | --- | --- |
| Release solution build | Pass, 0 warnings / 0 errors in the final build | `solution-build.log` |
| Aggregate self-tests | Pass | `self-tests.log` |
| Descriptor overflow/undefined-enum gates | Pass through aggregate self-tests | `tests/RawBufferVisualizer.Tests/Program.cs` |
| Disposed live-source read rejection | Pass; the test failed before the implementation and passed afterward | `self-tests.log` |
| Handoff Break-generation gate | Pass for Continue and next-Break non-revival | `tests/RawBufferVisualizer.Tests/WorkspaceAndHandoffCoordinatorTests.cs` |
| Legacy compatibility | Pass: Emgu CV `3.4.3`/`4.2.0`/`4.5.5`/`4.8.1`/`4.13.0` and OpenCvSharp `4.0.0`/`4.2.0`/`4.5.5`/`4.8.0`/`4.13.0` | `legacy-compatibility.log` |
| VSIX publish/structural guards | Pass | `publish-structural-guards.log` |
| Candidate/current-build/installed assembly equality | Pass, seven of seven in both IDE profiles | `candidate-build-installed-hashes.json` |
| Re-published extracted content | Pass, 94 files equal after normalizing generated `extensionDir` only | `candidate-republish-normalized-comparison.json` |
| Diff whitespace | Pass | `git diff --check` |

Release communication validation is rerun after this document update and must remain green before handoff.

## Evidence Paths

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\consolidated-local-release-verdict.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\solution-build.log
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\self-tests.log
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\legacy-compatibility.log
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\publish-structural-guards.log
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\candidate-build-installed-hashes.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\candidate-republish-normalized-comparison.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\installed-vs2022
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806\installed-vs2026
```

Fresh installed screenshots were visually inspected for both IDE generations. The automatic-inspection captures show the same rows before Continue and the unavailable live-source state afterward; the Connect Doctor captures show matching themed ranked rows, draft-only status, and preview. Earlier failed VS2026 automation folders remain diagnostic evidence only and are excluded from `consolidated-local-release-verdict.json`.

## Pre-Publish Clean And In-Place Update Gate

The unchanged candidate passed the final pre-publish gate on 2026-08-06 KST using VS2022 Community `17.14.37516.0`, instance `f2675563`, on the active leftmost monitor `\\.\DISPLAY2` at `-1920,365,1920,1080`.

| Check | Result |
| --- | --- |
| Clean install | Pass: exact candidate installed after an explicit clean reinstall; Marketplace payload check, Automatic Inspector, Automatic Collections, and Multi-Library Hybrid passed |
| Exact baseline identity | Pass: public `1.0.53.0` VSIX SHA-256 `E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3`; seven of seven installed package assemblies matched |
| In-place update | Pass: exact `2.0.0.0` candidate installed over the public baseline without `-Reinstall`, registration repair, `/ResetSkipPkgs`, or baseline uninstall |
| Updated package identity | Pass: one VS2022 extension manifest at `2.0.0.0`; seven of seven installed assemblies match candidate SHA-256 `D65C8B559A0E5C4A62FCDDEAE345A625DC76F71C4C9FE19BDB0DDE180EDEFC4C` |
| Automatic Inspector | Pass: 6 opened, 1 mapping required, 1 isolated failure; Continue invalidated five live sources and retained the copied array |
| Automatic Collections | Pass: 5 opened, 2 isolated failures, duplicate-free, preference restored |
| Multi-Library Hybrid | Pass: 9 documents, 0 errors, Open command 1, Scan command 1, package-protocol errors 0 |
| Viewer interactions | Pass: 540/900/1160 layouts retained Fit aspect and margin; 1:1, wheel zoom, pan, and Manual resize state passed |

The first two updated Multi-Library Hybrid automation attempts timed out because the harness did not identify the tiny Locals Visualizer control and used a fallback coordinate (`viewElementFound=False`). Both attempts still opened all eight automatic documents with zero product errors. A fresh manual-assisted run selected the same Bitmap Visualizer control, received the explicit ACK, and completed with nine documents and zero errors. The failed attempts remain preserved as harness-reliability evidence; no product repair or reinstall was used between them and the passing run.

Evidence:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\prepublish-update-gate-20260806
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\prepublish-update-gate-20260806\clean-install
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\prepublish-update-gate-20260806\in-place-update
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\prepublish-update-gate-20260806\in-place-update-retry
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\prepublish-update-gate-20260806\in-place-update-manual\MultiLibraryHybrid-installed-vsix.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\prepublish-update-gate-20260806\layout-interactions\layout-widths.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\portfolio-captures-20260806\selected
```

## Durable Closure

Status: Complete

Scope: P0 descriptor/enum validation, live process-memory disposal, Run Mode document invalidation, delayed-handoff session gating, Connect Doctor, exact candidate packaging, ten-version compatibility, and installed VS2022/VS2026 qualification.

Acceptance criteria: invalid descriptor arithmetic and undefined enums fail before transfer -> pass; disposed live sources reject another read -> pass; Continue invalidates all five live documents while retaining the copied array -> pass on both IDEs; delayed pre-Continue handoff cannot open in Run or a later Break -> pass; candidate/current-build/installed assemblies match -> pass; Release build, aggregate tests, 10-version legacy matrix, and package guards pass -> pass.

Verification: commands and installed matrix above; exact results are stored under `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-2.0.0\qualification-connect-doctor-docs-20260806`.

Evidence: exact consolidated candidate identity, `consolidated-local-release-verdict.json`, package/build/install equality reports, automated logs, canonical installed result JSON, and fresh UI captures listed above.

Boundary / next dependency: Marketplace remains public `1.0.53.0`. Qualification, source push at `0d3ac20`, CI run `31059894280`, clean installation, and exact public `1.0.53.0 -> 2.0.0.0` in-place update are complete. Explicit owner upload approval, publication, public update/readback, tag, and GitHub Release remain separate actions. Direct proprietary SDK work remains blocked by the vendor-license gate.

## Connect Doctor Durable Closure

Status: Complete

Scope: Existing-dialog Connect Doctor toggle, bounded ranked candidates, current-geometry format retention, accessible/themed candidate rows, draft-only candidate application, exact Save gate, reset/cancel/unavailable behavior, exact packaging, and installed VS2022/VS2026 verification.

Acceptance criteria: repeated toggle closes results -> pass; candidate selection updates draft/preview without saving -> pass; unrepresentable candidate Save is blocked -> pass; reset/cancel/unavailable paths have no persistence side effect -> pass; packed Mono12 candidate remains visible -> pass; aggregate tests/build/relevant UI smokes -> pass; exact package assemblies match both installations -> pass; installed mapping save/reopen -> pass on both IDEs with zero errors.

Verification: commands, candidate identity, installed matrix, and consolidated evidence paths above.

Evidence: 1,923,731-byte consolidated candidate, SHA-256 `D65C8B559A0E5C4A62FCDDEAE345A625DC76F71C4C9FE19BDB0DDE180EDEFC4C`; D-drive qualification root; source tests and smoke scripts.

Boundary / next dependency: Complete for the neutral synthetic wrapper and the consolidated release matrix, including source push, CI, clean install, and in-place update. It does not certify a proprietary SDK or physical camera/board. Explicit owner approval, publication, and public update/readback remain separate actions.
