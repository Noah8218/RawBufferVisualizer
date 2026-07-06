# Third-Party Notices

Raw Buffer Visualizer source code is licensed under the MIT License. Third-party libraries, SDKs, and build tools keep their own licenses and are not relicensed by this project.

Review this file before publishing a Marketplace package or GitHub release. If dependencies are added, removed, or upgraded, update this file from the package metadata and bundled license files.

## Runtime Components

| Component | Version | Used by | Redistributed in VSIX | License / notice |
| --- | --- | --- | --- | --- |
| SharpGL / SharpGL.WinForms / SharpGL.SceneGraph | 2.4.0 | Docked image canvas hosting | Yes | MIT license. Upstream: https://github.com/dwmkerr/sharpgl |
| Microsoft.VisualStudio.DebuggerVisualizers | 17.6.1032901 | Debugger visualizer object source contracts | Indirect/build output dependent | Microsoft package license metadata: https://aka.ms/pexunj |
| Microsoft.VisualStudio.Extensibility.Sdk / Build | 17.9.2092 | Visual Studio extensibility build/runtime contracts | Build/VSIX tooling | Package license file: `LICENSE_SDKOOB.txt` |
| Microsoft.VSSDK.BuildTools | 17.14.2120 | VSSDK package generation | Build tooling | Package license file: `license.txt`; package also includes `NOTICE.txt` |

## Optional Adapters And Samples

| Component | Version | Used by | Redistributed in VSIX | License / notice |
| --- | --- | --- | --- | --- |
| System.Drawing.Common | 8.0.8 / transitive 10.0.9 | Bitmap adapter and test paths | No direct VSIX copy | MIT license in NuGet metadata |
| OpenCvSharp4 / OpenCvSharp4.Windows / OpenCvSharp4.runtime.win | 4.13.0.20260627 | OpenCvSharp adapter and debuggee sample | No | Apache-2.0 license in NuGet metadata. NuGet: https://www.nuget.org/packages/OpenCvSharp4/ |
| Emgu.CV / Emgu.CV.runtime.windows | 4.13.0.5924 | Visualizer debuggee sample and smoke tests only | No | Dual GPLv3/commercial license. Package license file: `LICENSE.txt`; upstream: https://www.emgu.com/wiki/index.php/Licensing%3A |
| Microsoft.NETFramework.ReferenceAssemblies.net472 | 1.0.3 | Build/reference assemblies for `net472` | No runtime copy | Microsoft package license metadata |

## Notes

- The Visual Studio extension detects OpenCvSharp `Mat` and Emgu CV `Mat` by debugger visualizer target type and reflection. The VSIX does not bundle OpenCvSharp or Emgu CV assemblies.
- The debuggee sample references Emgu CV so contributors can test the Emgu path. Emgu CV is dual licensed; review GPLv3/commercial obligations before redistributing binaries that include Emgu CV.
- Transitive dependencies keep their own notices. Run this before release review:

```powershell
dotnet list .\RawBufferVisualizer.sln package --include-transitive
```

