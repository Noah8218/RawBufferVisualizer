# Raw Buffer Visualizer

> 이 문서는 게시용 영어 Overview의 한국어 검토본입니다. Visual Studio Marketplace에는 이 파일이 아니라 `marketplace-overview-1.0.53.md` 영어 원본을 사용하세요.

중단점에서 C# 머신 비전 이미지를 Visual Studio 안에서 검사합니다. Raw Buffer Visualizer는 Bitmap, OpenCvSharp/Emgu Mat, raw buffer, 포인터 및 컬렉션을 임시 저장이나 변환 코드 없이 하나의 도킹 Viewer에서 보여줍니다.

호환되는 카메라 및 frame grabber 래퍼를 자동으로 검색하고, 디버거에 노출된 멤버를 mapping하며, stride, pixel format, valid bit 및 byte order 오류를 진단할 수 있습니다.

![Visual Studio에서 실행 중인 Raw Buffer Visualizer 디버거 작업 흐름](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## 사용 방법

1. 확장 기능을 설치하고 Visual Studio를 완전히 다시 시작합니다.
2. `View > Other Windows > Raw Buffer Visualizer`를 한 번 엽니다.
3. 디버깅을 시작하고 이미지 변수 할당이 끝난 다음 중단합니다.
4. 초기화된 Mat 및 호환되는 래퍼에는 **Auto Inspect on Break** 또는 **Scan Now**를 사용하고, Bitmap 및 그 밖의 등록된 형식에는 디버거 아이콘을 사용합니다.
5. 썸네일을 선택해 픽셀, 원본 바이트, 크기, stride, 형식, 진단 정보 및 비교 보기를 확인합니다.

검사는 명시적인 사용자 동작으로만 실행됩니다. 저장된 mapping을 복원하거나 **Environment**를 열어도 현재 frame을 스캔하거나 이미지를 열지 않습니다.

## 지원하는 이미지 소스

- `System.Drawing.Bitmap`
- OpenCvSharp `Mat` 및 Emgu CV `Mat`
- `RawBufferSnapshot` 및 포인터 기반 `RawBufferView`
- 유효한 descriptor를 가진 raw managed array와 포인터
- 필요한 멤버가 디버거에 노출되는 호환 카메라 또는 frame grabber 래퍼
- 지원되는 형식 지정 또는 혼합 이미지 목록, dictionary 및 1차원 array

지원하는 pixel format은 `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32` 및 네 가지 8비트 Bayer phase입니다.

## Buffer 연결 및 레이아웃 진단

`[Map]` 후보는 **Connect Your Buffer**를 엽니다. Buffer, 크기, stride 또는 length, pixel-format 멤버를 선택하고 결과를 미리 본 다음 해당 형식의 편집 가능한 mapping을 저장합니다. 명시적 래퍼가 더 적합한 경우 제조사 중립 `RawBufferView` 시작 코드도 사용할 수 있습니다.

**Diagnose Buffer**는 기울어지거나 뒤섞이거나 어둡거나 잘못 packed된 이미지에 가능한 레이아웃의 우선순위를 정합니다. 후보를 적용하고 비교할 수 있습니다. RGB/BGR 순서와 Bayer phase는 모호할 수 있으므로 개발자 확인이 필요합니다.

## Viewer 및 비교 도구

- 모든 지원 소스를 위한 하나의 도킹 이미지 목록
- 종횡비를 유지하는 Fit, 1:1, 휠 확대/축소, 드래그 이동 및 고배율 픽셀 오버레이
- X/Y, GV/RGB 값, 채널 swatch, 원본 바이트, hover 통계, marker, line profile, histogram 및 진단 정보
- A/B, 연결 보기, 분할, 절대 차이 및 blink 비교
- PNG 및 raw snapshot 내보내기
- 매우 큰 raw payload를 위한 파일 기반 tiled display

## 1.0.53의 새로운 기능

- **Connect Your Buffer**에 mapping 미리보기, 저장, 복원, 편집 및 초기화를 추가했습니다.
- **Environment Check**를 Visual Studio 호스트, 로드된 확장 버전 및 임시 저장소 상태로 단순화했습니다. **Environment**를 다시 선택하면 닫힙니다.
- **Refresh**와 **Copy diagnostic report**는 frame을 스캔하거나 이미지를 열거나 소프트웨어를 설치하거나 확장 등록을 변경하지 않습니다.
- **What's New**를 다시 선택하면 릴리스 주요 내용이 닫히며, **Dismiss**는 현재 버전을 확인한 것으로도 기록합니다.
- 기존 Visual Studio 2026, Mat 컬렉션 및 디버거 전달 동작을 바꾸지 않고 매우 큰 포인터 기반 이미지의 첫 미리보기를 개선했습니다.

## Visual Studio 지원

| 제품 | 지원 범위 |
| --- | --- |
| Visual Studio 2022 | `17.14` 이상, x64 |
| Visual Studio 2026 | 안정판 `18.x`, x64 |

Community, Professional, Enterprise가 설치 대상입니다. Visual Studio 2019, Visual Studio 2022 `17.9`-`17.13`, 32비트 Visual Studio 및 Preview/Insiders 빌드는 지원 대상이 아닙니다.

## 안전 및 제한 사항

- 할당이 끝난 뒤 중단하세요. 할당문 자체에 설정한 중단점에서는 이전 값이나 null 값이 보일 수 있습니다.
- 자동 검색은 선택한 stack frame의 Locals와 Arguments만 제한된 범위에서 스캔합니다.
- 유효한 크기, stride 또는 length, pixel format 및 lifetime 정보가 없는 raw pointer는 안전하게 열 수 없습니다.
- 자동 검색은 임의의 제조사 메서드를 호출하거나 카메라 SDK DLL을 로드하거나 비공개 native 레이아웃을 해석하지 않습니다.
- 카메라 영상 취득과 장치 제어는 이 확장 기능의 범위가 아닙니다. 범용 buffer 검사는 제조사 인증을 의미하지 않습니다.
- Environment 보고서에는 자격 증명, 환경 변수 값 및 이미지 payload가 포함되지 않습니다. 로컬 경로는 포함될 수 있으므로 공유하기 전에 내용을 확인하세요.

## 라이선스 및 지원

Raw Buffer Visualizer는 [MIT License](https://github.com/Noah8218/RawBufferVisualizer/blob/main/LICENSE)로 배포됩니다. 외부 라이브러리는 각자의 라이선스를 따릅니다. 자세한 내용은 [Third-Party Notices](https://github.com/Noah8218/RawBufferVisualizer/blob/main/THIRD-PARTY-NOTICES.md)를 확인하세요.

소스 코드, 문서 및 문제 제보는 [Raw Buffer Visualizer 저장소](https://github.com/Noah8218/RawBufferVisualizer)에서 확인할 수 있습니다.
