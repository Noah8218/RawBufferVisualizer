# Raw Buffer Visualizer AI 작업 인계

> Historical handoff: 이 문서는 `1.0.50` 준비 당시의 작업 상태를 보존합니다. 현재 재개 지점과 릴리스 상태는 [MAINTAINER_HANDOFF.md](MAINTAINER_HANDOFF.md)와 [release-qualification-1.0.51.md](release-qualification-1.0.51.md)를 우선합니다.

이 문서는 다음 작업자가 현재 제품 상태와 `1.0.50` 릴리스 후보의 검증 경계를 빠르게 복원하기 위한 인계 문서입니다.

## 1. 제품 정체성

Raw Buffer Visualizer는 C# 머신비전 개발자를 위한 Visual Studio 2022 Image Watch 스타일 디버거 확장입니다.

핵심 사용 흐름은 다음과 같습니다.

1. 이미지 객체가 값 대입까지 끝난 줄 이후에 중단점을 건다.
2. 등록된 형식은 DataTip/Watch/Locals/Autos의 돋보기 아이콘으로 연다.
3. **Auto Inspect on Break**가 켜져 있으면 현재 스택 프레임의 Locals와 Arguments를 검사한다.
4. 열린 이미지는 하나의 도킹된 `Raw Buffer Visualizer` 창에 누적된다.
5. 사용자는 Fit/1:1/zoom/pan, 픽셀 값, raw bytes, 진단 및 비교 기능으로 영상을 확인한다.

카메라 취득, 조명, PLC/I/O, 레시피 실행 및 산업용 SDK 제어 화면은 이 프로젝트 범위가 아닙니다.

## 2. 릴리스 상태

| 항목 | 현재 상태 |
| --- | --- |
| Marketplace 공개 버전 | `1.0.49.0` |
| 현재 소스/후속 후보 | `1.0.50.0` |
| 공개 버전 외부 PC 결과 | Windows 10 문제 PC에서 확장 제거 후 `1.0.49` 재설치 시 정상화 |
| 이 결과가 증명하는 것 | 해당 PC/profile의 clean reinstall 경로 |
| 로컬 `1.0.50` 결과 | Windows 10 Pro / VS2022에서 exact 2,011,595-byte VSIX의 release announcement, 메뉴, ToolWindow, Automatic Mat collections, Bitmap handoff 및 hybrid 9 documents/0 errors 통과 |
| 현재 업로드 파일 | `artifacts\publish\RawBufferVisualizer-VisualStudioExtensibility-net472\RawBufferVisualizer.VisualStudio.Extensibility.vsix`; SHA-256 `E31F254EFCFD80D6F03FED3E453BEFC47CB4924D0FF853167AE7385F36B94D93` |
| 아직 증명하지 못한 것 | 문제 외부 PC의 `1.0.49 -> 1.0.50` 인플레이스 업데이트와 해당 PC의 DPI/도킹 Fit |

외부 PC가 clean reinstall 뒤 정상화된 사실을 인플레이스 업데이트 성공으로 기록하면 안 됩니다. 로컬 exact `1.0.50` 검증은 통과했습니다. 사용자는 이번에는 Marketplace 업로드를 먼저 하고 전파 뒤 문제 PC의 무삭제 업데이트를 검증하기로 했으므로, 외부 gate가 통과하기 전까지 전체 릴리스 상태는 `Incomplete`입니다.

## 3. 세 기능의 관계

### Vision Buffer Doctor

깨져 보이는 이미지를 다시 해석할 후보를 자동으로 제시합니다.

- pixel format, stride/row padding, width/height, endianness, valid bits 후보를 순위화합니다.
- 후보를 선택하면 debugger round-trip 없이 즉시 적용합니다.
- RGB/BGR 및 Bayer phase처럼 콘텐츠만으로 구분할 수 없는 경우는 모호성을 명시합니다.
- 최대 64행/4 MiB만 샘플링하여 전체 대형 버퍼 스캔을 피합니다.

### Smart Type Mapper

등록되지 않은 회사 전용 이미지 클래스를 사용자가 한 번 매핑하여 재사용하게 합니다.

- 데이터 포인터/배열, width, height, stride, pixel format 역할을 저장합니다.
- 사용자 범위 `%APPDATA%\RawBufferVisualizer\type-mappings.json`과 선택적 solution-local 설정을 지원합니다.
- 임의 메서드를 실행하지 않고 필드와 property getter만 읽습니다.
- arbitrary individual type에 Visual Studio 돋보기 아이콘을 새로 등록하지는 못합니다.

### Automatic Vision Inspector

