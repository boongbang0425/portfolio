# EmoLamp Server

MQTT 스타일 IoT 서버 - Unity 클라이언트와 실시간 통신 지원

## 기능

- 실시간 메시지 Pub/Sub (Long Polling)
- 램프 상태 저장 및 조회
- LED 색상 원격 제어
- 다중 클라이언트 지원

## API 엔드포인트

| Method | Endpoint | 설명 |
|--------|----------|------|
| GET | `/api/status` | 서버 상태 확인 |
| GET | `/api/state` | 현재 램프 상태 조회 |
| POST | `/api/state` | 램프 상태 업데이트 |
| POST | `/api/publish` | 메시지 발행 |
| GET | `/api/poll` | 메시지 폴링 |
| POST | `/api/led` | LED 색상 직접 제어 |
| GET | `/healthz` | Health Check |

## 로컬 실행

```bash
npm install
npm start
```

## Cloudtype 배포

1. GitHub Repository 연결
2. Node.js 18 런타임 선택
3. Start Command: `npm start`
4. Port: 3000
5. Health Check: `/healthz`

## 사용 예시

```bash
# 상태 확인
curl https://your-url.cloudtype.app/api/status

# LED 색상 변경
curl -X POST https://your-url.cloudtype.app/api/led \
  -H "Content-Type: application/json" \
  -d '{"r":255,"g":100,"b":50}'
```

## 라이선스

MIT
