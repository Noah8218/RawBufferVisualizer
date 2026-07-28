# AI Handoff: Buffer Doctor + Automatic Vision Inspector + Smart Type Mapper

> 2026-07-28 중요 갱신: Marketplace `1.0.47.0`은 새 PC에서 VSSDK 패키지가 실행되지 않아 `did not acknowledge the image handoff` 오류가 발생했다. `1.0.48.0`에서는 `RawBufferVisualizerPackage`와 VSCT를 메인 하이브리드 프로젝트로 이동하고, `PkgdefProjectOutputGroup`이 `RawBufferVisualizer.VisualStudio.Extensibility.pkgdef`을 생성하도록 수정했다. 일반 설치 스크립트의 수동 레지스트리 등록은 제거했다. 재발 방지 계약은 `docs/vsix-package-registration.md`, 로컬 검증 결과는 `docs/release-qualification-1.0.48.md`가 기준이다. 깨끗한 PC 검증에는 repair 스크립트를 사용하면 안 된다.

> 상태: 1.0.47 Buffer Doctor, Automatic Vision Inspector, Smart Type Mapper 자동 폴백과 등록형 Bitmap/Mat 혼합 시나리오는 installed-VSIX 검증 통과. Marketplace 공개와 실제 카메라 SDK/하드웨어 검증은 미완료
> 작성일: 2026-07-27
> 대상: 후속 AI 모델이 이어서 검증/수정/개선할 수 있도록 한 문서

---

## 1. 이 프로젝트는 무엇인가

**Raw Buffer Visualizer**는 C# 머신비전 개발자를 위한 Visual Studio 2022 디버거 확장이다. Image Watch처럼 중단점에서 이미지 변수를 한 번에 볼 수 있으며, OpenCvSharp.Mat, Emgu.CV.Mat, System.Drawing.Bitmap, RawBufferSnapshot/View, 그리고 지원하는 컬렉션(`List<>`, `Dictionary<,>`, `object[]`, 배열 등)을 하나의 도킹된 창에 누적해서 표시한다.

핵심 차별점은 "원시 버퍼 진단"이다: stride, pixel format, valid bits, byte order, packed mono, Bayer 등을 UI에서 직접 확인하고 해석할 수 있다.

---

## 2. 이번에 추가한 세 기능

### 2-1. Vision Buffer Doctor

**목표**: 이미지가 깨져 보일 때(대각선 밀림, 줄 깨짐, 밝기 이상 등) 사용자가 Width/Height/Stride/Pixel Format을 하나씩 바꿔보지 않아도, 가능한 해석 후보를 점수와 이유와 함께 보여주고 선택하면 즉시 적용한다.

**핵심 메시지**: "이미지를 보는 도구가 아니라, 이미지가 왜 깨졌는지 알려주는 도구."

**구현 위치**:
- `src/RawBufferVisualizer.Core/BufferInterpretation.cs` — 후보/결과 타입, `BufferDoctor.Diagnose()` 진입점
- `src/RawBufferVisualizer.Core/BufferInterpretationCandidateGenerator.cs` — 버퍼 길이로부터 가능한 (width, height, stride, format) 후보 생성
- `src/RawBufferVisualizer.Core/BufferInterpretationScorer.cs` — 구조 점수 + 샘플링 기반 콘텐츠 점수
- `src/RawBufferVisualizer.Core/RawImageSource.cs` — `TryReadRange()` API 추가 (메모리/파일/프로세스 메모리 소스 구현)
- `src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferToolWindowControl.xaml(.cs)` — Interpret 섹션에 "Diagnose Buffer" 버튼 + 후보 패널
- `tests/RawBufferVisualizer.Tests/Program.cs` — 8개의 Buffer Doctor 단위 테스트

**Phase 1에서 지원하는 포맷**:
- Mono8
- Mono16 (Little/Big Endian, ValidBits 10/12/14/16)
- RGB24 / BGR24
- BGRA32
- Mono10PackedLsb / Mono12PackedLsb
- Float32