Smart Type Mapper의 “사용자가 먼저 형식을 알아야 한다”는 한계를 줄이는 현재 프레임 탐색 기능입니다.

- Break Mode 진입 후 Locals와 Arguments를 합쳐 검사합니다.
- **Scan Now**는 같은 검사를 사용자가 즉시 재실행하게 합니다.
- 완전하고 안전하게 검증된 후보만 자동으로 열고, 불완전 후보는 `[Map]`, 실패 후보는 `[Failed]`로 격리합니다.
- 여러 후보 중 일부가 실패해도 성공한 이미지는 유지되어야 합니다.
- 자동 검사는 창을 강제로 열거나 포커스를 빼앗지 않습니다.
- 옵션은 사용자 설정에 저장되고 Visual Studio 재시작 뒤 복원됩니다.

## 4. `1.0.50` Automatic Inspector 계약

### OpenCvSharp Mat

정확한 런타임 형식으로 확인된 초기화된 `OpenCvSharp.Mat`은 자동 경로에서 다음 메타데이터를 사용합니다.

- `Data`
- `Cols`
- `Rows`
- `Step()`
- `Depth()`
- `Channels()`

### Emgu CV Mat

정확한 런타임 형식으로 확인된 초기화된 `Emgu.CV.Mat`은 다음 메타데이터를 사용합니다.

- `DataPointer`
- `Cols`
- `Rows`
- `Step`
- `Depth`
- `NumberOfChannels`

두 Mat 계열 모두 구조적 추측보다 exact-known-type 분기가 우선합니다. null/empty/disposed/잘못된 stride 또는 읽을 수 없는 메모리는 실패 행으로 격리해야 합니다.

### System.Drawing.Bitmap

Bitmap은 등록된 debugger visualizer의 돋보기 아이콘 경로를 사용합니다. Automatic Inspector는 debugger 평가 중 `LockBits`를 안전하게 수행할 수 없으므로 Bitmap을 자동 live-open 대상으로 과장하지 않습니다.

### 중단점 위치

다음은 잘못된 예입니다.

```csharp
Mat image = LoadImage(); // 이 줄에 멈추면 대입 전일 수 있음
```

다음 줄 이후에 중단점을 걸어야 합니다.

```csharp
Mat image = LoadImage();
Use(image); // 여기에서 image가 초기화된 상태
```

자동 검사 실패를 판단하기 전에 반드시 “대입 완료 후 중단” 조건을 확인합니다.

## 5. 원자적 handoff 계약

Debugger producer와 도킹 창 consumer 사이의 파일 handoff는 다음 상태를 사용합니다.

```text
publishing -> Ready -> Processing -> ACK
                                  -> NACK(reason)
```

필수 규칙:

- producer는 임시 `.publishing` 파일을 완성한 뒤 원자적으로 Ready 경로로 이동합니다.
- consumer는 Ready를 Processing으로 독점 claim한 뒤에만 읽습니다.
- 문서가 실제로 열린 뒤에만 ACK를 기록합니다.
- 거부 또는 예외 시 reason을 기록한 NACK를 남깁니다.
- Ready 파일이 사라진 것만으로 성공으로 판단하지 않습니다.
- request read와 terminal marker 이동은 `10 x 50 ms` 재시도합니다.
- watcher는 이벤트를 활성화하기 전에 `Created`와 `Renamed` 핸들러를 등록합니다.

Modern producer의 timeout/cancel과 Classic fire-and-forget 모두 공용 `ScheduleTerminalArtifactCleanup`을 사용합니다.

- 최대 2분 동안 100 ms 간격으로 ACK/NACK/conflict terminal artifact만 정리합니다.
- Ready/Processing은 reaper timeout에서 절대 삭제하지 않습니다.
- 취소/예외 시 소유한 요청 중 하나라도 `Missing`이 아니면 in-flight consumer와 경쟁하지 않도록 snapshot payload 디렉터리를 보존합니다.
- 2분 뒤에 terminalize된 marker와 process crash로 고착된 Processing은 즉시 자동 회수되지 않는 현재 경계입니다.

Persistent NACK marker 기록 실패는 상태와 함께 `package.log`에 남겨야 합니다.

## 6. Visual Studio 패키지/메뉴 계약

- Marketplace extension ID는 유지합니다.
- VSPackage GUID는 `{1977574b-f107-465f-bfd1-5fc022907039}`입니다.
- command table resource는 `Menus.ctmenu, 2`입니다.
- `View` 메뉴에는 `Raw Buffer Visualizer`와 `Raw Buffer Visualizer: Scan Current Frame`이 각각 정확히 한 번만 보여야 합니다.
- 향후 `.vsct` command table을 변경하면 ctmenu resource version도 증가시켜 stale cache와 구분합니다.
- 메뉴가 보이는 것만으로 package load/ToolWindow open 성공을 판정하지 않습니다.

