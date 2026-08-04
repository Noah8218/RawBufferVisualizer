# Connect Your Buffer (Smart Type Mapper) Design

Status: Phases 1–5 and the Connect Your Buffer current-source and installed-VSIX workflows are Complete in Compact and Wide layouts. Real pointer-backed **Open Variable** remains a separate verification boundary.
Last updated: 2026-08-04.

## Goal

Let a user inspect an image object of a type Raw Buffer Visualizer does not know — a company SDK frame class, a vendor wrapper, an internal struct — by mapping its members once, without writing code and without rebuilding the extension.

```csharp
public sealed class CompanyFrame
{
    public IntPtr ImageAddress { get; set; }
    public int SizeX { get; set; }
    public int SizeY { get; set; }
    public int LinePitch { get; set; }
    public CompanyPixelType PixelType { get; set; }
}
```

After one mapping, `List<CompanyFrame>` opens through the existing collection visualizer, and individual variables open through a new docked-window entry point.

Product sentence: "We cannot support every company's type — so any type can be mapped once by the user."

## Hard Constraint: The Visualizer Icon

The DataTip/Watch magnifier icon appears only for target types registered in `extension.json` **at build time**. There is no public API to register target types at runtime, and the mapping file is read by our extension, not by Visual Studio.

Therefore this feature does **not** promise "an icon appears for any type". The realistic promises are:

- Collections (`List<>`, `Dictionary<,>`, `object[]`, arrays) already carry the icon via open-generic registration; mapped types open automatically **inside** those collections.
- Individual variables open via a new **Open Variable** entry in the docked window (EnvDTE expression evaluation, phase 5).
- Workaround documented for today: wrap the variable in an array (`new[]{ frame }`) to get the collection icon.

`System.Object` as a visualizer target is not a supported path (would attach the icon to every variable; unproven whether VS accepts it; no precedent in this codebase).

## User Scenarios

### Scenario 1 — first encounter (one-time mapping)

1. Break in the debugger; `List<CompanyFrame>` has the visualizer icon; user clicks it.
2. Entries fail shape detection (`ImageAddress`, `SizeX`, ... are not in the built-in name candidates) and appear as red error rows: "Unsupported type Company.Vision.CompanyFrame".
3. The error row offers **Connect Your Buffer**. The object source has already attached a **member inventory** (member name, CLR kind, sample value) to the error payload — reading fields is side-effect free.
4. The mapping dialog lists detected members with heuristic pre-selection (IntPtr → Data, names containing Width/SizeX → Width, Pitch/Stride → Stride, enum → Pixel Format). The user adjusts dropdowns.
5. Enum mapping: each detected enum value is mapped to a `RawPixelFormat` (`Mono12 → Mono12PackedLsb`).
6. **Preview** renders a thumbnail immediately: the member values (pointer + dimensions) are already known, and the existing process-memory source can read the live debuggee buffer — no debugger round-trip needed.
7. **Save Mapping** writes the mapping file. **Use Suggested Roles** resets only the visible draft, and **Copy RawBufferView Template** copies optional neutral starter code for pointer-backed data without saving or scanning.

### Scenario 2 — afterwards (automatic)

- Next debug session, same collection: the object source loads the mapping file, matches the full type name, extracts the configured members, and entries open as images with no error rows.
- Individual variable: user opens the docked window → **Open Variable** → types `frame` or picks a recent expression → mapped types open immediately; unmapped types open the mapping dialog.

### Scenario 3 — team sharing

- Mapping files are resolved in this order: solution-local `.rawbuffervisualizer.json` (committable to source control) → `%APPDATA%\RawBufferVisualizer\type-mappings.json` (per user).
- Export/import is just copying the JSON file.

## Mapping File Schema (v1)

