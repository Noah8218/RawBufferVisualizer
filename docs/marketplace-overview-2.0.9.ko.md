# Raw Buffer Visualizer

## C# 머신 비전용 Image Watch 방식 디버깅

Visual Studio 중단점에서 이미지 변수와 원시 2D 버퍼를 직접 검사합니다. Raw Buffer Visualizer는 `System.Drawing.Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, 포인터 기반 프레임, 스냅샷, 지원되는 컬렉션을 디버그용 이미지 변환 코드 없이 하나의 도킹 뷰어에서 엽니다.

![코드 DataTip에서 OpenCvSharp Mat 열기](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/raw-buffer-visualizer-datatip-open.gif)

DataTip 예시는 실제 1280 x 720 산업 장비 이미지를 열고 이미지 카드에 정확한 `dataTipIndustrialMat` 식 이름을 유지합니다.

![Visual Studio Locals에서 실제 산업 이미지 열기](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/raw-buffer-visualizer-breakpoint-open.gif)

![Raw Buffer Visualizer 디버깅 작업 흐름](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/raw-buffer-visualizer-demo.gif)

## 2.0.9의 변경 사항

버전 `2.0.9`는 Visual Studio 디버거 시각화 도우미의 네이티브 이미지 전송 경로를 수정합니다.

- 8 MiB 이상의 포인터 기반 OpenCvSharp Mat, Emgu CV Mat, ImagePtr, RawBufferView는 전체 이미지를 디버거 RPC로 반복 직렬화하지 않고 검증된 라이브 프로세스 메모리 읽기 경로를 사용합니다.
- 추론된 ROI와 서브매트릭스 범위는 `stride * (height - 1) + 최소 행 바이트`로 계산합니다. 마지막 행 뒤에 존재하지 않는 패딩을 읽지 않습니다.
- 호출자가 명시한 버퍼 길이는 그대로 우선합니다.
- 읽을 수 없거나 해제됐거나 일부만 읽히거나 산술 범위를 넘는 네이티브 메모리는 계속 차단합니다.
- 작은 스냅샷은 기존 4 MiB 청크 경로를 유지하며 디버깅을 계속하거나 프로세스가 종료된 뒤에도 볼 수 있습니다.
- 디버거 RPC 실패 화면에는 작업 단계, 소스 또는 청크, 해당하는 바이트 오프셋과 크기, 예외 형식, HRESULT를 표시합니다. 원래 지역화된 디버거 예외는 로컬 지원 보고서에 보존합니다.
- 같은 식에서 같은 기술 원인으로 다시 실패하면 새 오류 행을 계속 추가하지 않고 기존 행의 보고서 ID와 최신 세부 정보를 갱신합니다.
- 서로 다른 등록 시각화 도우미를 연속으로 열어도 사용자가 고정한 Raw Buffer Visualizer 도킹 창은 닫히거나 교체되지 않습니다.
- Visual Studio 2022 `17.9+`와 안정판 Visual Studio 2026 `18.x` 지원 범위는 바뀌지 않습니다.

라이브 소스 행에는 프로세스 ID, 소스 포인터, 픽셀 주소, `LIVE` 상태가 기록됩니다. 실행을 계속하거나 프로세스를 종료하면 해당 행은 `UNAVAILABLE`로 바뀌며 해제된 메모리를 읽지 않습니다. 복사된 스냅샷은 `CAPTURED` 상태로 계속 볼 수 있습니다.

## 기본 작업 흐름

1. Raw Buffer Visualizer를 설치하고 Visual Studio를 다시 시작합니다.
2. 디버깅을 시작하고 이미지 객체가 초기화된 다음 줄에서 멈춥니다.
3. 지원되는 변수의 돋보기 메뉴에서 Raw Buffer Visualizer를 선택하거나 `View > Other Windows > Raw Buffer Visualizer`를 열어 Automatic Inspector를 사용합니다.
4. 이미지 행을 선택하여 식 이름, 크기, 형식, 스트라이드, 소스 상태, 포인터 출처, 픽셀을 확인합니다.
5. 이미지 위에 마우스를 올려 X/Y, 채널 또는 부호 있는 값, 원시 바이트를 확인합니다. 필요하면 Fit, 1:1, 이동, 확대/축소, 마커, 히스토그램, 라인 프로파일, A/B 비교, 저장을 사용합니다.

이미지는 목록에 누적됩니다. **Clear all**은 일시 중지된 프로그램의 데이터를 변경하지 않고 전체 이미지 목록과 문서별 표시 상태를 초기화합니다.

## 지원 입력

| 입력 | 지원 범위 |
| --- | --- |
| `System.Drawing.Bitmap` | 8비트 인덱스, 24비트 RGB 저장 방식, 일반적인 32비트 RGB/ARGB/PARGB 형식 |
| OpenCvSharp `Mat` | `CV_8UC1`, `CV_8UC3`, `CV_8UC4`, `CV_16UC1`, `CV_32FC1`, `CV_32SC1` |
| Emgu CV `Mat` | `Cv8U` C1/C3/C4, `Cv16U` C1, `Cv32F` C1, `Cv32S` C1 |
| `RawBufferSnapshot` | byte, unsigned-16, float-32, signed-int-32, 포인터 데이터로 만든 관리형 스냅샷 |
| `RawBufferView` | 포인터와 명시적인 너비, 높이, 스트라이드, 형식, 길이, 수명 정보 |
| 기존 애플리케이션 프레임 래퍼 | Automatic Inspector가 디버거에서 확인 가능한 안전한 구조를 인식하며, 모호한 구조는 Connect Your Buffer에서 한 번 매핑 가능 |
| 컬렉션 | 리스트, 딕셔너리, 동시성 딕셔너리, 배열, 혼합 지원 이미지 컬렉션 |
| 스냅샷 파일 | `.rbuf.json` 설명자와 `.raw` 페이로드 |

지원 픽셀 형식은 `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, `Int32`, 8비트 Bayer `RGGB`, `GRBG`, `GBRG`, `BGGR` 미리보기입니다.