## 7. Viewer Fit/Manual 계약

Viewer는 명시적인 두 상태를 가집니다.

### Fit

- 새 이미지가 열릴 때
- 선택 이미지가 바뀔 때
- **Fit** 버튼을 누를 때
- viewer를 더블클릭할 때

Fit은 aspect ratio를 유지하며 viewport 안에 약 5% 여백으로 전체 영상을 배치합니다. 도킹 크기가 바뀌면 Fit 상태에서는 다시 계산합니다.

### Manual

- mouse wheel zoom
- drag pan
- **1:1**

Manual 상태에서는 resize나 selection refresh 때문에 사용자의 zoom/center가 임의로 초기화되면 안 됩니다.

## 8. 구현 파일 지도

| 영역 | 주요 파일 |
| --- | --- |
| Buffer Doctor 후보/점수 | `src/RawBufferVisualizer.Core` |
| Smart Type Mapper 저장/추출 | `src/RawBufferVisualizer.ObjectSource`, `src/RawBufferVisualizer.VisualStudio.Vssdk` |
| Automatic Inspector | `src/RawBufferVisualizer.VisualStudio.Vssdk/AutomaticVisionInspector.cs` |
| exact known types | `src/RawBufferVisualizer.VisualStudio.Vssdk/KnownImageType.cs` |
| registered type capture | `src/RawBufferVisualizer.VisualStudio.Vssdk/KnownRegisteredImageCapture.cs` |
| 도킹 UI/consumer | `src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferToolWindowControl.xaml.cs` |
| package/watcher | `src/RawBufferVisualizer.VisualStudio.Extensibility/RawBufferVisualizerPackage.cs` |
| Modern producer | `src/RawBufferVisualizer.VisualStudio.Extensibility/DebuggerVisualizerLaunch.cs` |
| Classic producer | `src/RawBufferVisualizer.VisualStudio.Classic` |
| handoff 상태/claim | `src/RawBufferVisualizer.VisualStudio/VisualizerHandoffInbox.cs` |
| OpenGL Fit/Manual | `src/RawBufferVisualizer.OpenGlCanvas/RawOpenGlImageCanvas.cs` |
| 회귀 테스트 | `tests/RawBufferVisualizer.Tests/Program.cs` |

## 9. 현재 소스 검증 결과

현재 기록된 소스 검증:

- Release solution build: 성공, 오류 0, 기존 `VSTHRD010` 경고 18개
- self-test: `20/20` 통과
- 원자적 handoff 테스트:
  - `VisualizerHandoffInboxPublishesRequestsAtomically`
  - `VisualizerHandoffInboxClaimsRequestExactlyOnce`
  - `VisualizerHandoffInboxTracksExplicitCompletion`
- observer start gate와 실제 관찰 횟수 `> 0` 검증
- NACK Processing 파일을 150 ms 독점 잠금하여 retry 경로 검증
- terminal ACK가 cleanup 뒤 Missing이 되는 것, 잠긴 ACK 재시도 및 일시적 Missing 뒤 늦은 ACK 정리를 검증
- 200 ms reaper 뒤 Ready가 유지되는 것 검증
- `SmokePreviewFirstHandoff.ps1 -NoBuild`: preview 1/full 1 통과
- `SmokeSmartTypeMapper.ps1`: 통과
- `SmokeDockedLayoutWidths.ps1` 540/900/1160 px: aspect error 0, Fit margin 1.05, Manual zoom/center delta 0

이 current-source 결과와 별도로 exact installed-VSIX의 실제 Visual Studio debugger 동작도 아래와 같이 검증했습니다.

## 10. `1.0.50` 필수 installed-VSIX 검증

### 로컬 VS2022: 통과

- Windows 10 Pro `10.0.19045`, VS2022 `17.14.37314.3`, exact `1.0.50.0`
- `View` 메뉴의 open command 1개, scan command 1개
- `--multi-library-debug`를 대입 완료 후 중단점으로 실행
- OpenCvSharp/Emgu Mat 자동 열기: `8 detected: 8 opened, 0 need mapping, 0 failed`
- Bitmap 등록 visualizer 열기, 최종 9 documents / 0 errors
- **Scan Now** 반복 시 중복 없음
- 부분 실패: 6 opened / 1 mapping / 1 failed, 성공 이미지 유지
- package protocol error 0, ActivityLog의 관련 잠재 오류 0
- installed 화면은 육안 비율 증거이며, 정량 Fit/Manual 검증은 current-source 540/900/1160 matrix로 분리

