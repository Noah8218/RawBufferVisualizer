# Raw Buffer Visualizer 2.0.1

Version `2.0.1` publishes the complete Raw Buffer Visualizer 2.0 feature set at a higher extension version so Visual Studio recognizes it as a Marketplace update.

## Fixed

- Raised the VSIX and product version from `2.0.0` to `2.0.1` while keeping the existing Marketplace extension identity.
- Prepared a newly built package for the normal Visual Studio update path.

## Included 2.0 Features

- **Diagnose interpretation** in Connect Your Buffer ranks format, stride, valid-bit, and byte-order alternatives without saving until **Save Mapping** is selected.
- Registered views, mapped pointers, and mapped managed buffers use one checked 2D buffer contract.
- Invalid dimensions, stride, length, enum values, arithmetic overflow, and incompatible valid bits fail before transfer.
- Continuing or exiting the debuggee marks live process-backed rows `Unavailable`; copied managed buffers remain available.

Visual Studio 2022 `17.14+` and stable Visual Studio 2026 `18.x` remain the supported x64 targets. After installing or updating, close every Visual Studio window and restart Visual Studio.
