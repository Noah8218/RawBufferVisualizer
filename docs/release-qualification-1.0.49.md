# Release Qualification: 1.0.49

## Scope

This recovery candidate addresses the external `1.0.48` update failure where `View > Raw Buffer Visualizer` was visible but did not open the ToolWindow and debugger handoffs timed out.

## Candidate

```text
Version: 1.0.49.0
Path: C:\Git\RawBufferVisualizer\artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
Size: 1,990,882 bytes
SHA-256: 7C51F798544A09C23BA79A019EA0AA40F25B94A8BB9DF7F1F4B3599DF63E1516
Built: 2026-07-28 KST
```

## Change

- Marketplace extension ID remains `RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f`.
- VSPackage GUID changes from retired `{c15cc508-0fef-49bb-9478-4d2fdf9f87d2}` to `{1977574b-f107-465f-bfd1-5fc022907039}`.
- Package/VSCT/pkgdef ownership remains in `RawBufferVisualizer.VisualStudio.Extensibility`.
- View-command exceptions now display a message with the `package.log` path.

## Local evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| Release solution build | Pass | 0 errors; 18 existing `VSTHRD010` warnings |
| Core/self-test suite | Pass | `RawBufferVisualizer self-tests passed.` |
| PowerShell parser | Pass | Five changed release/install scripts parsed |
| Generated package registration | Pass | New Package GUID, installed-folder CodeBase, ToolWindow and menu entries |
| Retired Package GUID excluded | Pass | Packaging guard plus built-output search |
| Normal update over local 1.0.48 | Pass | Installed 1.0.49 without repair; payload validator passed |
| Automatic Vision Inspector | Pass | 6 opened, 1 mapping candidate, 1 isolated failure, duplicate-free |
| Multi-library hybrid | Pass on clean rerun | OpenCvSharp, Emgu, Bitmap, automatic wrappers; 9 documents, 0 errors |

The first MultiLibraryHybrid invocation immediately after the Automatic scenario timed out before the OpenCvSharp provider produced a handoff. A clean rerun passed. This is recorded rather than hidden; the external recovery test must include a registered Bitmap or Mat handoff.

Current-source screenshots:

```text
artifacts\ui\installed-vsix-new-features\automatic-vision-inspector.png
artifacts\ui\installed-vsix-new-features\multi-library-hybrid.png
```

The first Automatic screenshot accidentally captured another foreground application and was discarded; the scenario was rerun and the path now contains the verified Visual Studio 1.0.49 ToolWindow. A true before screenshot from the external failing PC is not available in this workspace.

## External outcome

The owner later confirmed that the affected Windows 10 PC became healthy after the extension was removed and public Marketplace `1.0.49` was installed again. The ToolWindow and handoff then operated normally.

This is a **clean-reinstall pass only**. It does not satisfy the original in-place update criterion below because uninstalling removed the stale installed state that the recovery gate was intended to exercise. It also does not qualify source `1.0.50`, Automatic Inspector on that PC, or Windows 10 DPI/Fit behavior.

## Original external acceptance gate

On the VS2022 PC/profile where Marketplace `1.0.48` failed:

1. Do not run the repair script or `/ResetSkipPkgs`.
2. Update to the exact SHA-256 candidate.
3. Restart Visual Studio.
4. Select `View > Raw Buffer Visualizer` and confirm the ToolWindow opens.
5. Invoke one registered Bitmap or Mat visualizer and confirm the handoff is acknowledged.
6. Run Automatic Inspector and confirm inferred images appear.

```text
Status: Incomplete
Scope: 1.0.49 package-identity recovery implementation, local validation, and affected-PC clean reinstall
Acceptance criteria: Local build/package/update/runtime checks passed; affected Windows 10 clean reinstall passed; original no-uninstall in-place recovery criterion was not exercised
Verification: Release build, self-tests, parser checks, package guard, local install/update smokes, Automatic and MultiLibrary installed-VSIX smokes, owner-reported external uninstall/reinstall result
Evidence: Candidate SHA-256, generated pkgdef, installed-VSIX result JSON/screenshots, and the 2026-07-29 owner report
Boundary / next dependency: This record must not be used as proof of an in-place Marketplace update; that gate moves to public 1.0.49 -> exact 1.0.50 on the affected Windows 10 PC
```
