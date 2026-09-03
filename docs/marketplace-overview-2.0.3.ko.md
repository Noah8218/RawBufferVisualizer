# Raw Buffer Visualizer

Visual Studio 중단점에서 C# 머신비전 이미지와 원시 2D 버퍼를 바로 확인할 수 있습니다. Raw Buffer Visualizer는 `System.Drawing.Bitmap`, OpenCvSharp/Emgu `Mat`, 지원되는 이미지 컬렉션, 관리형 또는 포인터 기반 애플리케이션 버퍼를 하나의 도킹 뷰어에 모아 보여줍니다. 애플리케이션에 디버그 전용 이미지 저장 또는 변환 코드를 추가할 필요가 없습니다.

![코드 DataTip에서 OpenCvSharp Mat 열기](images/raw-buffer-visualizer-datatip-open.gif)

코드 편집기에서 초기화된 `Mat` 또는 `Bitmap` 위에 마우스를 올리고 DataTip의 시각화 돋보기 아이콘을 선택합니다. 설치된 확장이 현재 객체를 도킹 뷰어에 엽니다. 위 예시는 OpenCvSharp `Mat`에서 실제 1280 x 720 `BGR24` 산업 장비 사진을 여는 과정입니다.

동일한 등록 시각화 기능은 Locals, Autos, Watch에서도 사용할 수 있습니다.

![Locals에서 실제 산업용 Bitmap 열기](images/raw-buffer-visualizer-breakpoint-open.gif)

두 번째 Break Mode 예시는 Locals의 초기화된 `industrialBitmap`에서 Raw Buffer Visualizer 항목을 선택해 식별하기 쉬운 1280 x 960 컬러 PCB 사진을 엽니다. 두 예시 모두 절차적으로 만든 시험 패턴이나 미리 불러 둔 이미지가 아니라 실제 디버거 시각화 전달 과정입니다.

## 하나의 도킹 이미지 디버깅 작업 흐름

1. 확장을 설치하고 Visual Studio를 완전히 다시 시작합니다.
2. 디버깅을 시작하고 이미지 변수가 대입된 다음 줄에서 중단합니다.
3. 등록된 이미지와 컬렉션은 DataTip, Locals, Autos, Watch의 시각화 아이콘으로 엽니다. 처음 시각화할 때 도킹 창이 자동으로 열립니다.
4. 도킹 창이 열린 뒤에는 초기화된 OpenCvSharp/Emgu Mat 및 호환 애플리케이션 래퍼를 **Auto Inspect on Break**로 찾거나 필요할 때 **Scan Now**를 사용합니다.
5. 썸네일을 선택해 픽셀, 원본 바이트, 크기, stride, 형식, 진단 및 비교 보기를 확인합니다.

![Visual Studio에서 실행 중인 Raw Buffer Visualizer 전체 작업 흐름](images/raw-buffer-visualizer-demo.gif)

6초 전체 데모는 설치된 확장과 실제 CC0 산업용 사진을 사용합니다. 등록 시각화 전달, 실시간 컬러 픽셀, 자동 검사, 진단 정보, 의도적으로 잘못 지정한 컬러 stride, Buffer Doctor 최상위 보정, 컬러 프레임 복구를 순서대로 보여줍니다. 어떤 상태도 1.2초보다 오래 유지하지 않습니다.

**Inspector**, **What's New**, **Environment** 또는 해석 패널을 열거나 닫는 동작은 스캔을 시작하지 않습니다. 자동 검사는 **Auto Inspect on Break**가 켜진 상태에서 디버거가 Break Mode에 진입할 때만 실행되며, **Scan Now**는 명시적인 수동 새로 고침으로 유지됩니다.

![Visual Studio에서 실제 산업용 PCB 이미지를 검사하는 Raw Buffer Visualizer](images/industrial-pcb-auto-inspector-pixel.png)

