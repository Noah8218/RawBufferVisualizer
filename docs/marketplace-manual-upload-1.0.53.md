# Raw Buffer Visualizer 1.0.53 Manual Marketplace Upload

> Publication completed and was read back on 2026-08-05 KST. Marketplace serves the exact VSIX and Overview recorded below. Retain this as the immutable upload record; do not upload another binary under version `1.0.53.0`.

This checklist records how the existing public Visual Studio Marketplace item was updated. It must not be used to create a new extension item or rebuild the qualified VSIX.

## Existing Item

```text
Marketplace item: openvisionlab.RawBufferVisualizer
Publisher ID: openvisionlab
Internal name: RawBufferVisualizer
Display name: Raw Buffer Visualizer
Public version verified on 2026-08-05 KST: 1.0.53.0
Gallery last updated: 2026-08-05T01:54:21.537Z
Visibility: Public
Price: Free
```

Public page: https://marketplace.visualstudio.com/items?itemName=openvisionlab.RawBufferVisualizer

Publisher page: https://marketplace.visualstudio.com/manage/publishers/openvisionlab

## Exact Files

### VSIX to upload

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\candidate-vendor-safe\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix
```

```text
Version: 1.0.53.0
Size: 1,914,615 bytes
SHA-256: E934F24A54F4D4265EA2A758E91A005FB58B84DD7B01CCF56F39F5610DCE8FA3
```

### English Overview to publish

```text
C:\Git\RawBufferVisualizer\docs\marketplace-overview-1.0.53.md
```

Replace the Marketplace Overview with the complete contents of this English file.

### Release Notes to publish

```text
C:\Git\RawBufferVisualizer\docs\marketplace-release-notes-1.0.53.md
```

Use the complete contents if the portal provides a release-notes field.

### Korean review copy — do not publish

```text
C:\Git\RawBufferVisualizer\docs\marketplace-overview-1.0.53.ko.md
```

This file is only for owner review. Do not paste it into the English Marketplace item.

## Upload Steps

1. Sign in to the publisher page above.
2. Open the existing **Raw Buffer Visualizer** item under publisher `openvisionlab`.
3. Select the existing item's edit or update action. Do not select **New extension**.
4. Upload the exact VSIX path above.
5. Confirm the portal reads version `1.0.53.0` before continuing.
6. Replace the Overview with the complete English Overview file.
7. Paste the Release Notes file if a separate release-notes field is available.
8. Keep the existing item public and free, then save/upload the update.
9. Wait for Marketplace processing and public-page propagation.

## Stop Instead Of Publishing If

- the portal shows any version other than `1.0.53.0`;
- the selected item is not `openvisionlab.RawBufferVisualizer`;
- the workflow is creating a second extension item;
- the selected file is a ZIP or a historical `1.0.51`/`1.0.52` VSIX;
- the VSIX size or SHA-256 differs from the values above;
- the item would become private, paid, or use another publisher.

## After Publication

Completed: the public page shows version `1.0.53.0`, the rendered `Raw Buffer Visualizer` Overview matches the English source after the Gallery's normal H1-marker removal, and the downloaded public VSIX matches the exact size and SHA-256 above.

Readback evidence:

```text
D:\OpenVisionLab-TestData\RawBufferVisualizer\release-1.0.53\marketplace-readback-20260805-110018
```
