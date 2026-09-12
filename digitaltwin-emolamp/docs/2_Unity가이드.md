# EmoLamp Unity 완벽 가이드

## 1. 프로젝트 설정

### 1.1 새 프로젝트 생성 (또는 기존 프로젝트 사용)
```
Unity Hub → New Project
- Template: 3D (URP) 또는 3D
- Project Name: EmoLamp
- Location: 원하는 경로
```

### 1.2 필수 패키지 설치

**Window → Package Manager**에서:

1. **TextMeshPro** (기본 포함)
   - 없으면 `com.unity.textmeshpro` 검색 후 설치

2. **Newtonsoft Json**
   - + 버튼 → Add package by name
   - `com.unity.nuget.newtonsoft-json` 입력 → Add

### 1.3 프로젝트 설정

**Edit → Project Settings → Player**

```
Other Settings:
├── Configuration
│   ├── Api Compatibility Level: .NET Framework ✓
│   └── Allow 'unsafe' Code: ✓ (선택)
│
└── Other Settings (아래쪽)
    └── Allow downloads over HTTP: Always allowed ✓
```

---

## 2. 폴더 구조 생성

**Assets 폴더에서 우클릭 → Create → Folder**

```
Assets/
├── Scripts/
│   ├── API/
│   │   ├── WeatherManager.cs
│   │   ├── ClaudeManager.cs
│   │   ├── FirebaseManager.cs
│   │   └── MQTTManager.cs
│   ├── Core/
│   │   ├── HSVController.cs
│   │   ├── LampController.cs
│   │   ├── BackgroundController.cs
│   │   └── SerialController.cs
│   └── UI/
│       └── UIManager.cs
├── Materials/
│   └── BulbEmission.mat
├── Models/
│   └── LuxoJr.fbx (또는 .obj)
├── Prefabs/
├── Scenes/
│   └── Main.unity
└── StreamingAssets/
    └── config.json
```

---

## 3. 스크립트 복사

제공된 `Unity/Scripts/` 폴더의 모든 .cs 파일을 해당 위치에 복사:

| 파일 | 위치 |
|------|------|
| SerialController.cs | Assets/Scripts/Core/ |
| HSVController.cs | Assets/Scripts/Core/ |
| LampController.cs | Assets/Scripts/Core/ |
| BackgroundController.cs | Assets/Scripts/Core/ |
| WeatherManager.cs | Assets/Scripts/API/ |
| ClaudeManager.cs | Assets/Scripts/API/ |
| FirebaseManager.cs | Assets/Scripts/API/ |
| MQTTManager.cs | Assets/Scripts/API/ |
| UIManager.cs | Assets/Scripts/UI/ |

---

## 4. 설정 파일 생성

### 4.1 config.json

**Assets/StreamingAssets/config.json** 생성:

```json
{
  "claudeApiKey": "<YOUR_API_KEY>",
  "openWeatherApiKey": "<YOUR_API_KEY>",
  "firebaseUrl": "https://emolamp-default-rtdb.firebaseio.com/",
  "mqttServerUrl": "https://port-0-motorcontrol-miqbz64b349f00ff.sel3.cloudtype.app",
  "serialPort": "COM3",
  "cityName": "Seoul"
}
```

⚠️ **중요**: 실제 API 키로 교체하세요!

---

## 5. 씬 구성

### 5.1 기본 오브젝트 생성

**Hierarchy 창에서 우클릭:**

```
Main Scene
├── --- Managers --- (빈 오브젝트)
│   ├── GameManager (빈 오브젝트)
│   ├── WeatherManager
│   ├── ClaudeManager
│   ├── FirebaseManager
│   ├── MQTTManager
│   ├── HSVController
│   ├── SerialController
│   ├── BackgroundController
│   └── UIManager
│
├── --- Environment --- (빈 오브젝트)
│   ├── Main Camera
│   ├── Directional Light
│   └── Background (Plane 또는 Quad)
│
├── --- Lamp --- (빈 오브젝트)
│   ├── LuxoJr_Model (3D 모델 임포트)
│   │   └── Bulb (전구 부분)
│   ├── BulbLight (Point Light)
│   └── LampController
│
└── --- UI --- (빈 오브젝트)
    └── Canvas
        ├── StatusBar
        ├── InputPanel
        ├── ModePanel
        └── ColorPickerPanel
```

### 5.2 Managers 설정

1. **빈 오브젝트 생성**: `Create Empty` → 이름: `Managers`

2. **각 Manager 오브젝트 생성**:
   - Managers 하위에 빈 오브젝트 생성
   - 해당 스크립트 컴포넌트 추가