동일한 1280 x 960 `BGR24` 프레임이 OpenCvSharp, Emgu CV, 포인터 기반 소유자, 중립 프레임 래퍼를 통해 열려 있습니다. 도킹 뷰어 한곳에서 이미지 목록, 실시간 B/G/R 값, 원본 바이트, 5 x 5 주변 통계, 크기, stride, 형식 및 소스 상태를 함께 확인할 수 있습니다.

## 지원 이미지 소스

- `System.Drawing.Bitmap`
- OpenCvSharp `Mat` 및 Emgu CV `Mat`
- `RawBufferSnapshot` 및 포인터 기반 `RawBufferView`
- 매핑된 `byte[]`, `ushort[]`, `float[]` 애플리케이션 버퍼
- 디버거에서 크기, stride 또는 정확한 길이, 픽셀 형식을 확인할 수 있는 매핑된 포인터 객체
- 지원되는 형식의 목록, 일반 또는 동시성 딕셔너리, 1차원 이미지 배열
- `.rbuf.json`과 `.raw` 스냅샷 파일

지원 픽셀 형식에는 `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32` 및 네 가지 8비트 Bayer 위상이 포함됩니다.

## 애플리케이션 버퍼 연결

호환 애플리케이션 객체가 직접 등록되어 있지 않으면 **Connect Your Buffer**에서 디버거에 표시되는 멤버를 버퍼, 너비, 높이, stride 또는 길이, 픽셀 형식, 유효 비트 및 바이트 순서에 연결할 수 있습니다. **Preview**는 사용자가 선택했을 때만 현재 중단된 버퍼를 읽습니다. 해석이 올바르지 않으면 같은 대화상자의 **Diagnose interpretation**에서 제한된 대안을 순위별로 확인할 수 있습니다. 편집 가능한 타입별 매핑은 **Save Mapping**을 선택했을 때만 저장됩니다.

등록 소스와 매핑 소스는 동일한 검증 규칙을 사용합니다. 잘못된 크기, 부족한 stride, 짧은 버퍼 길이, 지원하지 않는 형식 또는 바이트 순서, descriptor 계산 오버플로, 맞지 않는 유효 비트는 추측하지 않고 명확한 오류로 표시합니다.

## 검사, 비교 및 진단

- X/Y, GV 또는 RGB 값, 채널 색상, 원본 바이트, 마우스 주변 통계, 마커, 라인 프로파일, 히스토그램 및 진단 정보를 확인합니다.
- 종횡비를 유지하는 Fit, 1:1, 휠 확대/축소, 드래그 이동 및 고배율 픽셀 오버레이를 사용합니다.
- 연결 보기, 분할, 절대 차이 및 깜박임 모드로 A/B 이미지를 비교합니다.
- 현재 보이는 이미지를 PNG로 내보내거나 원본 소스를 raw 스냅샷으로 보존합니다.
- 파일 기반 타일 뷰어로 매우 큰 버퍼를 엽니다.
- **Diagnose Buffer**로 기울거나 뒤섞이거나 너무 어둡거나 잘못 패킹된 이미지의 가능한 해석을 순위별로 확인합니다.

아래 fixture는 원래 `BGR24` 컬러 바이트를 유지하면서 stride만 의도적으로 잘못 보고합니다. 크기는 2448 x 2048, 선언 stride는 `7344`, 실제 stride는 행마다 80바이트 패딩이 포함된 `7424`입니다.

![잘못된 stride 메타데이터에서 컬러 BGR24 해석 후보를 제시하는 Buffer Doctor](images/industrial-pcb-buffer-doctor-before.png)

최상위 `BGR24`, 2448 x 2048, stride `7424` 해석을 선택하면 디버거를 다시 왕복하지 않고 행 정렬과 원래 컬러 장면이 즉시 복구됩니다. Buffer Doctor는 현재 해석 메타데이터를 바꾸며, 중단된 애플리케이션의 원본 바이트를 수정하지 않습니다.

![올바른 stride 적용 후 컬러 BGR24 PCB 이미지를 복구한 Buffer Doctor](images/industrial-pcb-buffer-doctor-recovered.png)