```json
{
  "version": 1,
  "mappings": [
    {
      "typeName": "Company.Vision.CompanyFrame",
      "assemblyName": "Company.Vision",
      "members": {
        "data": "ImageAddress",
        "width": "SizeX",
        "height": "SizeY",
        "stride": "LinePitch",
        "bufferLength": null,
        "pixelFormat": "PixelType",
        "validBits": null,
        "bitDepth": null
      },
      "pixelFormatMap": {
        "Mono8": "Mono8",
        "Mono12": "Mono12PackedLsb",
        "Bgr": "BGR24"
      },
      "byteOrder": "LittleEndian"
    }
  ]
}
```

Rules:

- `data` may name an `IntPtr`/`UIntPtr` member or a managed array (`byte[]`, `ushort[]`, `float[]`); array-backed entries transfer via the existing chunked path, pointer-backed entries can use live process memory.
- `bufferLength` optional: derived from stride/height/format when absent.
- `typeName` + `assemblyName` match is exact (case-sensitive type, assembly simple name). No wildcards in v1.
- Unknown schema versions are ignored with a visible error row, never a crash.

## Safety Rules (member reading)

Auto-detection and mapped extraction read only:

- fields (public and non-public);
- property getters (the debugger already func-evals these; documented in the dialog);
- enum values;
- constant arithmetic on the above (e.g. `Width * 2` — phase 2, not v1).

Never invoked: arbitrary methods (`GetBuffer()`, `ConvertImage()`). Method-call expressions are out of scope for v1; if added later they require an explicit per-mapping user opt-in checkbox recorded in the file.

## Component Changes

### ObjectSource (`RawBufferVisualizer.VisualStudio.ObjectSource`, netstandard2.0, runs in debuggee)

1. `TypeMappingStore`: loads and caches mapping files (both locations, file-timestamp invalidation). File I/O in the debuggee is acceptable and matches existing snapshot behavior.
2. Generalize `ImagePtrVisualizerTransfer`: the existing name-candidate `FindMember`/`GetMemberValue`/`ConvertValue<T>` helpers already work by member name; a mapping simply supplies the names. Built-in heuristic detection stays as the fallback.
3. On shape-detection failure for a collection entry, attach a **member inventory** (name, kind, sample value preview, enum values when applicable) to the error metadata so the docked window can offer Connect Your Buffer without a second debugger round-trip.
4. Mapped extraction failures (member missing, wrong type, null pointer) become error rows with the reason, never silent drops (product rule: fail visibly).

### Docked window (`RawBufferVisualizer.VisualStudio.Vssdk`)

1. Error rows with a member inventory get a **Connect Your Buffer** action.
2. Mapping dialog: role dropdowns over the inventory, enum mapping grid, explicit Preview, Save Mapping, Use Suggested Roles, optional RawBufferView template copy, and Cancel. It follows the compact IDE UX rule and remains a dialog from the error row, not a new persistent pane.
3. Save → write mapping file → offer "retry this entry" (re-invokes the collection provider path via a new handoff request).

### Phase 5 — Open Variable (individual variables)

- New docked-window command: expression input + recent-expression list.
- Uses EnvDTE `Debugger.GetExpression` (reference already in the Vssdk csproj; no usage code exists today) to read member values of the current stack frame.
- Mapped type → open directly; unmapped type → member inventory from the expression → same mapping dialog.
- This is the only supported individual-variable entry; the icon limitation is stated in docs.

## Phases

| Phase | Scope | Size |
| --- | --- | --- |
| 1 | Mapping schema v1, `TypeMappingStore`, both storage locations | small |
| 2 | ObjectSource mapped extraction + failure member inventory | medium |
| 3 | Mapping dialog + preview + save/retry | medium |
| 4 | Error-row Connect Your Buffer action + collection retry flow | small |
| 5 | Open Variable via EnvDTE (individual variables) | medium-large |

Phases 1-4 deliver the majority of the value through the collection path. Phase 5 completes the individual-variable UX.