**지원하지 않는 것(Phase 2로 연기)**:
- Top-down / Bottom-up 행 순서
- Bayer phase 자동 판별 (현재는 힌트가 있을 때 모호성 그룹으로만 표시)
- Planar / Interleaved
- YUV, signed, 압축 포맷

**점수 구조**:
- 구조 점수 최대 40: exact/trailing-row fit, stride alignment, plausible dimensions, common sensor width, row padding
- 콘텐츠 점수 최대 60: 인접 행 연속성(row continuity) 상관계수, Mono16 endianness smoothness, Mono16 valid-bits 적합도
- 샘플링 한도: 최대 64개 행, 총 4MiB 이하. 전체 버퍼 스캔 금지.

**자동화된 테스트** (`tests/RawBufferVisualizer.Tests/Program.cs`에 추가됨):
1. `BufferDoctorFindsPaddedMono8Descriptor` — 2448x2048 stride 2560(112B padding) 버퍼에서 정확한 descriptor가 1위
2. `BufferDoctorCorrectStrideWinsOnRowContinuity` — 대각선 shear 케이스에서 올바른 stride가 승리
3. `BufferDoctorPrefersCorrectEndianness` — Mono16 LE/BE ramp에서 올바른 endianness 승리
4. `BufferDoctorPrefersMatchingValidBits` — 값이 4096 미만일 때 ValidBits 12가 16보다 높게
5. `BufferDoctorFindsPackedMono12Candidate` — packed Mono12 후보 발견
6. `BufferDoctorSamplingStaysWithinCaps` — 100000x100000 file-backed source에서 샘플링량 제한 준수
7. `BufferDoctorMarksRgbBgrAsAmbiguousTieGroup` — RGB24/BGR24을 모호성 그룹으로 표시
8. `BufferDoctorAcceptsTrailingRowFit` — stride*(h-1)+minStride 길이 허용

---

### 2-2. Smart Type Mapper

**목표**: Raw Buffer Visualizer가 원래 지원하지 않는 회사 전용 이미지 클래스(예: `Company.Vision.CompanyFrame`)를 사용자가 한 번 멤버 매핑하면, 코드 수정이나 확장 재빌드 없이 계속 열어볼 수 있게 한다.

**핵심 메시지**: "모든 회사를 직접 지원할 수 없으므로, 어떤 회사 타입이든 사용자가 1회 매핑할 수 있게 지원한다."

**구현 위치**:
- `src/RawBufferVisualizer.VisualStudio.ObjectSource/TypeMappingStore.cs` — 매핑 파일(JSON) 로드/저장, solution-local vs %APPDATA% 우선순위
- `src/RawBufferVisualizer.VisualStudio.ObjectSource/MappedTypeVisualizerTransfer.cs` — 매핑에 따라 멤버를 읽어 ImageCollectionItemTransfer 생성
- `src/RawBufferVisualizer.VisualStudio.ObjectSource/VisualizerMemberInventory.cs` — 실패 시 멤버 목록(이름/종류/타입/샘플값/enum 값) 수집
- `src/RawBufferVisualizer.VisualStudio.Vssdk/TypeMappingDialog.xaml(.cs)` — 매핑 다이얼로그(드롭다운 + enum 매핑 + Preview + Save)
- `src/RawBufferVisualizer.VisualStudio.Vssdk/ImageTypeRecognizer.cs` — Break Mode에서 Local 변수를 스캔해 이미지 후보 찾기
- `src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferToolWindowControl.xaml.cs` — "Scan Locals" 명령, Open Variable, 후보/에러 행 표시
- `src/RawBufferVisualizer.VisualStudio.Vssdk/OpenVariableDialog.xaml(.cs)` — 개별 변수 표현식 입력 다이얼로그

**매핑 파일 스키마(v1)**:
- `%APPDATA%\RawBufferVisualizer\type-mappings.json` (사용자 전용)
- `<SolutionRoot>\.rawbuffervisualizer.json` (팀 공유, 우선순위 더 높음)

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