```
[WeatherManager 오브젝트]
└── WeatherManager.cs 컴포넌트
    ├── Api Key: (OpenWeather 키)
    ├── City Name: Seoul
    └── Update Interval: 600

[ClaudeManager 오브젝트]
└── ClaudeManager.cs 컴포넌트
    └── Api Key: (Claude 키 - 이미 기본값 있음)

[FirebaseManager 오브젝트]
└── FirebaseManager.cs 컴포넌트
    ├── Database Url: https://YOUR_PROJECT.firebaseio.com
    └── Sync Interval: 5

[MQTTManager 오브젝트]
└── MQTTManager.cs 컴포넌트
    └── Server Url: https://YOUR_CLOUDTYPE_URL.cloudtype.app

[HSVController 오브젝트]
└── HSVController.cs 컴포넌트
    ├── Hue: 120
    ├── Saturation: 70
    └── Brightness: 70

[SerialController 오브젝트]
└── SerialController.cs 컴포넌트
    ├── Port Name: COM3 (장치관리자에서 확인)
    ├── Baud Rate: 9600
    └── Auto Connect: ✓

[BackgroundController 오브젝트]
└── BackgroundController.cs 컴포넌트
    ├── Main Camera: (드래그)
    ├── Directional Light: (드래그)
    ├── Bright Color: (밝은 파랑)
    └── Dark Color: (어두운 남색)
```

---

## 6. 3D 램프 모델 설정

### 6.1 모델 임포트

1. **Luxo Jr. FBX/OBJ 파일**을 `Assets/Models/`에 드래그
2. Import Settings:
   ```
   Scale Factor: 1 (또는 적절한 크기)
   Import Materials: ✓
   ```

### 6.2 씬에 배치

1. 모델을 Hierarchy에 드래그
2. Position: (0, 0, 0) 또는 적절한 위치
3. Scale: 모델 크기에 맞게 조정

### 6.3 전구 Material 설정

1. **새 Material 생성**: 
   - Assets/Materials 우클릭 → Create → Material
   - 이름: `BulbEmission`

2. **Material 설정**:
   ```
   Shader: Standard (또는 URP/Lit)
   
   Albedo: 흰색
   Emission: ✓ 체크
   Emission Color: 초록색 (기본값)
   Emission Intensity: 2
   ```

3. **전구 메시에 적용**:
   - 램프 모델에서 전구 부분 선택
   - Material 슬롯에 BulbEmission 드래그

### 6.4 Point Light 추가

1. **Lamp 하위에 Point Light 생성**:
   - 우클릭 → Light → Point Light
   
2. **설정**:
   ```
   Color: 초록색 (기본값)
   Intensity: 2
   Range: 5
   Position: 전구 위치에 맞춤
   ```

### 6.5 LampController 설정

1. **Lamp 오브젝트에 LampController.cs 추가**

2. **Inspector에서 연결**:
   ```
   Lamp Model: (램프 Transform)
   Bulb Renderer: (전구 Mesh Renderer)
   Bulb Light: (Point Light)
   Material Index: 0 (전구 Material 인덱스)
   HSV Controller: (HSVController 오브젝트)
   Serial Controller: (SerialController 오브젝트)
   ```

---

## 7. UI 구성

### 7.1 Canvas 생성

1. **Hierarchy → UI → Canvas**
2. **Canvas 설정**:
   ```
   Render Mode: Screen Space - Overlay
   UI Scale Mode: Scale With Screen Size
   Reference Resolution: 1920 x 1080
   ```

### 7.2 UI 요소 생성

#### Status Bar (상단)
```
Canvas
└── StatusBar (Panel)
    ├── Rect Transform
    │   ├── Anchor: Top-Stretch
    │   ├── Height: 100
    │   └── Padding: 20
    │
    ├── WeatherText (TextMeshPro)
    │   └── Text: "🌤️ 맑음 8°C"
    │
    ├── EmotionText (TextMeshPro)
    │   └── Text: "😊 기쁨"
    │
    └── SummaryText (TextMeshPro)
        └── Text: "💬 AI 분석 결과가 여기에 표시됩니다"
```

#### Input Panel (하단)
```
Canvas
└── InputPanel (Panel)
    ├── Rect Transform
    │   ├── Anchor: Bottom-Stretch
    │   ├── Height: 150
    │   └── Padding: 20
    │
    ├── InputField (TMP_InputField)
    │   ├── Placeholder: "오늘 기분이 어때요?"
    │   └── Width: 600
    │
    └── AnalyzeButton (Button)
        └── Text: "분석 ▶"
```

#### Mode Panel
```
Canvas
└── ModePanel (Panel)
    ├── AutoModeToggle (Toggle)
    │   └── Label: "AUTO"
    │
    └── ManualModeToggle (Toggle)
        └── Label: "MANUAL"
```

