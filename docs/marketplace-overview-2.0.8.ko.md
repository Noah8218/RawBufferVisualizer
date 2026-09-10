# Raw Buffer Visualizer

## C# 머신비전을 위한 Image Watch 방식 디버깅

중단점에서 이미지 변수와 2D 원시 버퍼를 Visual Studio 안에서 바로 확인합니다. `System.Drawing.Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, 포인터 기반 프레임, 스냅샷, 지원 컬렉션을 임시 이미지 파일이나 디버그 전용 변환 코드 없이 하나의 도킹 뷰어로 모읍니다.

![코드 DataTip에서 OpenCvSharp Mat 열기](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/raw-buffer-visualizer-datatip-open.gif)

DataTip 예시는 실제 1280 x 720 산업용 장비 이미지를 열고 이미지 카드에 정확한 `dataTipIndustrialMat` 식 이름을 유지합니다.

![Visual Studio Locals에서 실제 산업 이미지 열기](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/raw-buffer-visualizer-breakpoint-open.gif)

![Raw Buffer Visualizer 디버깅 흐름](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/raw-buffer-visualizer-demo.gif)

## 2.0.8의 새로운 기능

버전 `2.0.8`은 Visual Studio 2022 디버거에서 signed 32비트 단일 채널 행렬을 안정적으로 열도록 개선하고, 릴리스 패키지 검증을 강화하면서 기존 2.0 작업 흐름을 모두 유지합니다.

- OpenCvSharp `CV_32SC1`을 DataTip, Locals, Autos, Watch에서 `Int32`로 엽니다.
- Emgu CV `Cv32S` C1도 동일한 signed `Int32` 경로를 사용합니다.
- Automatic Inspector와 지원 Mat 컬렉션에서 해당 형식을 인식합니다.
- signed 최솟값과 최댓값을 그레이스케일로 자동 스케일링하면서 픽셀 검사에는 정확한 signed 정수와 원본 4바이트를 유지합니다.
- 포인터 이미지, 매핑된 `int[]`, 스냅샷, 바이트 순서, 패딩 stride, 샘플 미리보기, 타일 표시를 지원합니다.
- VSIX의 디버거 DLL과 새 Release 빌드 산출물의 SHA-256이 다르면 패키징이 실패합니다.
- Automatic Inspector는 최대 128개 후보를 탐색하고 처음 8개 또는 최대 2초 분량을 먼저 연 뒤 **Load next 8**, **Load all this Break**, **Stop**을 제공합니다.
- Break, F10, **Scan Now**를 반복하면 같은 행을 중복 생성하지 않고 제자리에서 갱신합니다. 형식 분석 캐시는 반복 탐색을 줄일 뿐, 매핑이 필요한 객체를 자동으로 여는 근거로 사용하지 않습니다.

![signed Int32 산업용 OpenCvSharp Mat 열기](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/int32-industrial-opencv-direct.png)

![signed Int32 행렬과 패딩 stride 프레임 자동 검사](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/int32-industrial-automatic-matrix.png)

`CV_32SC1`은 signed 32비트 정수 하나를 갖는 1채널이라는 뜻입니다. 32채널이라는 뜻이 아닙니다. `CV_32SC2`, `CV_32SC3`, `CV_32SC4` 같은 signed 다중 채널 형식은 이 릴리스에서 이미지로 해석하지 않습니다.

## 기본 사용 흐름

1. Raw Buffer Visualizer를 설치하고 Visual Studio를 다시 시작합니다.
2. 이미지 객체가 초기화된 뒤의 코드 위치에서 디버깅을 중단합니다.
3. 지원 변수의 Raw Buffer Visualizer 돋보기 항목을 선택하거나 `보기 > 다른 창 > Raw Buffer Visualizer`를 열고 Automatic Inspector를 사용합니다.
4. 이미지 행을 선택해 변수명, 크기, 픽셀 형식, stride, 소스 상태, 포인터 출처, 픽셀을 확인합니다.
5. 이미지 위에 마우스를 올려 X/Y, 채널값 또는 signed 값, 원시 바이트를 확인합니다. 필요하면 Fit, 1:1, 이동, 확대, 마커, 히스토그램, 라인 프로파일, A/B 비교, 저장을 사용합니다.

이미지는 목록에 누적됩니다. **Clear all**은 일시 중지된 프로그램의 데이터를 바꾸지 않고 전체 이미지 목록과 문서 종속 표시 상태를 초기화합니다.

## 지원 입력

| 입력 | 지원 내용 |
| --- | --- |
| `System.Drawing.Bitmap` | 8비트 인덱스, 24비트 RGB 저장, 일반적인 32비트 RGB/ARGB/PARGB |
| OpenCvSharp `Mat` | `CV_8UC1`, `CV_8UC3`, `CV_8UC4`, `CV_16UC1`, `CV_32FC1`, `CV_32SC1` |
| Emgu CV `Mat` | `Cv8U` C1/C3/C4, `Cv16U` C1, `Cv32F` C1, `Cv32S` C1 |
| `RawBufferSnapshot` | byte, unsigned-16, float-32, signed-int-32, 포인터 데이터의 관리형 스냅샷 |
| `RawBufferView` | 포인터와 명시적 너비, 높이, stride, 형식, 길이, 수명 정보 |
| 기존 응용 프로그램 프레임 래퍼 | Automatic Inspector가 안전한 디버거 노출 구조를 인식하며, 애매한 구조는 Connect Your Buffer에서 한 번 매핑 |
| 컬렉션 | 리스트, 딕셔너리, concurrent dictionary, 배열, 혼합 지원 이미지 컬렉션 |
| 스냅샷 파일 | `.rbuf.json` 설명 파일과 `.raw` 데이터 |

지원 픽셀 형식은 `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, `Int32`, 8비트 Bayer `RGGB`, `GRBG`, `GBRG`, `BGGR` 미리보기입니다.