**사용자 흐름**:
1. `List<CompanyFrame>` 변수의 돋보기 아이콘 클릭
2. 매핑이 없으면 각 항목이 "Unsupported type" 에러 행으로 표시되고 "Map This Type" 버튼 노출
3. 다이얼로그에서 멤버 역할 선택(Data/Width/Height/Stride/PixelFormat/BufferLength/ValidBits/ByteOrder)
4. PixelFormat이 enum이면 enum 값별로 RawPixelFormat 매핑
5. Preview 버튼으로 live debuggee memory에서 썸네일 확인
6. Save for This Type → JSON 저장
7. 다음 번부터는 자동으로 열림

**개별 변수 열기**: 도킹된 창의 "Open Variable" 버튼으로 `frame` 같은 표현식 입력. Break Mode일 때만 동작.

**자동화된 테스트** (`tests/RawBufferVisualizer.Tests/Program.cs`에 추가됨):
1. `TypeMappingFileRoundTrips` — JSON 직렬화/역직렬화
2. `TypeMappingResolutionPrefersSolutionLocal` — solution-local이 %APPDATA%보다 우선
3. `TypeMappingExtractsMappedCompanyFrame` — IntPtr/byte[]/ushort[] 기반 추출
4. `TypeMappingAppliesEnumPixelFormatMap` — `Mono12 -> Mono12PackedLsb`
5. `TypeMappingFailureIncludesMemberInventory` — 매핑 실패 시 멤버 목록 포함
6. `TypeMappingMissingMemberFailsVisibly` — 멤버 누락 시 가시적 에러

**안전 규칙**:
- 읽기만: field, property getter, enum 값, 배열 길이
- 메서드 호출 금지 (`GetBuffer()`, `ConvertImage()` 등)
- 임의 표현식 평가는 v1에서 제외

---

### 2-3. Automatic Vision Inspector

**목표**: Visual Studio가 빌드 시점에 등록하지 않은 회사 전용 타입에는 돋보기 아이콘을 붙일 수 없다는 Smart Type Mapper의 진입점 한계를 해소한다. 중단점에 들어가면 현재 Stack Frame의 Local을 자동으로 스캔하고, 안전하게 이미지 구조를 확인할 수 있는 값은 즉시 기존 도킹 창에 연다. 불확실한 값에만 Smart Type Mapper를 제시한다.

**우선순위**:
1. 저장된 사용자 Type Mapping
2. 알려진 이미지 형태
3. 타입 이름 힌트
4. 필드/프로퍼티 멤버 구조
5. 현재 값과 메모리/descriptor 유효성
6. Smart Type Mapper

**지원 범위**:
- 직접 멤버와 1단계 중첩 멤버
- `IntPtr`, `UIntPtr`, `byte[]`, `ushort[]`, `float[]`
- Data, Width, Height 필수; Stride, BufferLength, ValidBits, PixelFormat은 신뢰도와 안전성을 보강
- 90점 이상이고 포맷이 명확하면 자동 열기
- 40점 이상이지만 불완전하거나 모호하면 매핑 후보로 표시
- 40점 미만은 기본 숨김
- Break Mode 자동 스캔과 `Scan Now`
- 자동 행은 루트 표현식 기준으로 교체하여 반복 스캔 시 중복 방지

**안전 경계**:
- 임의 SDK 메서드 자동 호출 금지
- 무제한 객체 그래프 탐색 금지
- 동적 벤더 DLL 로딩/비공개 native layout 해석 금지
- 루트 배열과 컬렉션은 기존 등록된 collection visualizer가 담당
- 관리 배열은 VSSDK debugger property 자식 열거를 우선 사용하고 EnvDTE fallback은 256개로 제한

**구현/검증 문서**: `docs/automatic-vision-inspector.md`

