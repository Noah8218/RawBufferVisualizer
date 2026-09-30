# Developer Documentation

## Start Here

1. Follow [development prerequisites](development-prerequisites.md) to restore and build on Windows.
2. Open `RawBufferVisualizer.sln`. For extension debugging, select `RawBufferVisualizer.VisualStudio.Extensibility` and its executable launch profile (`devenv.exe /RootSuffix Exp`). This launches a separate experimental IDE.
3. To debug an application with an already installed extension, select `RawBufferVisualizer.VisualizerDebuggee`, start debugging, and open an initialized image variable at a breakpoint. Startup-project choices are local Visual Studio settings and are not restored by Git.
4. Read the [product contract](../PRODUCT_CONCEPT.md), [architecture and test routes](ARCHITECTURE_AND_VALIDATION.md), and [repository rules](../AGENTS.md).

## Task Routes

| Task | Source |
| --- | --- |
| Configure another development PC | [Development prerequisites](development-prerequisites.md) |
| Find an owner or a focused test group | [Architecture and validation](ARCHITECTURE_AND_VALIDATION.md) |
| Understand current user-visible behavior | [README](../README.md), [changelog](../CHANGELOG.md) |
| Review image attribution | [Documentation image sources](industrial-image-testing.md) |
| Review dependency and vendor boundaries | [Third-party notices](../THIRD-PARTY-NOTICES.md), [vendor policy](vendor-sdk-license-policy.md) |
| Read the current product overview | [English](marketplace-overview-2.2.1.md), [Korean](marketplace-overview-2.2.1.ko.md) |

The source manifest owns the source version. Tests specify expected behavior; compilation alone does not prove installed Visual Studio behavior. Historical screenshots illustrate the established workflow and do not certify a newer binary.