## Automatic Inspector와 Connect Your Buffer

**Auto Inspect on Break**를 켜 두면 선택한 스택 프레임에서 초기화된 지원 Mat과 안전한 이미지 형태 객체를 찾습니다. 자동 검사가 꺼져 있어도 **Scan Now**는 사용할 수 있습니다. 결과가 많으면 제한된 탐색과 단계적 로딩을 사용하고, 팝업 대신 후보·갱신·대기·실패 수를 도킹 창에 표시합니다. 한 항목이 실패해도 오류 행으로 남고 다른 이미지는 계속 열립니다.

응용 프로그램 객체가 이미지 형태이지만 구조가 애매하면 **Connect Your Buffer**에서 Data, Width, Height, Stride, Buffer Length, Pixel Format, Valid Bits, Byte Order 역할을 검토할 수 있습니다. **Preview**는 명시적으로 실행해야 하며, **Save Mapping**을 선택해야만 편집한 매핑이 이후 동일한 중단점 흐름에 저장됩니다.

## 포인터 및 수명 안전성

렌더링 전에 크기, stride, 필요한 바이트 길이, 형식, 유효 비트, 산술 범위를 검증합니다. 라이브 포인터 행은 Continue 또는 프로세스 종료 뒤 사용할 수 없음 상태로 전환되고, 복사된 스냅샷은 유지됩니다. 접근 불가, 해제됨, 부분 읽기 메모리는 완전한 이미지처럼 표시하지 않고 명확히 실패합니다.

OpenCvSharp와 Emgu 행은 네이티브 객체 `Ptr`과 실제 이미지 바이트를 읽는 `Pixels` 주소를 구분합니다. ImagePtr와 `RawBufferView`는 소스 포인터와 픽셀 포인터가 같을 때 한 줄로 합쳐 표시합니다. 같은 주소를 다시 열면 현재 바이트를 새로 읽으며, 주소를 영구적인 객체 식별자로 취급하지 않습니다.

## Vision Buffer Doctor

원시 이미지가 기울어지거나 어둡거나 깨져 보이면 `Inspector > Interpret > Diagnose Buffer`를 엽니다. Buffer Doctor는 제한된 샘플로 가능한 크기, stride, 형식, 유효 비트, 바이트 순서를 순위화하며 일시 중지된 프로그램의 원본 바이트는 바꾸지 않습니다.

![산업용 PCB 버퍼의 Buffer Doctor 후보](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/industrial-pcb-buffer-doctor-before.png)

![올바른 패딩 stride 적용 후 복구된 컬러 PCB](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/720ceec0ec72b603a08987f0e0436b5fa0adbbb9/docs/images/industrial-pcb-buffer-doctor-recovered.png)

## Visual Studio 호환성

- Visual Studio 2022 Community, Professional, Enterprise `17.9+`, x64
- 안정판 Visual Studio 2026 `18.x`, x64
- Visual Studio 2019, 32비트 Visual Studio, Preview/Insiders 빌드는 릴리스 대상이 아닙니다.

## 개인정보 및 진단

Raw Buffer Visualizer는 로컬에서 실행됩니다. 오류 행에서 버전, 소스 형식, 오류 정보, 로컬 진단 경로를 포함한 지원 보고서를 복사할 수 있으며 이미지 페이로드와 자격 증명은 제외됩니다. 공유 전 로컬 경로를 확인하십시오.

[소스 코드와 전체 문서](https://github.com/Noah8218/RawBufferVisualizer) · [전체 변경 기록](https://github.com/Noah8218/RawBufferVisualizer/blob/main/CHANGELOG.md)