### 외부 Windows 10 PC

- 공개 `1.0.49`가 설치된 상태에서 제거/repair 없이 `1.0.50`으로 업데이트
- Visual Studio 완전 종료/재시작
- 메뉴 개수, ToolWindow open, registered Bitmap/Mat, Automatic Inspector, ACK, Fit을 검증

clean reinstall은 이 인플레이스 업데이트 항목의 대체 증거가 아닙니다.

## 11. 알려진 제한

- Automatic Inspector는 현재 선택된 stack frame의 Locals와 Arguments만 검사합니다.
- root 및 한 단계 nested member까지만 제한적으로 조사합니다.
- arbitrary SDK method 호출, vendor DLL 동적 로드 및 private native layout 추측은 하지 않습니다.
- Bitmap automatic live-open은 지원 근거가 없으며 glyph 경로가 계약입니다.
- pointer lifetime은 debuggee가 paused 상태이고 메모리가 유효할 때만 보장됩니다.
- array-backed mapped individual variable은 기존 collection 경로가 필요할 수 있습니다.
- process crash로 남은 Processing과 2분 이후 terminal marker는 즉시 자동 회수하지 않습니다.
- 기존 24시간 stale snapshot 정리에는 active-document lease가 없어, 매우 오래 열린 file-backed 문서는 별도 장기 세션 검증과 lease 보강이 필요합니다.
- `VSTHRD010` 경고 18개는 남아 있는 기술 부채입니다.
- Windows 10 DPI/도킹 Fit은 외부 실증 전까지 지원 완료로 선언하지 않습니다.

## 12. 재현/검증 명령

```powershell
dotnet build RawBufferVisualizer.sln -c Release --no-restore /nodeReuse:false
dotnet run --project tests\RawBufferVisualizer.Tests\RawBufferVisualizer.Tests.csproj -c Release --no-build
powershell -ExecutionPolicy Bypass -File scripts\SmokePreviewFirstHandoff.ps1 -NoBuild
powershell -ExecutionPolicy Bypass -File scripts\SmokeSmartTypeMapper.ps1
powershell -ExecutionPolicy Bypass -File scripts\SmokeDockedLayoutWidths.ps1 -Widths 540,900,1160
powershell -ExecutionPolicy Bypass -File scripts\Test-ReleaseCommunication.ps1 -ExpectedVersion 1.0.50
powershell -ExecutionPolicy Bypass -File scripts\Publish-VisualStudioExtension.ps1 -Configuration Release -Framework net472 -ViewerFramework net472 -NoZip
powershell -ExecutionPolicy Bypass -File scripts\Test-VisualStudioMarketplaceUpdate.ps1 -ExpectedVersion 1.0.50.0
```

실제 옵션은 각 스크립트의 `Get-Help` 또는 parameter block을 먼저 확인합니다. 이미 최신 빌드가 있는 경우에만 `-NoBuild`를 사용합니다.

## 13. 다음 우선순위

1. 현재 변경을 GitHub에 커밋/푸시하고 exact 후보를 Marketplace에 업로드 | Prerequisite: 사용자 PUSH 지시와 Publisher 권한 | Recommended model: `gpt-5.6-terra` | Reasoning effort: `low`
2. Marketplace 전파 뒤 Windows 10 공개 `1.0.49 -> 1.0.50` 인플레이스 업데이트 검증 | Prerequisite: 해당 외부 VS2022 PC | Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`
3. 실제 산업 카메라 SDK/runtime 검증 확대 | Prerequisite: 합법적인 SDK, 드라이버, 대표 객체 또는 하드웨어 | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`

## 완료 기록

```text
Status: Incomplete
Scope: 1.0.50 source, final package, 통합 release communication, local installed-VSIX announcement/handoff/menu/automatic-image 동작 및 current-source Fit/Manual
Acceptance criteria: source, package/static, release communication, 로컬 exact installed-VSIX 및 current-source Fit matrix 통과; 외부 문제 PC 인플레이스 업데이트는 Pending
Verification: Release build 0 errors/18 existing warnings; self-tests 통과; communication guard/Marketplace dry run 통과; installed ReleaseAnnouncement, AutomaticCollections 및 MultiLibraryHybrid 통과
Evidence: docs/release-qualification-1.0.50.md, artifacts/ui/installed-vsix-new-features, exact SHA-256 E31F254EFCFD80D6F03FED3E453BEFC47CB4924D0FF853167AE7385F36B94D93
Boundary / next dependency: 외부 Windows 10 PC에서 public 1.0.49를 제거/repair 없이 exact 1.0.50으로 업데이트
```
