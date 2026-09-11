# portfolio

## 소개
제출용으로 정리한 코드파일묶음입니다.

## 프로젝트 목록
| 폴더 | 한 줄 요약 | 분야 | 핵심 기술 | 형태 |
|---|---|---|---|---|
| [esw-contest-ieum](esw-contest-ieum/) | 4채널 마이크로 방향을 추정해 특정 화자만 분리하고 한국어 실시간 자막을 생성 | 임베디드 · 음성 | ROS 2 Humble, Jetson Orin Nano, PyTorch, BlazeFace | 임베디드 시스템 |
| [digitaltwin-emolamp](digitaltwin-emolamp/) | 실물 LED 램프와 Unity 가상 램프를 서버로 실시간 동기화하는 디지털 트윈 | IoT · 디지털 트윈 | Unity 2022.3, Arduino, Node.js, Firebase | 데스크톱 앱 + 서버 + 하드웨어 |
| [coss-contest-medibox](coss-contest-medibox/) | IR 센서로 복약 여부를 기록하고 미복약 시 보호자에게 이메일 알림 | IoT · 헬스케어 | Arduino R4 WiFi, Node.js, Express, MariaDB, JWT | 웹 서비스 + 하드웨어 |
| [xr-contest-ieum](xr-contest-ieum/) | Quest 3에서 전통 목공 도구로 부재를 가공해 숭례문 공포를 조립하는 VR 체험 | XR | Unity 6000.3, URP, XR Interaction Toolkit, Addressables | VR 앱 |
| [food-save-web](food-save-web/) | 남는 음식을 등록하고 필요한 사람이 예약·수령하도록 연결하는 푸드 쉐어링 | 웹 서비스 | Node.js, Express, MySQL, JWT, multer | 웹 서비스 |
| [cau-green-campus](cau-green-campus/) | 탄소발자국을 계산하고 캠퍼스 실천 방법을 안내하는 웹 플랫폼 | 웹 서비스 | Node.js, Express, 정적 HTML/CSS/JS | 웹 서비스 |

## 기술 스택 요약

| 기술 | esw | digitaltwin | coss | xr | food-save | cau |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| **Unity** | | 2022.3.62f3 | | 6000.3.9f1 | | |
| **C#** | | O | | O | | |
| **Python** | O | | | | | |
| **ROS 2 Humble** | O | | | | | |
| **Node.js / Express** | | O | O | | O | O |
| **Arduino / C++** | | O | O | | | |
| **XR (OpenXR, XRI)** | | | | O | | |
| **MySQL / MariaDB** | | | O | | O | |
| **Firebase** | | O | | | | |
| **JWT 인증** | | | O | | O | |
| **딥러닝 모델** | O | | | | | |
| **외부 AI API** | | O | | O | | |
| **정적 프런트엔드** | O | O | O | | O | O |
| **Git LFS** | | | | O | | |

## 저장소 구성

각 프로젝트 폴더가 독립된 git 저장소입니다. 이 상위 폴더는 저장소가 아닙니다.

정리 작업 기록은 [cleanup_report.md](cleanup_report.md), 이전 상위 README는 [docs/README_old.md](docs/README_old.md)에 있습니다.