Buffer Doctor connection: the mapping dialog's Preview reuses the Diagnose Buffer candidate list when the preview looks broken, and an accepted interpretation is stored into `pixelFormatMap`/stride for that type.

## Tests

1. Mapping file round-trip (serialize/deserialize, version tolerance).
2. Resolution order: solution-local wins over `%APPDATA%`.
3. ObjectSource: a synthetic `CompanyFrame`-shaped test class mapped via file extracts pointer/width/height/stride/format correctly; managed-array `data` member works.
4. Enum mapping: `CompanyPixelType.Mono12 → Mono12PackedLsb` produces the right descriptor.
5. Failure inventory: unmapped type produces an error payload containing the member inventory.
6. Missing/renamed member after a mapping → visible error row, no crash.
7. UI smoke: mapping dialog opens from an error row in the docked harness; preview renders from a synthetic live source.
8. Template generation: mapped and derived stride/length forms are deterministic, UIntPtr is converted explicitly, and no proprietary SDK name is built in.
9. State/side-effect smoke: saved selections and byte order restore; reset does not save; copy does not save or open an image; explicit Save remains the persistence boundary.

## Documentation Impact On Release

- README describes Connect Your Buffer as the no-code mapping route, its storage scopes, optional template prerequisite, and lack of camera SDK/FFmpeg runtime dependency.
- Marketplace copy may say "Map unsupported image classes once" only with the icon and entry-route constraints stated plainly.
- Handoff: resolve the long-standing ImagePtr wording gap by documenting mapper-based support as the generic answer; the exact `Cressem.ImageModel.ImagePtr` registration remains a compatibility exception.

## Verification Record

Status: Complete

Scope: Installed-VSIX automatic fallback for one unregistered pointer-backed type whose structure is recognized but whose `Mono12` value requires an explicit packed-format decision.

Acceptance criteria:

- the unregistered type remains a visible `MappingRequired` candidate instead of being guessed or hidden -> passed at 88% confidence;
- selecting `Mono12PackedLsb` renders a preview from live debuggee memory -> passed;
- saving writes the inferred member roles and enum value mapping -> passed;
- the automatic scanner prefers the saved mapping and reopens the same value as 640 x 484, stride 960, `Mono12PackedLsb`, live source -> passed;
- the final automatic session contains zero errors -> passed;
- a pre-existing `%APPDATA%\RawBufferVisualizer\type-mappings.json` is restored after the smoke -> passed; SHA256 remained `CE235B3350E239B208B8830ED0F7BBE77CB4B4FFD5E44D6B73F8E701CBF403FF`.

Verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\SmokeInstalledVsixNewFeatures.ps1 -Scenario SmartTypeMapper -Configuration Release -NoBuild -NoInstall
```

Evidence:

- `artifacts/ui/installed-vsix-new-features/SmartTypeMapper-installed-vsix.json`
- `artifacts/ui/installed-vsix-new-features/SmartTypeMapper-session.json`
- `artifacts/ui/installed-vsix-new-features/smart-type-mapper-automatic-before-map.png`
- `artifacts/ui/installed-vsix-new-features/smart-type-mapper-dialog-preview.png`
- `artifacts/ui/installed-vsix-new-features/smart-type-mapper-automatic-after-reopen.png`

Boundary / next dependency: This proves the Automatic Vision Inspector-to-mapper recovery path for the supplied simulated `UnmappedCompanyFrame`. It does not prove every real SDK object, method-only buffer API, native lifetime rule, or vendor pixel-format enum.

## Connect Your Buffer Current-Source Verification — 2026-08-04

```text
Status: Complete
Scope: Vendor-neutral Connect Your Buffer dialog, explicit preview, suggestion reset, mapping save/reopen, optional pointer-backed RawBufferView template copy, and dark IDE control states.
Acceptance criteria: Selected roles and BigEndian save/reopen -> pass; dialog open/reset/copy leave the mapping file and image list unchanged -> pass; preview remains explicit -> pass; generated mapped/derived stride and length code plus UIntPtr conversion and no built-in proprietary SDK text -> pass; normal/focus/hover/open-popup/disabled/scroll/status UI states -> pass.
Verification: dotnet build .\RawBufferVisualizer.sln --configuration Release -> pass with 18 pre-existing ImageTypeRecognizer VSTHRD010 warnings; dotnet run --project .\tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj --configuration Release --no-build -> pass; .\scripts\SmokeSmartTypeMapper.ps1 -Configuration Release -NoBuild -> pass.
Evidence: D:\OpenVisionLab-TestData\RawBufferVisualizer\connect-your-buffer-2.0\before and D:\OpenVisionLab-TestData\RawBufferVisualizer\connect-your-buffer-2.0\final. The desktop smoke used leftmost monitor \\.\DISPLAY2, bounds X=-1920,Y=365,Width=1920,Height=1080; host window bounds Left=-1900,Top=385,Right=-740,Bottom=905.
Boundary / next dependency: These are current-source WPF view captures and harness behavior, not installed-VSIX evidence. Managed-array mappings use Save Mapping but intentionally do not generate RawBufferView pointer code. The optional template requires an application reference to RawBufferVisualizer.Sdk; Save Mapping remains the dependency-free route.
```

## Connect Your Buffer Installed-VSIX Verification - 2026-08-04

```text
Status: Complete
Scope: Install the current 1.0.53.0 development VSIX into VS2022 and VS2026, save an inferred mapping, start a fresh IDE/debuggee session, restore every role, copy neutral RawBufferView starter code, exercise suggestion reset and Cancel, and reopen the saved mapping through the normal Compact Inspector path.
Acceptance criteria: VSIX payload equals both installed extensions -> pass for five package-owned files; initial mapping/preview/save/reopen -> pass on VS2022 17.14.37516.0 and VS2026 18.8.12023.21; fresh-process persisted auto-open -> pass at 640 x 484, stride 960, Mono12PackedLsb, live source, zero errors; nine restored choices -> pass; 630-character RawBufferView template with no Basler/Pylon/Spinnaker/Vimba/IDS peak text -> pass; Copy/Use Suggested Roles/Cancel leave the mapping file unchanged -> pass; Compact Inspector > Interpret shows a visible Edit Mapping action and reopens the saved choices without forcing Wide layout -> pass on both IDEs.
Verification: Release build -> pass with 18 pre-existing ImageTypeRecognizer VSTHRD010 warnings and zero errors; self-tests -> pass; current-source Smart Type Mapper UI smoke -> pass; installed SmartTypeMapper and SmartTypeMapperPersisted -> pass on both IDEs; final package comparison -> pass; pre-test user mapping state -> restored after every run.
Evidence: package C:\Git\RawBufferVisualizer\.build\bin\RawBufferVisualizer.VisualStudio.Extensibility\Release\net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix, size 1,914,617 bytes, SHA-256 ADDC416CDA4F5DB68410BFA352628217D2B1D737BF3449B20D48A47DA751B6C1; before capture D:\OpenVisionLab-TestData\RawBufferVisualizer\connect-your-buffer-installed\VS2022\persisted\SmartTypeMapperPersisted-failure.png; current-source evidence D:\OpenVisionLab-TestData\RawBufferVisualizer\connect-your-buffer-compact\current-source; final installed evidence D:\OpenVisionLab-TestData\RawBufferVisualizer\connect-your-buffer-compact\installed-final. Desktop runs used leftmost monitor \\.\DISPLAY2 at -1920,365,1920,1080.
Boundary / next dependency: This proves the synthetic pointer-backed UnmappedCompanyFrame path and the packaged dark-theme Compact/Wide controls, not proprietary SDK objects or physical camera/board hardware. Real pointer-backed Open Variable remains separately bounded.
```