**2026-07-28 installed-VSIX 결과**:
- VS2022 17.14에서 함수 인자, 직접 포인터, `byte[]`(64 x 48), 1단계 중첩 포인터를 포함한 6개 이미지를 오류 없이 자동 표시
- 별도의 `[Map]` 1개와 `[Failed]` 1개가 있어도 6개 성공 이미지가 유지되고, `Scan Now` 반복 뒤에도 중복 없음
- 결과: `artifacts/ui/installed-vsix-new-features/AutomaticVisionInspector-installed-vsix.json`
- 화면: `artifacts/ui/installed-vsix-new-features/automatic-vision-inspector.png`
- Smart Type Mapper 자동 폴백: 88% `MappingRequired` 후보 → `Mono12PackedLsb` 선택 → live-memory Preview → Save → 640 x 484, stride 960, 오류 0으로 자동 재열림
- Smart Type Mapper 결과: `artifacts/ui/installed-vsix-new-features/SmartTypeMapper-installed-vsix.json`
- Smart Type Mapper 화면: `smart-type-mapper-automatic-before-map.png`, `smart-type-mapper-dialog-preview.png`, `smart-type-mapper-automatic-after-reopen.png`
- 실제 OpenCvSharp `Mat`, Emgu CV `Mat`, `System.Drawing.Bitmap`은 등록형 visualizer로 열리고 자동 후보에서 제외됨
- `RawBufferSnapshot`/`RawBufferView`도 자동 후보에서 제외되며, 카메라형 래퍼 6개는 자동으로 열려 전체 9개 이미지/오류 0개
- 혼합 결과: `artifacts/ui/installed-vsix-new-features/MultiLibraryHybrid-installed-vsix.json`

---

## 3. 현재까지 완료된 것

| 항목 | 상태 | 근거 |
|------|------|------|
| Buffer Doctor Core (생성/점수/샘플링) | 완료 | `BufferInterpretation*.cs`, 8개 단위 테스트 통과 |
| Buffer Doctor UI (Interpret 섹션 버튼 + 후보 패널) | 완료 | `RawBufferToolWindowControl.xaml/cs` |
| Smart Type Mapper Core (매핑 파일, 추출) | 완료 | `TypeMappingStore.cs`, `MappedTypeVisualizerTransfer.cs` |
| Smart Type Mapper UI (다이얼로그, Preview, Save) | 완료 | `TypeMappingDialog.xaml/cs` |
| Open Variable (개별 변수 EnvDTE 진입점) | 완료 | `OpenVariableDialog.xaml/cs`, `RawBufferToolWindowControl.OpenVariableExpression()` |
| Break Mode Locals 스캔 | 완료 | `ImageTypeRecognizer.cs`, `RawBufferToolWindowControl.ScanLocals()` |
| Automatic Vision Inspector | 완료 | `AutomaticVisionInspector.cs`, `VisionMemberInference.cs`, `VisualStudioDebugFrameContext.cs`, ToolWindow Auto Inspect UI |
| Automatic Vision Inspector installed-VSIX | 통과 | VS2022 17.14, 6개 자동 열기, 64x48 배열, 함수 인자, 1단계 중첩, 부분 실패 격리, 반복 스캔 중복 없음 |
| Smart Type Mapper 자동 폴백 installed-VSIX | 통과 | VS2022 17.14, 모호 후보 → live Preview → Save → 640x484 Mono12PackedLsb 자동 재열림, 오류 0 |
| 등록형/자동 혼합 installed-VSIX | 통과 | 실제 OpenCvSharp/Emgu/Bitmap + 자동 래퍼 6개, 전체 이미지 9개, 오류 0개 |
| Release 빌드 | 통과 | 2026-07-28, 경고 18개(VSTHRD010), 오류 0개 |
| VSIX 재설치 | 완료 | `RawBufferVisualizer.VisualStudio.Extensibility.vsix` 설치됨 |
| 단위 테스트 | 통과 (이전 회차) | `RawBufferVisualizer.Tests` 80+개 테스트 |
| 1.0.47 로컬 출시 자격 | 완료 | `docs/release-qualification-1.0.47.md` |

---

## 4. 검증 기록과 남은 실물 검증

### 4-1. installed-VSIX 검증 체크리스트 (자동화 통과, 수동 재확인용)

2026-07-27에 Visual Studio 2022 17.14 설치본 자동화로 Buffer Doctor, Automatic Vision Inspector, Smart Type Mapper 자동 폴백이 통과했다. 아래 항목은 향후 수동 회귀 확인이나 컬렉션/Open Variable 경로 확장 검증에 재사용한다.

