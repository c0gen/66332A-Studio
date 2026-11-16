# Third-party software and fonts

66332A Studio uses the following third-party components. Versioned NuGet lock files are the complete dependency inventory; native/transitive libraries are included by self-contained publishing. `tools/collect-notices.ps1` copies available package licenses, notices, and package metadata into the release's `Licenses` folder. The .NET SDK's runtime license and third-party notices are included there as well.

| Component | Pinned version | License / source |
| --- | --- | --- |
| .NET / WPF runtime | SDK 10.0.403, runtime 10.0.14 | MIT and associated notices; [dotnet](https://github.com/dotnet) |
| CommunityToolkit.Mvvm | 8.4.0 | MIT; [CommunityToolkit](https://github.com/CommunityToolkit/dotnet) |
| ScottPlot / ScottPlot.WPF | 5.0.55 | MIT; [ScottPlot](https://github.com/ScottPlot/ScottPlot) |
| System.IO.Ports | 10.0.0 | MIT; [dotnet/runtime](https://github.com/dotnet/runtime) |
| SkiaSharp and HarfBuzzSharp | See desktop lock file | MIT plus native-component notices; [SkiaSharp](https://github.com/mono/SkiaSharp) |
| OpenTK / GLWpfControl / GLFW | See desktop lock file | Package-specific license metadata and bundled notices; [OpenTK](https://github.com/opentk/opentk) |
| IBM Plex Sans (Regular, Medium, SemiBold) / Mono (Regular, Medium) | Vendored font files | SIL Open Font License 1.1; [IBM Plex](https://github.com/IBM/plex); full license in `Assets/Fonts/OFL.txt` |
| DSEG7 Classic Regular (seven-segment readout) | 0.46 | SIL Open Font License 1.1. Full license in `Assets/Fonts/DSEG-LICENSE.txt`. [Upstream release](https://github.com/keshikan/DSEG/releases/tag/v0.46) |
| xUnit / Microsoft.NET.Test.Sdk | See central versions / test locks | Development/test dependencies, not distributed as application features. |

The application bundles its fonts for offline use. The bundled fonts are licensed separately from the application source.
