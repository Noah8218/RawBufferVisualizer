# Raw Buffer Visualizer

## C# 머신비전을 위한 Image Watch 방식 디버깅

중단점에서 이미지 변수와 2D 원시 버퍼를 Visual Studio 안에서 바로 확인합니다. `System.Drawing.Bitmap`, OpenCvSharp `Mat`, Emgu CV `Mat`, 포인터 기반 프레임, 스냅샷, 지원 컬렉션을 임시 이미지 파일이나 디버그 전용 변환 코드 없이 하나의 도킹 뷰어로 모읍니다.

![코드 DataTip에서 OpenCvSharp Mat 열기](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/33b0a0b4855ce02aeed05f0726bf0071418611fa/docs/images/raw-buffer-visualizer-datatip-open.gif)

![Visual Studio Locals에서 실제 산업 이미지 열기](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/33b0a0b4855ce02aeed05f0726bf0071418611fa/docs/images/raw-buffer-visualizer-breakpoint-open.gif)

![Raw Buffer Visualizer 디버깅 흐름](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/33b0a0b4855ce02aeed05f0726bf0071418611fa/docs/images/raw-buffer-visualizer-demo.gif)

## 2.0.7의 새로운 기능

버전 `2.0.7`은 signed 32비트 단일 채널 행렬 시각화를 추가합니다.

- OpenCvSharp `CV_32SC1`을 DataTip, Locals, Autos, Watch에서 `Int32`로 엽니다.
- Emgu CV `Cv32S` C1도 동일한 `Int32` 경로를 사용합니다.
- Automatic Inspector와 지원 Mat 컬렉션에서도 새 형식을 인식합니다.
- signed 최솟값과 최댓값을 그레이스케일로 자동 스케일링하여 라벨 맵과 정수 결과 영상을 확인할 수 있습니다.
- 픽셀 검사에는 정확한 signed 정수값과 원본 4바이트가 유지됩니다.
- 명시적 리틀/빅 엔디언, 포인터 이미지, 매핑된 `int[]`, 스냅샷, 샘플 미리보기, 타일 표시, 패딩 stride를 지원합니다.

![실제 산업용 OpenCvSharp CV_32SC1 Mat 열기](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/33b0a0b4855ce02aeed05f0726bf0071418611fa/docs/images/int32-industrial-opencv-direct.png)

위 화면은 설치된 2.0.7 확장에서 실제 산업용 PCB 원본을 signed `Int32`로 표시한 결과입니다. 상태 표시줄에는 정확한 signed 픽셀값과 원본 4바이트가 유지됩니다.

![signed Int32 행렬과 패딩 stride 프레임 자동 검사](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/33b0a0b4855ce02aeed05f0726bf0071418611fa/docs/images/int32-industrial-automatic-matrix.png)

Automatic Inspector는 OpenCvSharp, Emgu CV, 매핑된 포인터, 스냅샷, 컬렉션, 패딩 stride 형태를 동일한 이미지 목록으로 엽니다.

`CV_32SC1`은 **signed 32비트 정수 하나를 갖는 1채널**이라는 뜻입니다. 32채널이라는 뜻이 아닙니다. `CV_32SC2`, `CV_32SC3`, `CV_32SC4` 같은 signed 다중 채널 형식은 이 버전에서 이미지로 해석하지 않습니다.

## 기본 사용 흐름

1. Raw Buffer Visualizer를 설치하고 Visual Studio를 다시 시작합니다.
2. 이미지 객체가 초기화된 코드 위치에서 디버깅을 중단합니다.
3. 지원 변수의 Raw Buffer Visualizer 돋보기 항목을 선택하거나 `보기 > 다른 창 > Raw Buffer Visualizer`를 열고 Automatic Inspector를 사용합니다.
4. 이미지 행을 선택해 크기, 픽셀 형식, stride, 소스 상태, 포인터 출처, 픽셀을 확인합니다.
5. 이미지 위에 마우스를 올려 X/Y, 채널값 또는 signed 값, 원시 바이트를 확인합니다. 필요하면 Fit, 1:1, 이동, 확대, 마커, 히스토그램, 라인 프로파일, A/B 비교, 저장을 사용합니다.

이미지는 목록에 누적됩니다. **Clear all**은 일시 중지된 프로그램의 데이터를 바꾸지 않고 뷰어의 전체 목록과 문서 종속 표시 상태를 초기화합니다.

## 지원 입력

| 입력 | 지원 내용 |
| --- | --- |
| `System.Drawing.Bitmap` | 8비트 인덱스, 24비트 RGB 저장, 일반적인 32비트 RGB/ARGB/PARGB |
| OpenCvSharp `Mat` | `CV_8UC1`, `CV_8UC3`, `CV_8UC4`, `CV_16UC1`, `CV_32FC1`, `CV_32SC1` |
| Emgu CV `Mat` | `Cv8U` C1/C3/C4, `Cv16U` C1, `Cv32F` C1, `Cv32S` C1 |
| `RawBufferSnapshot` | byte, unsigned-16, float-32, signed-int-32, 포인터 데이터의 관리형 스냅샷 |
| `RawBufferView` | 포인터와 명시적 너비, 높이, stride, 형식, 길이, 수명 정보 |
| 기존 카메라/프레임 래퍼 | Automatic Inspector가 디버거에서 안전하게 보이는 레이아웃을 인식하며, 애매한 경우 Connect Your Buffer에서 한 번 매핑 |
| 컬렉션 | 리스트, 딕셔너리, concurrent dictionary, 배열, 혼합 지원 이미지 컬렉션 |
| 스냅샷 파일 | `.rbuf.json` 설명 파일과 `.raw` 데이터 |

