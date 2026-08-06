# Raw Buffer Visualizer

> 이 문서는 Marketplace에 게시할 영어 Overview의 한국어 검토본입니다. 실제 Marketplace에는 `marketplace-overview-2.0.0.md` 영어 원문을 사용합니다.

C# 머신비전 이미지와 raw 2D 버퍼를 중단점에서 Visual Studio 안에서 바로 검사합니다. Raw Buffer Visualizer는 임시 이미지 저장이나 디버그 전용 변환 코드 없이 Bitmap, OpenCvSharp/Emgu Mat, 포인터, managed buffer와 지원 컬렉션을 하나의 docked viewer에 모아 줍니다.

![Visual Studio에서 실제 산업용 PCB 이미지를 검사하는 Raw Buffer Visualizer](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/industrial-pcb-auto-inspector-pixel.png)

동일한 실제 PCB 장면을 지원되는 네 가지 live 표현으로 열었으며, 하나의 docked window에서 픽셀 값, 원본 바이트, 주변 통계, 크기, stride와 형식을 함께 확인할 수 있습니다.

## 사용 방법

1. 확장을 설치하고 Visual Studio를 완전히 다시 시작합니다.
2. `View > Other Windows > Raw Buffer Visualizer`를 한 번 엽니다.
3. 디버깅을 시작하고 이미지 변수가 할당된 뒤 중단합니다.
4. 초기화된 Mat과 호환 wrapper에는 **Auto Inspect on Break** 또는 **Scan Now**를 사용하고, 등록된 형식에는 디버거 visualizer 아이콘을 사용합니다.
5. 썸네일을 선택해 픽셀, 원본 바이트, 크기, stride, 형식, 진단과 비교 화면을 확인합니다.

Preview와 scan은 사용자가 명시적으로 실행할 때만 동작합니다. 저장된 mapping 복원, 패널 열기/닫기 또는 표시 상태 변경은 현재 frame을 scan하거나 이미지를 열지 않습니다.

![Visual Studio에서 실행 중인 Raw Buffer Visualizer 디버거 작업 흐름](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/raw-buffer-visualizer-demo.gif)

## 지원 이미지 소스

- `System.Drawing.Bitmap`
- OpenCvSharp `Mat`과 Emgu CV `Mat`
- `RawBufferSnapshot`과 포인터 기반 `RawBufferView`
- mapping된 `byte[]`, `ushort[]`, `float[]` 버퍼
- 크기, stride 또는 정확한 length, pixel format과 lifetime이 명시된 mapping 포인터 객체
- 지원되는 typed/mixed 이미지 목록, dictionary와 1차원 배열

지원 pixel format은 `Mono8`, `Mono16`, `Mono10PackedLsb`, `Mono12PackedLsb`, `Binary`, `RGB24`, `BGR24`, `BGRA32`, `Float32`와 네 가지 8비트 Bayer phase입니다.

## Connect Your Buffer

호환 객체가 직접 등록되어 있지 않으면 **Connect Your Buffer**에서 디버거에 보이는 멤버를 buffer, width, height, stride 또는 length, format, valid bits와 byte order 역할에 연결할 수 있습니다. 결과를 명시적으로 Preview합니다. 해석이 여전히 잘못 보이면 같은 대화상자의 **Diagnose interpretation**에서 제한된 범위의 대안을 순위별로 확인할 수 있습니다. 후보 선택은 보이는 초안과 Preview만 바꾸며, **Save Mapping**을 선택해야만 해당 형식의 편집 가능한 mapping으로 저장됩니다. 명시적인 wrapper가 더 분명할 때는 중립 `RawBufferView` 시작 코드를 사용할 수 있습니다.

등록 소스와 mapping 소스는 같은 checked 검증 계약을 사용합니다. 잘못된 크기, 부족한 stride, 짧은 buffer length, 정의되지 않은 format/order 값, descriptor 산술 overflow 또는 호환되지 않는 valid bits는 추측하지 않고 전송 전에 실패합니다.

## 진단과 비교

- X/Y, GV/RGB 값, 채널 색상, 원본 바이트, hover 통계, marker, line profile, histogram과 진단 정보를 확인합니다.
- 종횡비를 유지하는 Fit, 1:1, 휠 확대/축소, drag pan과 고배율 pixel overlay를 사용합니다.
- 연결 보기, split, 절대 차이와 blink 모드로 A/B 이미지를 비교합니다.
- **Diagnose Buffer**로 기울어지거나 깨지거나 어둡거나 packed 해석이 잘못된 이미지의 가능한 layout을 순위별로 확인합니다.
- PNG 이미지와 raw snapshot을 내보냅니다.
- 매우 큰 raw payload는 file-backed tiled viewer로 엽니다.

