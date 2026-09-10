# EmoLamp Cloudtype 배포 가이드

## 1. GitHub Repository 생성

### 1.1 새 Repository 만들기

1. **GitHub 접속**: https://github.com/new
2. **Repository 설정**:
   ```
   Repository name: EmoLamp-Server
   Description: EmoLamp MQTT-style IoT Server
   Public 또는 Private 선택
   Add README: ✓
   Add .gitignore: Node
   ```
3. **Create repository** 클릭

### 1.2 파일 업로드

**Cloudtype 폴더의 파일들을 Repository에 업로드:**

```
EmoLamp-Server/
├── server.js
├── package.json
├── .gitignore
├── .env.example
├── README.md
└── public/
    └── index.html      # 웹 대시보드
```

**방법 1: GitHub 웹에서 직접 업로드**
- Repository → Add file → Upload files
- 파일들 드래그 앤 드롭
- Commit changes

**방법 2: Git 명령어**
```bash
git clone https://github.com/YOUR_USERNAME/EmoLamp-Server.git
cd EmoLamp-Server
# 파일 복사
git add .
git commit -m "Initial commit"
git push origin main
```

---

## 2. Cloudtype 프로젝트 생성

### 2.1 Cloudtype 로그인

1. https://cloudtype.io/ 접속
2. GitHub 계정으로 로그인

### 2.2 새 프로젝트 생성

1. **+ 새 프로젝트** 클릭
2. **프로젝트 이름**: `emolamp-server`
3. **생성** 클릭

### 2.3 서비스 추가

1. **+ 서비스 추가** 클릭
2. **GitHub 연결** 선택
3. **Repository 선택**: `EmoLamp-Server`
4. **Branch**: `main`

### 2.4 배포 설정

```
=== 기본 설정 ===
서비스 이름: emolamp-server
런타임: Node.js 18

=== 빌드 설정 ===
Install Command: npm install
Build Command: (비워둠)
Start Command: npm start

=== 포트 설정 ===
Port: 3000

=== Health Check ===
경로: /healthz
```

### 2.5 환경변수 설정 (선택)

현재 서버는 환경변수 없이도 동작하지만, 필요시:

```
PORT=3000
NODE_ENV=production
```

### 2.6 배포

1. **배포하기** 버튼 클릭
2. 빌드 로그 확인
3. 배포 완료까지 대기 (1-2분)

---

## 3. 배포 확인

### 3.1 URL 확인

배포 완료 후 제공되는 URL 확인:
```
https://port-0-emolamp-server-xxxxx.sel3.cloudtype.app
```

### 3.2 API 테스트

**브라우저에서 접속:**
```
https://YOUR_URL.cloudtype.app/
```

**예상 응답:**
```json
{
  "name": "EmoLamp Server",
  "description": "MQTT-style IoT Server for EmoLamp",
  "endpoints": {
    "status": "GET /api/status",
    "publish": "POST /api/publish",
    "poll": "GET /api/poll?clientId=xxx&since=timestamp",
    "state": "GET|POST /api/state",
    "led": "POST /api/led"
  },
  "currentState": { ... }
}
```

### 3.3 상태 확인

```
https://YOUR_URL.cloudtype.app/api/status
```

**예상 응답:**
```json
{
  "status": "ok",
  "server": "EmoLamp Server",
  "version": "1.0.0",
  "uptime": 123.456,
  "timestamp": 1702012800000
}
```

---

## 4. Unity에서 서버 URL 설정

### 4.1 MQTTManager 설정

**Unity Inspector에서:**
```
Server Url: https://port-0-emolamp-server-xxxxx.sel3.cloudtype.app
```

### 4.2 또는 config.json 수정

```json
{
  "mqttServerUrl": "https://port-0-emolamp-server-xxxxx.sel3.cloudtype.app"
}
```

---

## 5. API 엔드포인트 상세

### GET /api/status
서버 상태 확인

