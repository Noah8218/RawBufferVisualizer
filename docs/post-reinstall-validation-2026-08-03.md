# Windows Reinstall Functional Revalidation - 2026-08-03

This record covers the first full project recheck after Windows was reinstalled and the workspace was restored, followed by the owner-approved `1.0.53` corrective work. It identifies what still works, what was corrected, and the final installed-runtime evidence.

This record does **not** replace [release-qualification-1.0.52.md](release-qualification-1.0.52.md). Marketplace now serves that exact immutable candidate. The separate-PC `1.0.50 -> 1.0.52` update remains unverified post-publication.

## Scope And Acceptance Criteria

Included:

- restored development utilities and Visual Studio instances;
- source restore, Release build, aggregate self-tests, compatibility checks, packaging, and registration audit;
- current-source viewer/layout, preview replacement, Buffer Doctor, memory-soak, and sampled-preview evidence;
- actual Visual Studio 2022 and Visual Studio 2026 command/ToolWindow/debugger checks;
- a documented utility matrix, reviewed in-product environment-check proposal, and approved `1.0.53` implementation.

Excluded:

- Marketplace publication of the new `1.0.53` development candidate;
- any Marketplace write; this audit performs public read-back verification only;
- the mandatory separate-PC update from exact public `1.0.50`;
- vendor camera SDK/hardware certification;
- an exhaustive repeat of every historical manual scenario where a current deterministic test already covers the same contract.

Acceptance requires every invoked check either to pass or to be recorded with a reproducible failure and corrective action. Exact-public `1.0.52` and final-candidate `1.0.53` installed checks completed in VS2022 and VS2026. The surviving boundary is the already-recorded separate-PC `1.0.50 -> 1.0.52` Marketplace profile-migration check, which is outside this restored-PC audit.

## Restored Environment

| Item | Detected state | Assessment |
| --- | --- | --- |
| Git | `2.55.0.windows.3` | Available |
| Windows PowerShell | `5.1.26100.8875` | Available; repository script baseline |
| .NET SDK | `8.0.421`, `8.0.423`, `9.0.316`, `10.0.302` | Available; .NET 8 baseline present |
| Visual Studio 2022 Community | `17.14.37516.0`, complete and launchable | Supported |
| Visual Studio 2026 Community | `18.8.12023.21`, complete and launchable | Supported stable target |
| .NET desktop workload | Detected in both Visual Studio instances | Available |
| VSSDK workload component | Not returned by `vswhere -requires Microsoft.VisualStudio.Component.VSSDK` | Not required by the passing CLI restore/build; recommended only for IDE-centric extension work |
| `vswhere.exe` | Visual Studio Installer path present | Available |
| `VsixPublisher.exe` | Restored under `Microsoft.VSSDK.BuildTools\17.14.2094` | Available for dry-run/release tooling |
| FFmpeg | Not found | Acceptable; optional demo-only utility |
| Winget | Available | Optional manual installer transport |
| Test storage | `D:` available | Evidence stored under the required D-drive root |

See [development-prerequisites.md](development-prerequisites.md) for the reusable role matrix and recovery commands.

## Diagnostic Candidate

The post-reinstall source was packaged to prove that a fresh machine can restore and build the project:

```text
Version: 1.0.52.0
Source HEAD: acc467f8e03fee58c37464d883d3a6ddd447d13d
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 1,902,521 bytes
SHA-256: 95395489AF7427B8CE6C15BE8E9236C21B87153C1C340BF3B960D100523E79C8
```

This newly built package is diagnostic evidence only. It is not the immutable public 1,902,513-byte package with SHA-256 `3DD78167E60BB7DCC4C3AC1EE83622DEBFF75CEFC2D040977F1D854E33EB9E1F`, and it must not be substituted into exact-package or profile-migration qualification.

The original pre-reinstall `D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.52` runtime-evidence directories were not recovered. Marketplace supplied an exact 1,902,513-byte public copy with the recorded hash. After verifying the source and target paths and refusing overwrite, that byte-identical public copy was restored to the canonical candidate path. This recovers the immutable binary only; it does not recreate the missing historical logs or screenshots.

## Local 1.0.53 Candidate

```text
Version: 1.0.53.0
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 1,911,715 bytes
SHA-256: EB94CCE2144E1325FDFDB2DF8A63C383F9B4E82DFD2F8EF504CAEECB99220534
Entries: 94
Embedded notes: Raw Buffer Visualizer 1.0.53
```