잘못된 stride metadata는 동일한 grayscale PCB 장면을 기울어지게 표시하며, Buffer Doctor는 순위가 매겨진 대안을 계속 보여 줍니다.

![잘못된 stride metadata가 적용된 산업용 PCB 이미지의 해석 후보](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/industrial-pcb-buffer-doctor-before.png)

최상위 `Mono8`, 2448 x 2048, stride `2560` 해석을 선택하면 추가 디버거 왕복 없이 이미지가 복구됩니다.

![올바른 stride를 적용해 복구된 산업용 PCB 이미지](https://raw.githubusercontent.com/Noah8218/RawBufferVisualizer/main/docs/images/industrial-pcb-buffer-doctor-recovered.png)

## 2.0.0의 새로운 내용

- mapping 대화상자에 format, stride, valid bits와 byte order 후보를 순위별로 보여 주고 명시적인 저장 경계를 유지하는 Connect Doctor를 추가했습니다.
- 등록 view, mapping 포인터와 mapping managed buffer에 적용되는 하나의 문서화된 2D 호환성 계약을 추가했습니다.
- 등록 및 mapping metadata가 dimension, stride, length, format/order enum과 valid-bits를 같은 checked fail-closed 규칙으로 검증합니다.
- `Mono16`의 1~16 valid bits를 유지하며 실행 가능한 fixture가 10, 12, 14, 16을 검증합니다.
- `Mono10PackedLsb`와 `Mono12PackedLsb`는 고정 layout과 맞지 않는 valid-bits를 거부합니다.
- 중립 fixture가 독점 SDK 없이 descriptor, 전송 바이트, byte order와 포인터 소유권을 검증합니다.
- Continue 또는 debuggee 종료 뒤에는 live process-backed row를 `Unavailable`로 전환하고 이후 source read를 차단합니다. Visual Studio가 이미 복사해 소유하는 managed buffer는 계속 사용할 수 있습니다.
- Continue 전에 예약된 지연 handoff는 Run Mode나 다음 Break에서 다시 열리지 않습니다.

## Visual Studio 지원

| 제품 | 지원 범위 |
| --- | --- |
| Visual Studio 2022 | `17.14` 이상, x64 |
| Visual Studio 2026 | 안정 버전 `18.x`, x64 |

Community, Professional, Enterprise를 지원합니다. Visual Studio 2019, Visual Studio 2022 `17.9`~`17.13`, 32비트 Visual Studio와 Preview/Insiders 빌드는 지원 대상이 아닙니다.

## 안전과 제한

- 값 할당이 끝난 뒤 중단하십시오. 할당문 자체의 중단점에서는 이전 값이나 null이 보일 수 있습니다.
- 포인터에는 유효한 크기, stride 또는 length, pixel format과 디버기가 중단된 동안 유지되는 lifetime이 필요합니다.
- Continue 또는 process 종료 뒤 live row에는 마지막으로 렌더링된 픽셀만 참고용으로 남을 수 있으며 debuggee source를 다시 읽지 않습니다. 새 live source가 필요하면 유효한 중단점에서 다시 열거나 scan하십시오.
- 자동 검색은 선택한 stack frame의 Locals와 Arguments만 제한된 범위에서 검사합니다.
- 모호하거나 compressed, planar, YUV, 미지원 packed, offset 또는 method-only layout은 추측하지 않고 명확히 실패합니다.
- 카메라 획득/제어, 조명, PLC/I/O, 3D point cloud와 depth/coordinate container는 이 확장의 범위가 아닙니다.
- 범용 buffer 검사는 특정 카메라, frame grabber, driver, SDK 또는 하드웨어 인증을 의미하지 않습니다.

## 라이선스와 지원

Raw Buffer Visualizer는 [MIT License](https://github.com/Noah8218/RawBufferVisualizer/blob/main/LICENSE)로 배포됩니다. 외부 라이브러리는 각 라이선스를 따르며 [Third-Party Notices](https://github.com/Noah8218/RawBufferVisualizer/blob/main/THIRD-PARTY-NOTICES.md)에서 확인할 수 있습니다.

PCB 데모 사진은 CC0이며 출처와 정확한 파일 검증은 [산업용 이미지 테스트 문서](https://github.com/Noah8218/RawBufferVisualizer/blob/main/docs/industrial-image-testing.md)에 기록되어 있습니다. 사진에 우연히 포함된 제품 표시는 제휴나 보증을 의미하지 않습니다.

소스, 문서와 이슈 등록은 [Raw Buffer Visualizer 저장소](https://github.com/Noah8218/RawBufferVisualizer)에서 확인할 수 있습니다.