#### Color Picker Panel (MANUAL 모드용)
```
Canvas
└── ColorPickerPanel (Panel)
    ├── Active: false (기본 비활성)
    │
    ├── ColorPreview (Image)
    │   └── 현재 색상 미리보기
    │
    ├── HueSlider (Slider)
    │   ├── Min: 0, Max: 360
    │   └── Value: 120
    │
    ├── SaturationSlider (Slider)
    │   ├── Min: 0, Max: 100
    │   └── Value: 70
    │
    └── BrightnessSlider (Slider)
        ├── Min: 0, Max: 100
        └── Value: 70
```

### 7.3 UIManager 연결

**UIManager 오브젝트 Inspector**:

```
=== Managers ===
Weather Manager: (드래그)
Claude Manager: (드래그)
Firebase Manager: (드래그)
MQTT Manager: (드래그)
HSV Controller: (드래그)
Lamp Controller: (드래그)
Background Controller: (드래그)
Serial Controller: (드래그)

=== Status Bar UI ===
Weather Text: (드래그)
Emotion Text: (드래그)
Summary Text: (드래그)

=== Input UI ===
Input Field: (드래그)
Analyze Button: (드래그)
Analyze Button Text: (버튼 내 Text)

=== Mode UI ===
Auto Mode Toggle: (드래그)
Manual Mode Toggle: (드래그)
Color Picker Panel: (드래그)
Color Preview: (드래그)
Hue Slider: (드래그)
Saturation Slider: (드래그)
Brightness Slider: (드래그)

=== Connection Status ===
Serial Status Icon: (드래그)
Firebase Status Icon: (드래그)
Connection Text: (드래그)
```

---

## 8. 카메라 설정

### Main Camera
```
Position: (0, 1, -3) - 램프가 잘 보이도록
Rotation: (10, 0, 0) - 약간 아래를 봄
Clear Flags: Solid Color
Background: (어두운 파랑)
```

### Directional Light
```
Rotation: (50, -30, 0)
Intensity: 1
Color: 흰색
```

---

## 9. 빌드 전 체크리스트

### 스크립트 연결 확인
```
□ 모든 Manager 스크립트가 오브젝트에 붙어있음
□ UIManager에 모든 참조가 연결됨
□ LampController에 모델/라이트 연결됨
□ BackgroundController에 카메라/라이트 연결됨
```

### API 키 확인
```
□ Claude API 키 설정됨
□ OpenWeather API 키 설정됨
□ Firebase URL 설정됨
□ MQTT Server URL 설정됨
```

### 하드웨어 확인
```
□ Arduino COM 포트 번호 확인
□ SerialController에 올바른 포트 설정
```

---

## 10. 실행 및 테스트

### 10.1 Play 모드 실행

1. **Unity Editor에서 Play 버튼 클릭**

2. **Console 창 확인**:
   ```
   [SerialController] 연결 성공: COM3
   [WeatherManager] 날씨 정보 요청: Seoul
   [FirebaseManager] 리스닝 시작
   ```

### 10.2 기능 테스트

1. **텍스트 입력 테스트**:
   - 입력창에 "오늘 기분 좋아" 입력
   - "분석" 버튼 클릭
   - LED 색상 변화 확인

2. **다국어 테스트**:
   - "I'm feeling happy today" 입력
   - Claude가 직접 영어를 이해하여 분석

3. **MANUAL 모드 테스트**:
   - MANUAL 토글 클릭
   - 슬라이더로 색상 변경
   - Unity LED + 물리 LED 동시 변화 확인

4. **조도 센서 테스트**:
   - 센서를 손으로 가림
   - Unity 배경 어두워지는지 확인

---

## 11. 문제 해결

### Serial 연결 안 됨
```
1. Arduino IDE의 Serial Monitor 닫기
2. 장치관리자에서 COM 포트 확인
3. SerialController의 Port Name 수정
4. Unity 재시작
```

### API 호출 실패
```
1. Console에서 에러 메시지 확인
2. API 키 올바른지 확인
3. 인터넷 연결 확인
4. Project Settings → HTTP 허용 확인
```

### LED 색상 안 바뀜
```
1. LampController에 Material 연결 확인
2. Material의 Emission 활성화 확인
3. HSVController → LampController 이벤트 연결 확인
```

### 조도값 안 들어옴
```
1. Serial 연결 상태 확인
2. Arduino Serial Monitor에서 "LIGHT:xx" 출력 확인
3. 센서 회로 연결 확인
```

---

## 12. 다음 단계

1. ✅ Unity 프로젝트 설정 완료
2. → Arduino 회로 연결 (3_회로가이드.md)
3. → Cloudtype 서버 배포 (4_Cloudtype가이드.md)
4. → 전체 테스트 (5_테스트가이드.md)