An initial incremental package was rejected because its generated internal manifest remained `1.0.52.0` despite `1.0.53` source. After a clean rebuild, the internal manifest is `1.0.53.0`. The bump script now invalidates cached generated manifests/VSIX files, and the publish script rejects source/generated/packaged version mismatches. Actual VS2022 review then exposed two candidate defects: the host file version included a `built by` suffix that was not parsed, and the panel Close action was clipped in a compact dock. Both are corrected in the final hash above. That exact candidate was installed into VS2022 instance `17.0_f2675563` and VS2026 instance `18.0_19923728`; each installed manifest and both product assemblies hash-match the candidate payload.

```text
VS2022 installed folder: C:\Users\USER\AppData\Local\Microsoft\VisualStudio\17.0_f2675563\Extensions\gvv4a2c1.e5f
VS2026 installed folder: C:\Users\USER\AppData\Local\Microsoft\VisualStudio\18.0_19923728\Extensions\xvplqz1z.vem
RawBufferVisualizer.VisualStudio.dll: 68,096 bytes; SHA-256 C6700E9BD6302A99A787544B74686D0CDBE04ECC78783B0FEF00E02FCEEE9B4D
RawBufferVisualizer.VisualStudio.Vssdk.dll: 194,560 bytes; SHA-256 66637E25B7D46147E6ABBB4D262D9C7EA6B1AFF87A55EF33C1AAAEB9A7878FE5
```

## Essential-Only Environment Follow-Up

Owner feedback after the candidate review narrowed the in-product panel to the three checks that an extension user actually needs: the Visual Studio host, the loaded Raw Buffer Visualizer registration, and writable temporary storage. The .NET SDK, VSSDK workload, and FFmpeg remain documented development or demo utilities in [development-prerequisites.md](development-prerequisites.md), but no longer appear as product setup requirements. The Environment toolbar control is now a toggle; selecting it again closes the panel, so the redundant in-panel Close button and all installer/download actions were removed.

The preserved `EB94CCE...20534` candidate above predates this feedback. It remains historical evidence and must not be uploaded as the current Environment behavior. The corrected current-source package used only for this follow-up installation and UI verification is:

```text
Source baseline: bb23756 plus the uncommitted essential-only Environment follow-up
Version: 1.0.53.0
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\essential-only-toggle\current-source-vsix\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 1,907,942 bytes
SHA-256: 297A01104993CB7C524EE418759FDF0EAB41D07A0766A018B1A475F793219CF9
VS2022 installed folder: C:\Users\USER\AppData\Local\Microsoft\VisualStudio\17.0_f2675563\Extensions\i00kqfbx.m2v
VS2026 installed folder: C:\Users\USER\AppData\Local\Microsoft\VisualStudio\18.0_19923728\Extensions\d4t0qvg1.cnd
RawBufferVisualizer.VisualStudio.dll: 63,488 bytes; SHA-256 D0BF16316FF16112AA7199681EB94D75F873B226E2837AEDF5A79B12B9E1B33E
RawBufferVisualizer.VisualStudio.Vssdk.dll: 190,464 bytes; SHA-256 BDA11BFE35A958358C03781994D4093443C049CF05361E54AC1788496D61F784
```

Both installed manifests report `1.0.53.0`, and both installed product assemblies hash-match the corrected VSIX payload. VS2022 Wide and VS2026 Compact were exercised on active leftmost monitor `\\.\DISPLAY1`, bounds `0,0,1920,1080`: opening showed exactly the three required Ready rows plus Refresh and Copy; no optional-tool text or installer action was present; selecting Environment again removed the panel and exposed `Environment check closed` through the ToolWindow status. Both IDEs closed normally without a save prompt.

## Panel Toggle Consistency Follow-Up

The next owner-approved follow-up made **What's New** follow the same panel contract as **Environment**. Before the edit, a fresh VS2026 capture proved that selecting What's New a second time left the release banner open. Current source uses the existing release handler with a ToggleButton: the first selection opens the banner, a repeated selection closes it without changing the saved release-seen preference, and **Dismiss** closes the banner, clears the toggle state, and persists the release-seen preference. The narrow-layout **Inspector** control was already a toggle and required no change; ordinary action controls such as Open, Clear, Save, Fit, and Scan Now remain one-shot commands rather than panels.

The newest current-source package used only for this follow-up installation and UI verification is:

```text
Source baseline: bb23756 plus the uncommitted essential-only Environment and panel-toggle consistency follow-ups
Version: 1.0.53.0
Path: D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\current-source-vsix\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 1,908,045 bytes
SHA-256: 5EC07758A9592F9F47C7479B005E205AA255A60D6F1400586A37F5B3B8024C23
VS2022 installed folder: C:\Users\USER\AppData\Local\Microsoft\VisualStudio\17.0_f2675563\Extensions\vxty5daa.2d4
VS2026 installed folder: C:\Users\USER\AppData\Local\Microsoft\VisualStudio\18.0_19923728\Extensions\nmzdwcrf.z0i
RawBufferVisualizer.VisualStudio.dll: 63,488 bytes; SHA-256 8AB612A6D69B0561D5D5411B1792823E8E66E1C20C33A0777C8287903A6FD636
RawBufferVisualizer.VisualStudio.Vssdk.dll: 190,976 bytes; SHA-256 95B424C494CA28E71B52BF1FB4C69933DCF3EDDED7C667DD3F29A629C0E0FB65
```

Both installed manifests report `1.0.53.0`, and both installed product assemblies match the VSIX payload by length and SHA-256. VS2022 Wide and VS2026 Compact were exercised on the active leftmost monitor `\\.\DISPLAY1`, bounds `0,0,1920,1080`: What's New opened on the first selection, closed on the second selection, reopened, and then closed with its checked state cleared by Dismiss. The fixed dark ToolWindow theme showed the existing normal, hover/focus, checked, and unchecked states without a platform-default visual leak. Both IDEs closed normally without a save prompt.

## Functional Matrix

| Area | Result | Evidence or boundary |
| --- | --- | --- |
| Repository recovery | Pass | `main` at `acc467f`; only the pre-existing excluded `.tmp/` and three root helper scripts are untracked |
| NuGet restore and Release solution build | Pass | Fresh restored toolchain built the solution with no errors |
| Aggregate self-tests | Pass | Existing workspace, handoff, source, descriptor, and viewer suites completed |
| Release communication and package guard | Pass | Version/payload/package registration checks passed for the diagnostic build |
| Installed registration audit | Pass | Diagnostic `1.0.52.0` registration checked in VS2022 and VS2026; one Open command and one Scan command were observed |
| Legacy Bitmap/OpenCvSharp/Emgu compatibility matrix | Pass | Compatibility scripts completed after restore |
| Preview-first/full replacement | Pass | One preview replaced by one full document; a missing full replacement was rejected without losing the preview contract |
| Docked 540/900/1160 layouts | Pass | Fit/manual aspect and center checks, hover/pin, error report, and recovery passed at all three widths |
| Buffer Doctor panel | Pass | Padded Mono8 diagnosis and descriptor application produced current-source captures |
| Docked cleanup/memory soak | Pass | 240 opens with Delete/Clear; no positive managed/private/working-set, GDI, USER, or test-directory growth |
| Warm dense 100k/200k sampled preview | Pass | 100k `148 ms`; 200k `243 ms`; both below `5,000 ms` |
| First-access dense 100k/200k sampled preview after correction | Pass | Page-budgeted pointer previews produced 122x122/62x62 output. In the final regression run, first source access in the benchmark process was `992 ms`/`692 ms`; sample stage `950 ms`/`632 ms`. The script records that Windows file cache was not flushed or claimed as controlled. |
| Automatic Vision Inspector harness seam | Pass | `ActiveDocumentForDiagnostics` provides a narrow current-workspace seam; `-VerifyDiagnosticSeamOnly` passed against the current Release assembly. Full visual layout execution remains part of the installed/current-source UI checkpoint. |
| Smart Type Mapper VSSDK discovery | Pass | `-VerifyVssdkDiscoveryOnly` selected restored `Microsoft.VSSDK.BuildTools\17.14.2094\tools\vssdk`; historical `17.9.3168` is no longer required. |
| Historical Environment Check candidate | Pass, superseded | The preserved `EB94CCE...20534` package passed its then-approved required/optional panel, report, external-link confirmation, compact Close, and installed-runtime checks. Owner feedback later superseded only that panel scope; the package remains historical and is not the current publication candidate. |
| Essential-only Environment follow-up | Pass | Aggregate and static contracts prove exactly three runtime rows, no optional-tool/install surfaces, a ToggleButton open/close contract, temp write/delete, privacy-bounded report, build-suffixed host parsing, and no scan/open/install side effects. Installed current-source UI passed in VS2022 Wide and VS2026 Compact; repeated Environment selection closed the panel and reported `Environment check closed`. |
| Panel toggle consistency follow-up | Pass | Release communication contracts prove What's New initialization, repeated-selection close, and Dismiss state synchronization. The prior installed VS2026 behavior was captured before editing; the matching current-source package then passed first-open, repeated-button close, reopen, and Dismiss in VS2022 Wide and VS2026 Compact. Inspector was already a narrow-layout toggle. |
| Actual VS2022 workflow | Pass | Exact public `1.0.52` showed the ToolWindow, debugger break/Continue, automatic status, and registered visualizer availability. Exact final `1.0.53` then showed the corrected host suffix, `348.16`-pixel compact and `990 x 695` wide panels, visible compact Close, report copy, debugger break/Continue, automatic inspection, and the installed `1.0.53.0` registration state. |
| Actual VS2026 command/ToolWindow/debug workflow | Pass | Exact public `1.0.52` showed the ToolWindow, debugger break/Continue, and the local-window `Alt+Down` visualizer-list registration cue. Exact final `1.0.53` then showed the same compact/wide panel and action behavior, host `18.8.12023.21 built by: stable`, debugger break/Continue, the local `args` visualizer cue, automatic inspection, and installed `1.0.53.0` registration state. |
| Public Marketplace read-back | Pass | Gallery reports `1.0.52.0`; public VSIX is 1,902,513 bytes and SHA-256 `3DD78167...EB9E1F`; rendered Overview is the 1.0.52 document |
| Historical release evidence preservation | Partial | Exact candidate binary recovered from the matching public asset; original pre-reinstall VS2022/VS2026 runtime folders are absent, so the historical Markdown record is the remaining durable summary |
| Separate-profile `1.0.50 -> 1.0.52` update | Unverified | No eligible separate PC currently running exact public `1.0.50`; local reinstall and public asset equality do not prove profile migration |

