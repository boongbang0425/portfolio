/*
 * EmoLamp - Arduino 코드 (Common Anode 수정판)
 * * 하드웨어 설정 (확인됨):
 * 1. RGB LED (Common Anode):
 * - R핀: D3
 * - G핀: D5
 * - B핀: D4
 * - 공통핀(-): 5V (VCC)에 연결
 * * 2. 조도 센서:
 * - S핀: A0
 * - V핀: 5V
 * - G핀: GND
 */

// ==================== 핀 설정 (수정됨) ====================
const int PIN_LED_R = 3;      // 테스트 코드 기준 R
const int PIN_LED_G = 5;      // 테스트 코드 기준 G
const int PIN_LED_B = 4;      // 테스트 코드 기준 B
const int PIN_LIGHT = A0;     // 조도 센서

// ==================== 설정값 ====================
const int BAUD_RATE = 9600;
const int LIGHT_SEND_INTERVAL = 500;  // 조도 전송 주기 (ms)
const int LIGHT_THRESHOLD = 3;        // 변화 감지 임계값 (%)

// ==================== 변수 ====================
int currentR = 0;
int currentG = 0;
int currentB = 0;

int lastLightPercent = -1;
unsigned long lastLightSendTime = 0;

String inputBuffer = "";

// ==================== 초기화 ====================
void setup() {
    Serial.begin(BAUD_RATE);
    
    pinMode(PIN_LED_R, OUTPUT);
    pinMode(PIN_LED_G, OUTPUT);
    pinMode(PIN_LED_B, OUTPUT);
    pinMode(PIN_LIGHT, INPUT);
    
    // 초기화: 초록색 켜기
    // setLED 함수 내부에서 Common Anode 처리를 하므로
    // 여기서는 직관적으로 (R=0, G=255, B=0)을 넣으면 됩니다.
    setLED(0, 255, 0);
    
    Serial.println("STATUS:OK");
    Serial.println("EmoLamp Arduino Ready (Common Anode Mode)");
    
    delay(100);
}

// ==================== 메인 루프 ====================
void loop() {
    // 1. Serial 명령 처리
    processSerial();
    
    // 2. 조도 센서 비활성화 (센서 고장)
    // processLightSensor();
}

// ==================== Serial 처리 ====================
void processSerial() {
    while (Serial.available() > 0) {
        char c = Serial.read();
        if (c == '\n' || c == '\r') {
            if (inputBuffer.length() > 0) {
                parseCommand(inputBuffer);
                inputBuffer = "";
            }
        } else {
            inputBuffer += c;
            if (inputBuffer.length() > 50) {
                inputBuffer = "";
            }
        }
    }
}

void parseCommand(String cmd) {
    cmd.trim();
    
    // 디버그 출력
    Serial.print("DEBUG:Received=");
    Serial.println(cmd);
    
    // RGB 명령: "RGB:255,200,100"
    if (cmd.startsWith("RGB:")) {
        String values = cmd.substring(4);
        int comma1 = values.indexOf(',');
        int comma2 = values.indexOf(',', comma1 + 1);
        
        if (comma1 > 0 && comma2 > comma1) {
            int r = values.substring(0, comma1).toInt();
            int g = values.substring(comma1 + 1, comma2).toInt();
            int b = values.substring(comma2 + 1).toInt();
            
            r = constrain(r, 0, 255);
            g = constrain(g, 0, 255);
            b = constrain(b, 0, 255);
            
            setLED(r, g, b);
        }
    }
    // HSV 명령 등은 동일...
    else if (cmd == "STATUS") {
        Serial.println("STATUS:OK");
    }
    else if (cmd == "LIGHT") {
        sendLightValue(true);
    }
}

// ==================== LED 제어 (핵심 수정 부분) ====================
void setLED(int r, int g, int b) {
    currentR = r;
    currentG = g;
    currentB = b;
    
    // ★ Common Anode 처리 ★
    // Unity에서 255(최대밝기)를 보내면 -> 아두이노는 0(GND)을 출력해야 켜짐
    // Unity에서 0(끄기)을 보내면 -> 아두이노는 255(5V)를 출력해야 꺼짐
    analogWrite(PIN_LED_R, 255 - r);
    analogWrite(PIN_LED_G, 255 - g);
    analogWrite(PIN_LED_B, 255 - b);
}

void setLEDFromHSV(float h, float s, float v) {
    // (HSV 변환 로직은 그대로 둡니다.
    // 마지막에 setLED를 호출할 때 자동으로 반전 처리됩니다.)
    
    h = fmod(h, 360.0f);
    if (h < 0) h += 360.0f;
    s = constrain(s, 0.0f, 100.0f) / 100.0f;
    v = constrain(v, 0.0f, 100.0f) / 100.0f;
    
    float c = v * s;
    float x = c * (1 - abs(fmod(h / 60.0f, 2) - 1));
    float m = v - c;
    
    float r1, g1, b1;
    if (h < 60) { r1 = c; g1 = x; b1 = 0; }
    else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
    else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
    else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
    else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
    else { r1 = c; g1 = 0; b1 = x; }
    
    int r = (int)((r1 + m) * 255);
    int g = (int)((g1 + m) * 255);
    int b = (int)((b1 + m) * 255);
    
    setLED(r, g, b);
}

// ==================== 조도 센서 ====================
void processLightSensor() {
    unsigned long now = millis();
    if (now - lastLightSendTime < LIGHT_SEND_INTERVAL) {
        return;
    }
    lastLightSendTime = now;
    sendLightValue(false);
}

void sendLightValue(bool force) {
    int rawValue = analogRead(PIN_LIGHT);
    int lightPercent = map(rawValue, 0, 1023, 0, 100);
    lightPercent = constrain(lightPercent, 0, 100);
    
    if (force || abs(lightPercent - lastLightPercent) >= LIGHT_THRESHOLD) {
        lastLightPercent = lightPercent;
        Serial.print("LIGHT:");
        Serial.println(lightPercent);
    }
}