**Buffer Doctor**:
1. `--buffer-doctor-debug` 인자로 실행
2. `badStrideSnapshot` 중단점에서 이미지가 깨져 보이는지 확인
3. "Diagnose Buffer" 버튼 클릭
4. 후보 패널에서 `stride 2560` 후보가 1위인지 확인
5. 해당 후보 선택 시 이미지가 바로 복원되는지 확인
6. UI smoke 스크립트 참고: `scripts/SmokeBufferDoctorPanel.ps1`

**Smart Type Mapper (컬렉션 경로)**:
1. `--smart-type-mapper-debug` 인자로 실행
2. `companyFrameList` 변수의 돋보기 클릭
3. 항목이 "Unsupported type" 에러 행으로 표시되고 "Map This Type" 버튼이 보이는지 확인
4. 매핑 다이얼로그에서 멤버가 자동으로 선택되었는지 확인
   - Data → `ImageAddress`
   - Width → `SizeX`
   - Height → `SizeY`
   - Stride → `LinePitch`
   - Pixel Format → `PixelType`
5. Preview 버튼 클릭 시 live memory에서 썸네일이 렌더링되는지 확인
6. enum 매핑: `Mono12 -> Mono12PackedLsb`, `Bgr -> BGR24` 설정 후 Save
7. `%APPDATA%\RawBufferVisualizer\type-mappings.json`에 저장되었는지 확인
8. 다시 변수의 돋보기 클릭 시 자동으로 이미지가 열리는지 확인

**Smart Type Mapper (개별 변수 경로)**:
1. `--multi-library-debug` 또는 `--smart-type-mapper-debug`로 실행
2. 도킹된 창의 "Open Variable" 버튼 클릭
3. 표현식에 `companyFrame` 입력
4. 매핑되어 있으면 이미지가 열리고, 없으면 매핑 다이얼로그가 뜨는지 확인
5. Pointer-backed(`IntPtr`) 데이터는 v1에서만 지원; array-backed 개별 변수는 아직 컬렉션 경로로 사용

### 4-2. 다양한 시뮬레이션 클래스 검증

`samples/RawBufferVisualizer.VisualizerDebuggee/Program.cs`에 추가된 다음 클래스들은 실제 칩/라이브러리가 아닌 시뮬레이션이지만, Smart Type Mapper가 구조 기반으로 인식하는지 확인할 수 있다.

- `SimulatedOpenCvSharpMat` — `Data`, `Rows`, `Cols`, `Step`
- `SimulatedEmguCvMat` — `DataPointer`, `Rows`, `Cols`, `Step`, `Depth`, `Channels`
- `SimulatedBaslerGrabResult` — `PixelData`, `Width`, `Height`, `Stride`, `PixelType`
- `SimulatedFlirImagePtr` — `Data`, `Width`, `Height`, `Stride`, `PixelFormat`
- `SimulatedAvtVimbaFrame` — `Buffer`, `Width`, `Height`, `Stride`, `PixelFormat`
- `SimulatedIdsUeyeMemoryBuffer` — `Data`, `Width`, `Height`, `Stride`

`--multi-library-debug` 인자로 실행하면 위 객체들이 모두 스코프에 있다. 각각에 대해:
- `ImageTypeRecognizer`가 Local 스캔에서 후보로 잡는지 (Scan Locals)
- `Map This Type`이 필요한 경우 매핑 후 다시 열리는지
- 매핑 후 Buffer Doctor와 연동되는지 (깨진 이미지면 Diagnose Buffer)

### 4-3. 다양한 실제 포맷/라이브러리 검증

실제 OpenCvSharp/Emgu/Bitmap은 등록형 visualizer 경로로 검증되었다. 아래 포맷 목록은 호환성 매트릭스로도 확인됐지만, Automatic Inspector가 이 등록형 타입을 다시 추론하지 않는 것이 의도된 동작이다. 산업 카메라 SDK는 실제 런타임 객체가 확보될 때 추가 테스트해야 한다.

**OpenCvSharp**:
- `Mat` (CV_8UC1, CV_8UC3, CV_8UC4, CV_16UC1, CV_32FC1)
- `Mat` inside `List<Mat>`, `Dictionary<string, Mat>`