## 2.0.3의 변경 사항

- .NET Framework와 최신 .NET의 `ConcurrentDictionary<TKey,TValue>` 이미지 컬렉션을 DataTip, Locals, Autos, Watch에서 직접 엽니다.
- 동시성 딕셔너리 키를 이미지 행 이름으로 유지하고, 지원하지 않는 값은 다른 항목과 분리해 오류로 표시하며, 한 번에 최대 256개만 처리합니다.
- 도킹 뷰어를 아직 열지 않은 상태에서도 등록된 `ImagePtr`를 처음부터 열 수 있도록 수정했습니다. 시각화 기능을 사용하기 전에 Tool Window를 수동으로 열 필요가 없습니다.
- 동일한 Marketplace 확장 ID로 Visual Studio 2022 `17.9+`와 안정 버전 Visual Studio 2026 `18.x` 지원을 유지합니다.

## Visual Studio 지원

| 제품 | 지원 범위 |
| --- | --- |
| Visual Studio 2022 | `17.9` 이상, x64 |
| Visual Studio 2026 | 안정 버전 `18.x`, x64 |

Community, Professional 및 Enterprise를 설치 대상으로 지원합니다. Visual Studio 2019, Visual Studio 2022 `17.8` 이하, 32비트 Visual Studio 및 Preview/Insiders 빌드는 지원 대상이 아닙니다.

`2.0.3` 패키지는 Visual Studio `17.9` SDK 하한과 `2.0.2`에서 사용한 동일한 인프로세스/아웃오브프로세스 패키지 분리 경계를 유지합니다. 설치 후보는 Visual Studio 2022 Community `17.14.37516.0`과 Visual Studio 2026 Community `18.8.12105.206`에서 새 `ConcurrentDictionary` 및 최초 실행 `ImagePtr` 시나리오를 통과했습니다. Visual Studio 2022 `17.9`는 패키지/SDK 하한과 변경되지 않은 구조의 이전 런타임 근거로 유지되며, 정확한 `2.0.3` 바이트는 현재 설치된 17.9 호스트에서 실행하지 못했습니다.

## 안전한 디버깅 동작

- 대입이 끝난 다음 줄에서 중단하십시오. 대입문 자체에 중단점을 두면 이전 값이나 null이 보일 수 있습니다.
- 포인터에는 유효한 크기, stride 또는 길이, 픽셀 형식과 함께 디버거가 중단된 동안 유지되는 수명이 필요합니다.
- Continue 또는 프로세스 종료 후 기존 행은 마지막 렌더링 픽셀만 시각적 참고로 유지하며 이전 디버기 메모리를 다시 읽지 않습니다.
- 자동 검색은 선택한 스택 프레임의 Locals와 Arguments로 제한하며 컬렉션 및 멤버 검사량에 상한을 둡니다.
- 모호하거나 지원되지 않는 레이아웃은 임의로 추측하지 않고 매핑 또는 오류 결과로 표시합니다.

## 라이선스 및 지원

Raw Buffer Visualizer는 [MIT License](https://github.com/Noah8218/RawBufferVisualizer/blob/main/LICENSE)로 제공됩니다. 외부 라이브러리는 각각의 라이선스를 유지하며 자세한 내용은 [Third-Party Notices](https://github.com/Noah8218/RawBufferVisualizer/blob/main/THIRD-PARTY-NOTICES.md)를 참고하십시오.

두 산업용 시연 사진은 모두 CC0입니다. 출처, 저자, 정확한 파일 해시 및 캡처 검증은 [산업 이미지 테스트 문서](industrial-image-testing.md)에 기록되어 있습니다. 사진에 우연히 포함된 제품 표시는 제휴나 보증을 의미하지 않습니다.

소스 코드, 문서 및 이슈 등록은 [Raw Buffer Visualizer 저장소](https://github.com/Noah8218/RawBufferVisualizer)에서 확인할 수 있습니다.