`CV_32SC1`은 부호 있는 32비트 정수 한 채널을 뜻하며 32채널이 아닙니다. `CV_32SC2`, `CV_32SC3`, `CV_32SC4` 같은 부호 있는 다중 채널 형식은 이번 버전에서 이미지로 해석하지 않습니다.

## Automatic Inspector와 Connect Your Buffer

**Auto Inspect on Break**를 켜 두면 현재 스택 프레임에서 초기화된 지원 Mat와 안전한 이미지 유사 객체를 찾습니다. 자동 검사를 꺼도 **Scan Now**는 사용할 수 있습니다. 후보가 많으면 탐색과 로딩을 제한된 단위로 진행하고 후보·갱신·대기·실패 수를 표시합니다.

- `[Auto]`: 안전 검증 후 열림
- `[Map]`: 멤버 또는 열거형을 한 번 매핑해야 함
- `[Failed]`: 객체는 인식했지만 현재 값을 안전하게 읽지 못함

한 항목의 실패가 다른 이미지 로딩을 막지 않습니다. 반복되는 Break, F10, **Scan Now**에서는 동일한 자동 행을 중복 추가하지 않고 현재 값으로 갱신합니다.

구조가 모호한 이미지 객체는 **Connect Your Buffer**에서 Data, Width, Height, Stride, Buffer Length, Pixel Format, Valid Bits, Byte Order 역할을 확인할 수 있습니다. **Preview**는 사용자가 눌러야 실행되며 **Save Mapping**을 선택해야 이후 같은 형식의 중단점에 매핑이 저장됩니다.

## 포인터와 메모리 수명 안전

렌더링 전에 크기, 스트라이드, 최소 행 바이트, 필요한 메모리 범위, 형식, 유효 비트, 산술을 검증합니다. OpenCvSharp와 Emgu 행은 네이티브 객체의 `Ptr`과 실제 이미지 바이트를 읽는 `Pixels` 주소를 구분합니다. ImagePtr와 RawBufferView는 두 주소가 같으면 하나의 결합 주소로 표시합니다.

이미지를 열 때마다 해당 주소의 현재 바이트를 다시 읽습니다. 같은 주소가 재사용됐다고 이전 이미지 내용을 재사용하지 않습니다. 라이브 메모리를 읽는 동안에는 디버기가 중단된 상태이고 해당 메모리가 유효해야 합니다.

## Vision Buffer Doctor

원시 이미지가 기울거나, 너무 어둡거나, 뒤섞이거나, 잘못 패킹된 것처럼 보이면 `Inspector > Interpret > Diagnose Buffer`를 엽니다. Buffer Doctor는 중단된 프로그램의 바이트를 변경하지 않고 제한된 샘플에서 가능한 크기, 스트라이드, 형식, 유효 비트, 바이트 순서를 순위로 제시합니다.

![산업용 PCB 버퍼의 Buffer Doctor 후보](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/industrial-pcb-buffer-doctor-before.png)

![올바른 패딩 스트라이드를 적용해 복구한 컬러 PCB 이미지](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/industrial-pcb-buffer-doctor-recovered.png)

## Visual Studio 호환성

- Visual Studio 2022 Community, Professional, Enterprise `17.9+`, x64
- 안정판 Visual Studio 2026 `18.x`, x64
- Visual Studio 2019, 32비트 Visual Studio, Preview/Insiders 빌드는 릴리스 대상이 아닙니다.

일반 확장 사용자가 별도로 준비할 소프트웨어는 Visual Studio와 이 확장뿐입니다. 디버깅 대상 애플리케이션이 사용하는 이미지 라이브러리는 해당 애플리케이션에 포함됩니다.

## 개인정보 및 진단

Raw Buffer Visualizer는 로컬에서 실행됩니다. 오류 행에서 버전, 소스 형식, 기술 오류 정보, 로컬 진단 경로가 포함된 지원 보고서를 복사할 수 있습니다. 이미지 페이로드와 자격 증명은 포함하지 않으며, 공유 전 로컬 경로를 확인해야 합니다.

[소스 코드와 전체 문서](https://github.com/Noah8218/RawBufferVisualizer) · [전체 변경 기록](https://github.com/Noah8218/RawBufferVisualizer/blob/main/CHANGELOG.md)