The first VS2026 F5 attempt briefly reported that project details were unavailable during initial NuGet/project loading. The same IDE then completed a successful build and reached the breakpoint on retry. This is recorded as a first-load observation, not as a reproduced product defect.

## Evidence

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\candidate
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\marketplace
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\marketplace\public-1.0.52.vsix
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\perf\docked-memory-soak\docked-memory-soak.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\perf\sampled-preview-warm\sampled-preview-performance.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\perf\sampled-preview-page-budget\sampled-preview-performance.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\buffer-doctor
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\docked-layout-widths\layout-widths.json
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\before-host-version-fix-vs2022.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\after-host-version-fix-vs2022.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\final\vs2022-compact-final.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\final\vs2022-wide-final.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\final\vs2022-debug-break-final.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\final\vs2026-compact-final.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\final\vs2026-wide-final.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\final\vs2026-debug-break-final.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\essential-only-toggle\before-user-current.png
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\essential-only-toggle\after-vs2022-wide-open.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\essential-only-toggle\after-vs2022-wide-closed.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\essential-only-toggle\after-vs2026-compact-open.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\environment-check\essential-only-toggle\after-vs2026-compact-closed.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\before-vs2026-whats-new-open.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\before-vs2026-second-click-still-open.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\after-vs2022-wide-open.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\after-vs2022-wide-closed.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\after-vs2022-wide-dismissed.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\after-vs2026-compact-open.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\after-vs2026-compact-closed.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency\after-vs2026-compact-dismissed.jpg
D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\preview-first-handoff\preview-first-handoff.json
```

Earlier restored-workstation evidence used active leftmost monitor `\\.\DISPLAY2`, bounds `-1920,365,1920,1080`. During the final public/candidate recheck the topology was dynamically re-read as a single active `\\.\DISPLAY1`, bounds `0,0,1920,1080`, working area `0,0,1920,1032`. VS main-window captures at approximately `5-6,1-2,1908,1025` and floating wide captures at `5,0,990,695` intersected that display. UI input stopped whenever an unrelated foreground application made the target uncertain.

## Approved Corrections - Implementation Status

The owner approved the reviewed correction set and Environment Check mockup.

1. Complete: Automatic Vision Inspector now uses the `ActiveDocumentForDiagnostics` seam; the seam-only validation passed.
2. Complete: Smart Type Mapper now discovers the newest restored VSSDK build-tools directory; discovery selected `17.14.2094`.
3. Complete: sampled-preview output records map, view, pointer acquisition, sample, cleanup, first/repeat access, drive, and source metadata. The sample stage was the dominant cold path, so large pointer-backed previews now use a bounded page estimate without changing the five-second threshold.
4. Complete: Environment Check, build-suffixed host-version handling, compact-safe action row, privacy report, confirmation-only external actions, release communication, and unit/static contract tests are implemented in `1.0.53`.
5. Complete installed runtime: exact public `1.0.52` passed in VS2022 and VS2026. The final-hash `1.0.53` candidate then passed installed compact/wide Environment Check and debug-registration checks in both IDEs. Both IDEs were stopped and closed normally; no process was force-terminated and no unsaved state was discarded.
6. Complete follow-up: owner feedback removed non-runtime utilities and external actions from the product panel, converted Environment to a same-control open/close toggle, updated release/development communication, and passed current-source installed UI checks in VS2022 Wide and VS2026 Compact. The preserved candidate in item 5 was not overwritten.
7. Complete panel consistency follow-up: What's New now opens and closes from the same toggle, while Dismiss additionally saves the release-seen preference and clears the checked state. Static contracts, the Release build, aggregate tests, matching installed payloads, and actual VS2022 Wide/VS2026 Compact interactions passed.

## Historical In-Product Environment Check Mockup

The mockup below records the initially approved candidate and is retained as change history. The later essential-only follow-up above supersedes its optional-tool rows, installer actions, and separate Close button.

Review: normal users have no extra runtime utility to install. A silent "install everything" feature would therefore add risk and present contributor/media tools as product dependencies. If approved, the useful feature is a read-only **Environment** panel that diagnoses the current Visual Studio/extension state and opens only official installers or download pages for genuinely missing items.

Proposed placement and flow:

```text
+--------------------------------------------------------------------------------+
| Raw Buffer Visualizer     Open  Clear  Save  Fit  1:1  Inspector  What's New   |
|                                                                    Environment |
+--------------------------------------------------------------------------------+
| Environment Check                                                             |
| [✓] Visual Studio 2026 18.8 x64          Supported                             |
| [✓] Raw Buffer Visualizer 1.0.52          Registered                           |
| [✓] Temporary storage                    Writable                              |
|                                                                                |
| Optional contributor/media tools                                               |
| [!] .NET 8 SDK                         [Open official download]                |
| [!] Visual Studio workload             [Open Visual Studio Installer]          |
| [!] FFmpeg (demo media only)            [Open installation guide]              |
|                                                                                |
| [Refresh]  [Copy diagnostic report]  [Close]                                   |
+--------------------------------------------------------------------------------+
```

Behavior contract:

- Opening, refreshing, or closing the panel does not scan a frame, open an image, install software, change the input image, or modify VSPackage registration.
- Required runtime checks appear before optional contributor/media tools.
- Install actions require confirmation and only open Visual Studio Installer or an official page; they do not download or execute installers silently.
- The diagnostic report excludes secrets and image payloads and warns that local paths should be reviewed before sharing.
- The panel uses the product's existing fixed dark ToolWindow visual system. The product does not currently claim an adaptive light ToolWindow theme. Compact `348.16`-pixel and floating wide `990 x 695` states were verified in both supported IDE generations; existing deterministic layout tests cover the intermediate widths.

The owner initially approved this mockup. The preserved `EB94CCE...20534` candidate implements it, while current source implements the later essential-only toggle behavior. The current public `1.0.52` package contains neither version of the feature.

## Durable Closure

Status: Complete
Scope: Post-Windows-reinstall recovery, approved harness/performance corrections, essential-only `1.0.53` Environment toggle, consistent What's New toggle/Dismiss behavior, deterministic source/package tests, current-source installation, and VS2022 Wide/VS2026 Compact evidence
Acceptance criteria: Original restore/build/core/preview/soak and exact-package gates passed; essential-only Environment and What's New source/static contracts pass; corrected current-source VSIX installed with matching payload in both IDEs; each installed UI closes Environment and What's New on repeated selection, while Dismiss closes What's New and clears its checked state
Verification: Release solution build; aggregate self-tests; release communication; Environment Check contract test; package and installed-assembly hash comparison; actual installed VS2022 Wide and VS2026 Compact open/repeated-close/reopen/Dismiss interactions on the active leftmost monitor
Evidence: historical candidate SHA-256 `EB94CCE2144E1325FDFDB2DF8A63C383F9B4E82DFD2F8EF504CAEECB99220534`; intermediate essential-only development VSIX SHA-256 `297A01104993CB7C524EE418759FDF0EAB41D07A0766A018B1A475F793219CF9`; newest panel-consistency development VSIX SHA-256 `5EC07758A9592F9F47C7479B005E205AA255A60D6F1400586A37F5B3B8024C23`; UI evidence under `D:\OpenVisionLab-TestData\RawBufferVisualizer\post-reinstall-2026-08-03\ui\panel-toggle-consistency`
Boundary / next dependency: This completes the requested product/UI follow-up but does not replace the canonical `1.0.53` release candidate or publish Marketplace content. Before any upload, choose whether to retain `1.0.53` or bump again, then produce and qualify a new immutable candidate from the committed follow-up source. The separate-PC exact-public `1.0.50 -> 1.0.52` profile-migration debt also remains external.