**Response:**
```json
{
  "status": "ok",
  "server": "EmoLamp Server",
  "version": "1.0.0",
  "uptime": 123.456,
  "timestamp": 1702012800000
}
```

### POST /api/publish
메시지 발행

**Request:**
```json
{
  "topic": "emolamp/state",
  "payload": {
    "mode": "AUTO",
    "hue": 120,
    "saturation": 70,
    "brightness": 80
  },
  "clientId": "unity_abc123"
}
```

**Response:**
```json
{
  "success": true,
  "message": { ... }
}
```

### GET /api/poll
메시지 폴링

**Request:**
```
/api/poll?clientId=unity_abc123&since=1702012800000
```

**Response:**
```json
{
  "topic": "emolamp/state",
  "payload": { ... },
  "clientId": "web_xyz789",
  "timestamp": 1702012850000
}
```

### GET /api/state
현재 램프 상태 조회

**Response:**
```json
{
  "mode": "AUTO",
  "emotion": "joy",
  "hue": 50,
  "saturation": 100,
  "brightness": 80,
  "colorHex": "#FFD700",
  "summary": "좋은 하루",
  "weather": "맑음",
  "timestamp": 1702012800000
}
```

### POST /api/state
램프 상태 업데이트

**Request:**
```json
{
  "mode": "MANUAL",
  "manualColorHex": "#FF0000"
}
```

### POST /api/led
LED 색상 직접 제어

**Request:**
```json
{
  "r": 255,
  "g": 100,
  "b": 50
}
```

---

## 6. 웹 테스트 도구

### 6.1 curl 테스트

```bash
# 상태 확인
curl https://YOUR_URL.cloudtype.app/api/status

# 상태 조회
curl https://YOUR_URL.cloudtype.app/api/state

# 상태 업데이트
curl -X POST https://YOUR_URL.cloudtype.app/api/state \
  -H "Content-Type: application/json" \
  -d '{"mode":"MANUAL","manualColorHex":"#FF0000"}'

# LED 제어
curl -X POST https://YOUR_URL.cloudtype.app/api/led \
  -H "Content-Type: application/json" \
  -d '{"r":255,"g":0,"b":0}'
```

### 6.2 Postman / Insomnia

1. 새 Request 생성
2. URL 입력
3. Method 선택 (GET/POST)
4. Body에 JSON 입력 (POST의 경우)
5. Send

---

## 7. 로그 확인

### Cloudtype 대시보드에서:

1. 프로젝트 선택
2. 서비스 선택
3. **로그** 탭 클릭
4. 실시간 로그 확인

### 예상 로그:
```
[2024-12-09T10:30:00.000Z] GET /api/status
[2024-12-09T10:30:05.000Z] POST /api/state
[STATE] Updated: mode=AUTO, emotion=joy
[PUBLISH] emolamp/state: {"mode":"AUTO"...}
```

---

## 8. 문제 해결

### 배포 실패

**빌드 오류:**
```
1. package.json 문법 확인
2. Node.js 버전 확인 (18 이상)
3. 로그에서 에러 메시지 확인
```

**Health Check 실패:**
```
1. /healthz 엔드포인트 존재 확인
2. 서버가 3000 포트에서 실행 중인지 확인
3. 로그에서 시작 메시지 확인
```

### 연결 안 됨

**CORS 오류:**
```
- server.js에 cors 미들웨어 확인
- app.use(cors()) 있는지 확인
```

**타임아웃:**
```
- 서버 상태 확인
- Cloudtype 대시보드에서 재시작
```

---

## 9. 재배포

### 코드 수정 후:

**GitHub에 Push하면 자동 배포:**
```bash
git add .
git commit -m "Fix: 수정 내용"
git push origin main
```

**또는 수동 배포:**
1. Cloudtype 대시보드
2. 서비스 선택
3. **재배포** 버튼 클릭

---

## 10. 다음 단계

1. ✅ Cloudtype 서버 배포 완료
2. ✅ Unity에 서버 URL 설정
3. → 전체 시스템 테스트 (5_테스트가이드.md)