**Emgu CV**:
- `Emgu.CV.Mat` (Cv8U C1/C3/C4, Cv16U C1, Cv32F C1)

**System.Drawing.Bitmap**:
- Format8bppIndexed, Format24bppRgb, Format32bppArgb

**산업 칩라 SDK (가능한 경우)**:
- Basler pylon `IGrabResult` / `IBuffer`
- HIKROBOT MVS `IFrame`
- FLIR Spinnaker `ImagePtr`
- AVT Vimba `Frame`
- IDS uEye `MemoryBuffer`
- Euresys eGrabber / Teledyne DALSA Sapera / Matrox MIL

이들은 실제 객체가 필요하므로, 객체가 없다면 시뮬레이션 클래스로 동일한 멤버 구조를 검증하는 것으로 대체할 수 있다. 단, 실제 SDK 객체의 lifetime, pointer validity, enum 값 이름은 반드시 실제 환경에서 확인해야 한다.

### 4-4. 엣지 케이스 검증

- 매핑 파일이 손상되었을 때 확장이 crash 없이 에러 행으로 실패하는지
- `typeName`은 같지만 `assemblyName`이 다른 두 타입이 별개로 매핑되는지
- 매핑된 멤버가 rename되어 없어졌을 때 가시적 에러가 나오는지
- Buffer Doctor 후보를 선택 후 다시 Interpret 수동 조정이 잘 연동되는지
- Buffer Doctor의 RGB/BGR 모호성 그룹이 실제로 잘 표시되는지
- Large Mat(8192x8192 이상)에서 Buffer Doctor 샘플링이 여전히 빠른지
- Break Mode가 아닐 때 Open Variable이 비활성화되거나 명확한 메시지를 보여주는지

---

## 5. 알려진 제한 사항

1. **시각화 아이콘 제한**: Smart Type Mapper가 임의의 개별 타입에 돋보기 아이콘을 만들지는 못한다. Visual Studio는 빌드 시점에 등록된 타입에만 아이콘을 붙인다. 매핑된 타입은 등록된 컬렉션 나이거나, "Open Variable" 진입점을 통해야 한다.
2. **개별 변수 array-backed 매핑**: v1에서는 `Open Variable`이 pointer-backed(`IntPtr`) 변수만 지원한다. 배열 기반 매핑은 컬렉션(`List<>` 등)을 통해 열어야 한다.
3. **자동 인식 한계**: Buffer Doctor는 후보를 제시할 뿐, RGB/BGR이나 Bayer phase는 장면에 따라 수학적으로 구분 불가능할 수 있다.
4. **메서드 호출 금지**: Smart Type Mapper는 field/property getter만 읽는다. `GetBuffer()` 같은 메서드는 호출하지 않는다.
5. **VSTHRD010 경고**: `ImageTypeRecognizer.cs`에서 EnvDTE 객체 접근 시 UI 스레드 경고가 18개 있다. installed-VSIX 동작은 검증했지만 경고는 기술 부채로 남아 있다.

---

## 6. 다음 모델이 수행할 권장 작업 순서

### 단계 1: 실제 산업 SDK 객체 검증

Prerequisite: 정확한 패키지/SDK 버전, 재현 가능한 런타임 객체, 포인터 lifetime 규칙, 합법적으로 사용할 수 있는 샘플. 이 입력이 없으면 산업 SDK 검증에 모델 토큰을 쓰지 않는다.

1. 사용자가 실제 산업 SDK 객체를 제공하면 type/assembly/멤버/enum/lifetime을 기록하고 동일한 Break Mode 경로를 검증한다.
2. 시뮬레이션 객체 결과를 실제 SDK 지원 주장으로 확대하지 않는다.

Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`

### 단계 2: 1.0.47 Marketplace 공개

1. `docs/marketplace-overview-1.0.47.md`와 `docs/marketplace-release-notes-1.0.47.md`를 Marketplace에 반영한다.
2. SHA256 `DAB2CE62007F77F11CFF828AF02EF2F2DAE26A3BB3238CF251679E4A69505174`인 최종 VSIX를 업로드한다.
3. 공개 버전/Overview가 실제로 전파됐는지 확인한다.

Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`