OpenCvSharp 호환성은 `4.0.0.20181225`, `4.2.0.20200208`, `4.5.5.20211231`, `4.8.0.20230708`, `4.13.0.20260627`에서 확인합니다. Emgu CV는 `3.4.3.3016`, `4.2.0.3662`, `4.5.5.4823`, `4.8.1.5350`, `4.13.0.5924`에서 확인합니다.

## 지원 픽셀 형식

`Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`, `Int32`, 8비트 Bayer `RGGB`, `GRBG`, `GBRG`, `BGGR` 미리보기를 지원합니다.

`Int32` 표시는 전체 이미지 또는 제한된 미리보기 샘플의 signed 범위를 그레이스케일로 변환합니다. 원본 데이터는 바뀌지 않으며, Inspector에는 실제 signed 값과 저장된 4바이트가 그대로 표시됩니다.

## Automatic Vision Inspector

**Auto Inspect on Break**를 켜 두면 현재 스택 프레임에서 초기화된 지원 Mat과 안전한 이미지 형태 객체를 찾습니다. 자동 검사가 꺼져 있어도 **Scan Now**는 사용할 수 있습니다.

- `[Auto]`: 검증 후 정상적으로 열림
- `[Map]`: 멤버 또는 enum을 한 번 매핑해야 함
- `[Failed]`: 인식했지만 현재 데이터를 안전하게 읽을 수 없음

한 항목의 실패가 다른 이미지 열기를 막지 않습니다. OpenCvSharp/Emgu의 정확한 Mat 리스트와 1차원 배열은 별도 컬렉션 옵션 없이 자동 포함됩니다.

## 버퍼 및 포인터 안전성

렌더링 전에 크기, stride, 필요한 바이트 길이, 형식, 유효 비트, 산술 범위를 검증합니다. 라이브 포인터 행은 Continue 또는 프로세스 종료 뒤 사용할 수 없음 상태로 전환되고, 복사된 스냅샷은 유지됩니다. 접근 불가, 해제됨, 부분 읽기 메모리는 완전한 이미지처럼 표시하지 않고 명확히 실패합니다.

OpenCvSharp와 Emgu 행은 네이티브 객체 `Ptr`과 실제 이미지 바이트를 읽는 `Pixels` 주소를 구분합니다. ImagePtr와 `RawBufferView`는 소스 포인터와 픽셀 포인터가 같을 때 한 줄로 합쳐 표시합니다.

## Vision Buffer Doctor

원시 이미지가 기울어지거나 어둡거나 깨져 보이면 `Inspector > Interpret > Diagnose Buffer`를 엽니다. Buffer Doctor는 제한된 샘플을 사용해 가능한 크기, stride, 형식, 유효 비트, 바이트 순서를 순위화합니다. 후보를 선택해도 일시 중지된 프로그램의 원본 바이트는 바뀌지 않습니다.

![산업용 PCB 버퍼의 Buffer Doctor 후보](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/33b0a0b4855ce02aeed05f0726bf0071418611fa/docs/images/industrial-pcb-buffer-doctor-before.png)

![올바른 패딩 stride 적용 후 복구된 컬러 PCB](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/33b0a0b4855ce02aeed05f0726bf0071418611fa/docs/images/industrial-pcb-buffer-doctor-recovered.png)

## Visual Studio 호환성

- Visual Studio 2022 `17.9` 이상, x64
- 안정 버전 Visual Studio 2026 `18.x`, x64
- Community, Professional, Enterprise 에디션

2.0.7 설치본 런타임 검증은 Visual Studio 2022 `17.14.37516.0`에서 완료했습니다. 정확한 Visual Studio 2022 `17.9` 및 안정 버전 Visual Studio 2026의 2.0.7 런타임은 이번 후보 환경에서 확인하지 못했지만, 선언된 호환 대상과 17.9 SDK 경계는 변경하지 않았습니다.

일반 확장 사용자가 별도로 준비할 프로그램은 지원되는 Visual Studio뿐입니다. 디버깅 대상 애플리케이션이 사용하는 라이브러리는 해당 애플리케이션의 구성에 속합니다.

## 범위

Raw Buffer Visualizer는 디버깅 중 이미 존재하는 2D 이미지 메모리를 검사합니다. 카메라 취득·제어, 조명, PLC/I/O, 3D 포인트 클라우드 또는 깊이 컨테이너 시각화는 범위에 포함되지 않습니다.

확장은 소스 코드, 이미지 데이터, 디버거 값을 업로드하지 않습니다. 진단 보고서는 공유 전에 검토할 수 있도록 로컬에서 생성됩니다.

[소스, 문서, 이슈 트래커](https://github.com/Noah8218/RawBufferVisualizer)