### 단계 3: 별도 PC 업데이트/재시작 검증

이전에 1.0.45가 설치된 다른 VS2022 PC에서 update → restart → registered provider/Automatic Inspector 경로를 확인하고 package-load popup이 없음을 기록한다.

Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`

---

## 7. 주요 파일 요약

| 파일 | 역할 |
|------|------|
| `src/RawBufferVisualizer.Core/BufferInterpretation.cs` | Buffer Doctor 진입점, 후보 정렬, 모호성 그룹 처리 |
| `src/RawBufferVisualizer.Core/BufferInterpretationCandidateGenerator.cs` | 버퍼 길이 → 해석 후보 생성 |
| `src/RawBufferVisualizer.Core/BufferInterpretationScorer.cs` | 후보 샘플링 및 점수 계산 |
| `src/RawBufferVisualizer.Core/RawImageSource.cs` | `TryReadRange` API, 메모리/파일/프로세스 소스 |
| `src/RawBufferVisualizer.VisualStudio.ObjectSource/TypeMappingStore.cs` | 매핑 JSON 로드/저장 |
| `src/RawBufferVisualizer.VisualStudio.ObjectSource/MappedTypeVisualizerTransfer.cs` | 매핑 기반 debuggee 객체 → transfer |
| `src/RawBufferVisualizer.VisualStudio.ObjectSource/VisualizerMemberInventory.cs` | 실패 시 멤버 목록 수집 |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/ImageTypeRecognizer.cs` | Break Mode Local 변수 스캔 |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/TypeMappingDialog.xaml(.cs)` | 매핑 다이얼로그 |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/OpenVariableDialog.xaml(.cs)` | 개별 변수 표현식 입력 |
| `src/RawBufferVisualizer.VisualStudio.Vssdk/RawBufferToolWindowControl.xaml(.cs)` | ToolWindow UI, Scan Locals, Open Variable, Diagnose Buffer 패널 |
| `samples/RawBufferVisualizer.VisualizerDebuggee/Program.cs` | 테스트용 debuggee, 여러 시뮬레이션 클래스 포함 |
| `tests/RawBufferVisualizer.Tests/Program.cs` | 단위 테스트 |
| `docs/buffer-doctor-design.md` | Buffer Doctor 설계 문서 |
| `docs/smart-type-mapper-design.md` | Smart Type Mapper 설계 문서 |
| `docs/MAINTAINER_HANDOFF.md` | 전체 프로젝트 상태 문서 |

---

## 8. 마지막으로 실행된 명령

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Git\RawBufferVisualizer\scripts\Install-VisualStudioExtension.ps1" -Configuration Release -Framework net472 -ViewerFramework net472 -VisualStudioInstanceId 2c8402d8 -NoBuild -Reinstall
powershell -ExecutionPolicy Bypass -File "C:\Git\RawBufferVisualizer\scripts\SmokeInstalledVsixNewFeatures.ps1" -Scenario MultiLibraryHybrid -Configuration Release -NoBuild -NoInstall
```

결과:
- 1.0.47 최종 VSIX 설치 완료
- 새 Visual Studio 세션에서 혼합 시나리오 통과
- 등록형 3개 + 자동 6개 = 이미지 9개, 오류 0개

---

## 9. 주의사항

- 이 문서의 1.0.47 자동화 시나리오는 실제 Visual Studio 2022 디버거/installed-VSIX 환경에서 완료되었다. 실제 산업 카메라 SDK 런타임과 하드웨어는 별도 미검증 범위다.
- "Open Variable"과 "Scan Locals"는 Break Mode에서만 의미 있다.
- 매핑 파일은 `%APPDATA%\RawBufferVisualizer\type-mappings.json`에 저장된다. 이 파일을 수동으로 편집할 때는 JSON 형식과 `version: 1`을 유지해야 한다.
- 새로운 SDK 포맷을 추가할 때는 반드시 실제 객체의 lifetime과 pointer validity를 고려해야 한다.
