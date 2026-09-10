require('dotenv').config();
const express = require('express');
const mariadb = require('mariadb');
const cors = require('cors');
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');
const path = require('path');
const fs = require('fs');

// ===== nodemailer 선택적 로드 (없어도 서버 동작) =====
let nodemailer = null;
try {
    nodemailer = require('nodemailer');
    console.log('📧 nodemailer 모듈 로드됨');
} catch (e) {
    console.log('📧 nodemailer 미설치 - 이메일 기능 비활성화');
}

const app = express();
const PORT = process.env.PORT || 3000;
const JWT_SECRET = process.env.JWT_SECRET || 'coss-secret-key-2025';

const DATA_FILE = path.join(__dirname, 'coss-data.json');

let pool = null;
if (process.env.DB_HOST) {
    pool = mariadb.createPool({
        host: process.env.DB_HOST,
        user: process.env.DB_USER,
        password: process.env.DB_PASSWORD,
        database: process.env.DB_NAME,
        port: process.env.DB_PORT || 3306,
        connectionLimit: 5
    });
}

// ===== Nodemailer 설정 (선택적) =====
let mailTransporter = null;
if (nodemailer && process.env.GMAIL_USER && process.env.GMAIL_APP_PASSWORD) {
    mailTransporter = nodemailer.createTransport({
        service: 'gmail',
        auth: {
            user: process.env.GMAIL_USER,
            pass: process.env.GMAIL_APP_PASSWORD
        }
    });
    console.log('📧 이메일 서비스 설정 완료');
}

app.use(cors({ origin: '*', methods: ['GET', 'POST', 'PUT', 'DELETE'], allowedHeaders: ['Content-Type', 'Authorization'] }));
app.use(express.json());
app.use(express.static('public'));

// ===== 확장된 데이터 구조 =====
let sensorData = {
    sensors: {
        1: { id: 1, name: '아침 약', emoji: '🌅', value: 0, lastOpened: null, todayOpened: false, targetTime: '08:00', description: '혈압약', missedAlertSent: false, alarmDismissed: false },
        2: { id: 2, name: '점심 약', emoji: '☀️', value: 0, lastOpened: null, todayOpened: false, targetTime: '13:00', description: '비타민', missedAlertSent: false, alarmDismissed: false },
        3: { id: 3, name: '저녁 약', emoji: '🌙', value: 0, lastOpened: null, todayOpened: false, targetTime: '18:00', description: '관절약', missedAlertSent: false, alarmDismissed: false },
        4: { id: 4, name: '자기전 약', emoji: '🛌', value: 0, lastOpened: null, todayOpened: false, targetTime: '22:00', description: '수면제', missedAlertSent: false, alarmDismissed: false }
    },
    history: [],
    dailyStats: {},
    users: [{ id: 1, email: 'user@coss.com', password: '', name: '홍길동', guardianEmail: '', profileIcon: 'user', profileColor: '#6B8E6B' }],
    userMedications: {},
    deviceInfo: { ipAddress: null, firmwareVersion: '1.0.0', lastHeartbeat: null, isOnline: false },
    isRefillMode: false,
    refillStartTime: null,
    notificationSettings: { enabled: true, nightModeEnabled: false, nightStart: '22:00', nightEnd: '06:00' }
};

let pendingRemoval = { 1: null, 2: null, 3: null, 4: null };
const FLICKERING_THRESHOLD_MS = 1000;

function saveData() {
    try { fs.writeFileSync(DATA_FILE, JSON.stringify(sensorData, null, 2)); } catch (e) { console.error('데이터 저장 실패:', e); }
}

function loadData() {
    try {
        if (fs.existsSync(DATA_FILE)) {
            const loaded = JSON.parse(fs.readFileSync(DATA_FILE));
            sensorData = { ...sensorData, ...loaded };
            if (!sensorData.userMedications) sensorData.userMedications = {};
            if (!sensorData.deviceInfo) sensorData.deviceInfo = { ipAddress: null, firmwareVersion: '1.0.0', lastHeartbeat: null, isOnline: false };
            if (sensorData.isRefillMode === undefined) sensorData.isRefillMode = false;
            if (!sensorData.notificationSettings) sensorData.notificationSettings = { enabled: true, nightModeEnabled: false, nightStart: '22:00', nightEnd: '06:00' };
            for (let id in sensorData.sensors) {
                if (sensorData.sensors[id].missedAlertSent === undefined) sensorData.sensors[id].missedAlertSent = false;
                if (sensorData.sensors[id].alarmDismissed === undefined) sensorData.sensors[id].alarmDismissed = false;
            }
            sensorData.users.forEach(u => { if (!u.guardianEmail) u.guardianEmail = ''; if (!u.profileIcon) u.profileIcon = 'user'; if (!u.profileColor) u.profileColor = '#6B8E6B'; });
            console.log('📂 저장된 데이터 로드됨');
        } else {
            console.log('✨ 새 데이터 시작');
            saveData();
        }
    } catch (e) { console.error('데이터 로드 실패:', e); }
}

async function initTestAccount() {
    const testEmail = 'user@coss.com', testPassword = 'coss1234';
    let user = sensorData.users.find(u => u.email === testEmail);
    if (!user) {
        user = { id: 1, email: testEmail, password: await bcrypt.hash(testPassword, 10), name: '홍길동', guardianEmail: '', profileIcon: 'user', profileColor: '#6B8E6B' };
        sensorData.users.push(user);
        saveData();
        console.log('👤 테스트 계정 생성: user@coss.com / coss1234');
    } else if (!user.password || !(await bcrypt.compare(testPassword, user.password))) {
        user.password = await bcrypt.hash(testPassword, 10);
        saveData();
        console.log('🔑 테스트 계정 비밀번호 설정: coss1234');
    } else {
        console.log('✅ 테스트 계정: user@coss.com / coss1234');
    }
    
    // 테스트 계정용 샘플 데이터 초기화
    initTestAccountData();
}

function initTestAccountData() {
    const testUserId = 1;
    const now = new Date();
    const today = now.toISOString().split('T')[0];
    
    // 테스트용 약물 데이터 설정
    if (!sensorData.userMedications[testUserId]) {
        sensorData.userMedications[testUserId] = {
            1: { time: '08:00', meds: [{ name: '혈압약', dose: '1정' }, { name: '비타민D', dose: '1000IU' }], taken: false },
            2: { time: '13:00', meds: [{ name: '오메가3', dose: '1캡슐' }], taken: false },
            3: { time: '18:00', meds: [{ name: '관절약', dose: '2정' }, { name: '유산균', dose: '1포' }], taken: false },
            4: { time: '22:00', meds: [{ name: '마그네슘', dose: '1정' }], taken: false }
        };
    }
    
    // 테스트용 7일치 히스토리 데이터 생성 (시연용 - 예쁜 그래프)
    if (sensorData.history.length === 0) {
        const sensorNames = ['아침 약', '점심 약', '저녁 약', '자기전 약'];
        const targetTimes = ['08:00', '13:00', '18:00', '22:00'];
        
        // 시연용 데이터: 점점 좋아지는 복약 패턴 (오늘 포함)
        // [6일전, 5일전, 4일전, 3일전, 2일전, 1일전, 오늘]
        const dailyPattern = [
            [1, 2],           // 6일전: 아침, 점심 (2회)
            [1, 2, 3],        // 5일전: 아침, 점심, 저녁 (3회)
            [1, 3],           // 4일전: 아침, 저녁 (2회)
            [1, 2, 3, 4],     // 3일전: 완벽! (4회)
            [1, 2, 3],        // 2일전: 아침, 점심, 저녁 (3회)
            [1, 2, 3, 4],     // 1일전: 완벽! (4회)
            [1, 2]            // 오늘: 아침, 점심 복용 완료 (시연 시작점)
        ];
        
        for (let dayOffset = 6; dayOffset >= 0; dayOffset--) {
            const date = new Date();
            date.setDate(date.getDate() - dayOffset);
            const dateKey = date.toISOString().split('T')[0];
            
            if (!sensorData.dailyStats[dateKey]) {
                sensorData.dailyStats[dateKey] = { date: dateKey, sensors: {} };
            }
            
            const slotsForDay = dailyPattern[6 - dayOffset];
            
            for (const slotId of slotsForDay) {
                const [targetH, targetM] = targetTimes[slotId - 1].split(':').map(Number);
                const recordTime = new Date(date);
                
                // 오늘인 경우 현재 시간 이전으로 설정
                if (dayOffset === 0) {
                    const currentHour = now.getHours();
                    if (targetH > currentHour) continue; // 미래 시간은 건너뜀
                    recordTime.setHours(targetH, targetM + Math.floor(Math.random() * 10), 0, 0);
                } else {
                    // 목표 시간 ±10분 내 랜덤
                    recordTime.setHours(targetH, targetM + Math.floor(Math.random() * 20) - 10, 0, 0);
                }
                
                sensorData.history.push({
                    userId: testUserId,  // 사용자별 데이터 분리
                    sensorId: slotId,
                    sensorName: sensorNames[slotId - 1],
                    action: 'removed',
                    timestamp: recordTime.toISOString(),
                    returnedAt: new Date(recordTime.getTime() + 5000).toISOString(),
                    duration: 5
                });
                
                if (!sensorData.dailyStats[dateKey].sensors[slotId]) {
                    sensorData.dailyStats[dateKey].sensors[slotId] = { count: 0, times: [] };
                }
                sensorData.dailyStats[dateKey].sensors[slotId].count++;
                sensorData.dailyStats[dateKey].sensors[slotId].times.push(recordTime.toISOString());
                
                // 오늘 복용한 약은 todayOpened 플래그 설정
                if (dayOffset === 0 && sensorData.sensors[slotId]) {
                    sensorData.sensors[slotId].todayOpened = true;
                    sensorData.sensors[slotId].lastOpened = recordTime.toISOString();
                }
            }
        }
        
        // 히스토리를 최신순으로 정렬
        sensorData.history.sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp));
        
        saveData();
        console.log('📊 테스트 계정 시연용 데이터 생성 완료');
    }
}

loadData();
initTestAccount().catch(e => console.error('계정 초기화 실패:', e));

async function sendGuardianEmail(userId, subject, htmlContent) {
    if (!mailTransporter) { console.log('📧 이메일 서비스 미설정'); return false; }
    const user = sensorData.users.find(u => u.id === userId);
    if (!user || !user.guardianEmail) { console.log('📧 보호자 이메일 없음'); return false; }
    try {
        await mailTransporter.sendMail({ from: `"COSS 스마트약통" <${process.env.GMAIL_USER}>`, to: user.guardianEmail, subject, html: htmlContent });
        console.log(`📧 이메일 발송: ${user.guardianEmail}`);
        return true;
    } catch (e) { console.error('📧 이메일 실패:', e); return false; }
}

async function checkMissedMedication() {
    const now = new Date(), currentHour = now.getHours();
    if (sensorData.notificationSettings.nightModeEnabled) {
        const [nightStartH] = sensorData.notificationSettings.nightStart.split(':').map(Number);
        const [nightEndH] = sensorData.notificationSettings.nightEnd.split(':').map(Number);
        if (currentHour >= nightStartH || currentHour < nightEndH) return;
    }
    if (!sensorData.notificationSettings.enabled) return;
    for (let id in sensorData.sensors) {
        const sensor = sensorData.sensors[id];
        if (sensor.todayOpened || sensor.missedAlertSent) continue;
        const [tHour, tMin] = sensor.targetTime.split(':').map(Number);
        const targetDate = new Date(now); targetDate.setHours(tHour, tMin, 0, 0);
        const diffMinutes = Math.round((now - targetDate) / 1000 / 60);
        if (diffMinutes > 0.17 && mailTransporter) {  // 0.17분 = 약 10초
            const user = sensorData.users[0];
            if (user && user.guardianEmail) {
                const subject = `[긴급] ${user.name}님이 ${sensor.name}을 복용하지 않았습니다.`;
                const htmlContent = `<div style="font-family:sans-serif;max-width:500px;margin:0 auto;padding:20px;"><div style="background:#6B8E6B;color:white;padding:20px;border-radius:15px 15px 0 0;text-align:center;"><h1 style="margin:0;">💊 COSS 알림</h1></div><div style="background:#f5f5f5;padding:25px;border-radius:0 0 15px 15px;"><p>${user.name} 보호자님,</p><p>${sensor.targetTime}에서 ${diffMinutes}분 경과 - <strong>${sensor.emoji} ${sensor.name}</strong> 미복용</p></div></div>`;
                if (await sendGuardianEmail(user.id, subject, htmlContent)) {
                    sensor.missedAlertSent = true;
                    saveData();
                }
            }
        }
    }
}

function calculatePDC(dailyStats) {
    const dates = Object.keys(dailyStats).sort();
    if (dates.length === 0) return 0;
    const startDate = new Date(dates[0]), endDate = new Date(dates[dates.length - 1]);
    const totalDays = Math.max(1, Math.ceil((endDate - startDate) / (1000 * 60 * 60 * 24)) + 1);
    let successDays = 0;
    for (let dk in dailyStats) { if (Object.values(dailyStats[dk].sensors || {}).filter(s => s.count > 0).length > 0) successDays++; }
    return Math.round((successDays / totalDays) * 100);
}

function calculateMaxStreak(dailyStats) {
    const dates = Object.keys(dailyStats).sort();
    if (dates.length === 0) return 0;
    let maxStreak = 0, currentStreak = 0, prevDate = null;
    for (let dk of dates) {
        if (Object.values(dailyStats[dk].sensors || {}).filter(s => s.count > 0).length > 0) {
            if (prevDate) {
                const diff = Math.round((new Date(dk) - new Date(prevDate)) / (1000 * 60 * 60 * 24));
                currentStreak = (diff === 1) ? currentStreak + 1 : 1;
            } else { currentStreak = 1; }
            prevDate = dk;
        } else { currentStreak = 0; prevDate = null; }
        maxStreak = Math.max(maxStreak, currentStreak);
    }
    return maxStreak;
}

function calculateAdherenceMetrics(userId = null) {
    // 사용자별 히스토리 필터링
    const userHistory = userId 
        ? sensorData.history.filter(h => h.userId === userId)
        : sensorData.history;
    
    // 사용자별 일별 통계 계산
    const userDailyStats = {};
    userHistory.forEach(h => {
        if (h.action === 'removed' && h.timestamp) {
            const dateKey = h.timestamp.split('T')[0];
            if (!userDailyStats[dateKey]) userDailyStats[dateKey] = { date: dateKey, sensors: {} };
            if (!userDailyStats[dateKey].sensors[h.sensorId]) userDailyStats[dateKey].sensors[h.sensorId] = { count: 0, times: [] };
            userDailyStats[dateKey].sensors[h.sensorId].count++;
            userDailyStats[dateKey].sensors[h.sensorId].times.push(h.timestamp);
        }
    });
    
    const totalDays = Object.keys(userDailyStats).length;
    const totalCount = userHistory.filter(h => h.action === 'removed').length;
    
    // 최장 미복용 기간 계산
    const dates = Object.keys(userDailyStats).sort();
    let maxGap = 0;
    let prevDate = null;
    for (let dk of dates) {
        if (prevDate) {
            const diff = Math.round((new Date(dk) - new Date(prevDate)) / (1000 * 60 * 60 * 24));
            if (diff > 1) {
                maxGap = Math.max(maxGap, diff - 1);
            }
        }
        prevDate = dk;
    }
    
    // 시간 정확도 계산 (목표 시간 대비 실제 복용 시간 오차)
    let totalAccuracy = 0;
    let accuracyCount = 0;
    for (let h of userHistory) {
        if (h.action === 'removed' && h.timestamp && h.sensorId) {
            const sensor = sensorData.sensors[h.sensorId];
            if (sensor && sensor.targetTime) {
                const [targetH, targetM] = sensor.targetTime.split(':').map(Number);
                const actualTime = new Date(h.timestamp);
                const targetMinutes = targetH * 60 + targetM;
                const actualMinutes = actualTime.getHours() * 60 + actualTime.getMinutes();
                const diffMinutes = Math.abs(actualMinutes - targetMinutes);
                // 30분 이내면 100%, 60분이면 50%, 120분 이상이면 0%
                const accuracy = Math.max(0, 100 - (diffMinutes / 1.2));
                totalAccuracy += accuracy;
                accuracyCount++;
            }
        }
    }
    const timeAccuracy = accuracyCount > 0 ? Math.round(totalAccuracy / accuracyCount) : 0;
    
    return { 
        totalDays, 
        totalCount, 
        averagePerDay: totalDays > 0 ? totalCount / totalDays : 0, 
        pdc: calculatePDC(userDailyStats), 
        maxStreak: calculateMaxStreak(userDailyStats),
        maxGap,
        timeAccuracy
    };
}

function authenticateToken(req, res, next) {
    const authHeader = req.headers['authorization'];
    const token = authHeader && authHeader.split(' ')[1];
    if (!token) return res.status(401).json({ error: 'No token' });
    jwt.verify(token, JWT_SECRET, (err, user) => {
        if (err) return res.status(403).json({ error: 'Invalid token' });
        req.user = user;
        next();
    });
}

// ===== API =====
app.get('/value', (req, res) => res.json(sensorData.sensors));

app.post('/value', (req, res) => {
    const { sensorId, value, ipAddress, firmwareVersion } = req.body;
    if (ipAddress) sensorData.deviceInfo.ipAddress = ipAddress;
    if (firmwareVersion) sensorData.deviceInfo.firmwareVersion = firmwareVersion;
    sensorData.deviceInfo.lastHeartbeat = new Date().toISOString();
    sensorData.deviceInfo.isOnline = true;
    // 리필 모드일 때는 복용 기록을 생성하지 않음
    if (sensorData.isRefillMode) {
        console.log('📦 리필 모드 - 복용 기록 건너뜀');
        return res.json({ success: true, ignored: true });
    }
    const finalSensorId = parseInt(sensorId, 10), finalValue = parseInt(value, 10);
    if (finalSensorId < 1 || finalSensorId > 4) return res.status(400).json({ error: 'Invalid sensor ID' });
    const sensor = sensorData.sensors[finalSensorId];
    if (finalValue === 1 && sensor.value === 0) {
        pendingRemoval[finalSensorId] = { timestamp: Date.now(), startTime: new Date().toISOString() };
        console.log(`[Sensor ${finalSensorId}] 🔴 REMOVED`);
    }
    if (finalValue === 0 && sensor.value === 1 && pendingRemoval[finalSensorId]) {
        const elapsedMs = Date.now() - pendingRemoval[finalSensorId].timestamp;
        if (elapsedMs >= FLICKERING_THRESHOLD_MS) {
            const now = new Date();
            sensor.lastOpened = now.toISOString();
            sensor.todayOpened = true;
            sensor.missedAlertSent = false;
            const today = now.toISOString().split('T')[0];
            if (!sensorData.dailyStats[today]) sensorData.dailyStats[today] = { date: today, sensors: {} };
            if (!sensorData.dailyStats[today].sensors[finalSensorId]) sensorData.dailyStats[today].sensors[finalSensorId] = { count: 0, times: [] };
            sensorData.dailyStats[today].sensors[finalSensorId].count++;
            sensorData.dailyStats[today].sensors[finalSensorId].times.push(now.toISOString());
            sensorData.history.unshift({ userId: 1, sensorId: finalSensorId, sensorName: sensor.name, action: 'removed', timestamp: pendingRemoval[finalSensorId].startTime, returnedAt: now.toISOString(), duration: Math.round(elapsedMs / 1000) });
            if (sensorData.history.length > 500) sensorData.history.pop();
            console.log(`[Sensor ${finalSensorId}] ✅ RECORDED`);
            saveData();
        }
        pendingRemoval[finalSensorId] = null;
    }
    sensor.value = finalValue;
    res.json({ success: true, sensor });
});

app.post('/api/auth/login', async (req, res) => {
    const { email, password } = req.body;
    const user = sensorData.users.find(u => u.email === email);
    if (!user || !(await bcrypt.compare(password, user.password))) return res.status(401).json({ error: 'Invalid credentials' });
    const token = jwt.sign({ id: user.id, email: user.email }, JWT_SECRET);
    res.json({ success: true, token, user: { id: user.id, name: user.name, email: user.email, guardianEmail: user.guardianEmail, profileIcon: user.profileIcon, profileColor: user.profileColor } });
});

app.post('/api/auth/register', async (req, res) => {
    const { email, password, name } = req.body;
    if (sensorData.users.find(u => u.email === email)) return res.status(400).json({ error: 'Exists' });
    const newUser = { id: sensorData.users.length + 1, email, password: await bcrypt.hash(password, 10), name, guardianEmail: '', profileIcon: 'user', profileColor: '#6B8E6B' };
    sensorData.users.push(newUser);
    saveData();
    const token = jwt.sign({ id: newUser.id, email }, JWT_SECRET);
    res.json({ success: true, token, user: { id: newUser.id, name, email, guardianEmail: '', profileIcon: 'user', profileColor: '#6B8E6B' } });
});

app.get('/api/profile', authenticateToken, (req, res) => {
    const user = sensorData.users.find(u => u.id === req.user.id);
    if (!user) return res.status(404).json({ error: 'Not found' });
    res.json({ success: true, user: { id: user.id, name: user.name, email: user.email, guardianEmail: user.guardianEmail || '', profileIcon: user.profileIcon || 'user', profileColor: user.profileColor || '#6B8E6B' } });
});

app.put('/api/profile', authenticateToken, (req, res) => {
    const user = sensorData.users.find(u => u.id === req.user.id);
    if (!user) return res.status(404).json({ error: 'Not found' });
    const { name, guardianEmail, profileIcon, profileColor } = req.body;
    if (name) user.name = name;
    if (guardianEmail !== undefined) user.guardianEmail = guardianEmail;
    if (profileIcon) user.profileIcon = profileIcon;
    if (profileColor) user.profileColor = profileColor;
    saveData();
    res.json({ success: true, message: '업데이트됨', user: { id: user.id, name: user.name, email: user.email, guardianEmail: user.guardianEmail, profileIcon: user.profileIcon, profileColor: user.profileColor } });
});

app.get('/api/medications/user', authenticateToken, (req, res) => res.json({ success: true, data: sensorData.userMedications[req.user.id] || null }));

app.post('/api/medications/user', authenticateToken, (req, res) => {
    const { cardData } = req.body;
    if (!cardData) return res.status(400).json({ error: 'cardData required' });
    sensorData.userMedications[req.user.id] = cardData;
for (let id in cardData) { 
        if (sensorData.sensors[id] && cardData[id].time) {
            // 시간이 변경되었으면 alarmDismissed 리셋
            if (sensorData.sensors[id].targetTime !== cardData[id].time) {
                sensorData.sensors[id].alarmDismissed = false;
            }
            sensorData.sensors[id].targetTime = cardData[id].time; 
        }
    }
    saveData();
    res.json({ success: true, message: '저장됨' });
});

app.get('/api/device/status', authenticateToken, (req, res) => {
    const now = Date.now(), lastHB = sensorData.deviceInfo.lastHeartbeat ? new Date(sensorData.deviceInfo.lastHeartbeat).getTime() : 0;
    const isOnline = (now - lastHB) < 30000;
    sensorData.deviceInfo.isOnline = isOnline;
    const sensorRawData = {};
    for (let id in sensorData.sensors) sensorRawData[id] = { name: sensorData.sensors[id].name, value: sensorData.sensors[id].value };
    res.json({ success: true, device: { ...sensorData.deviceInfo, isOnline, timeSinceLastHeartbeat: lastHB ? Math.round((now - lastHB) / 1000) : null }, sensorRawData, isRefillMode: sensorData.isRefillMode });
});

app.post('/api/device/heartbeat', (req, res) => {
    const { ipAddress, firmwareVersion } = req.body;
    if (ipAddress) sensorData.deviceInfo.ipAddress = ipAddress;
    if (firmwareVersion) sensorData.deviceInfo.firmwareVersion = firmwareVersion;
    sensorData.deviceInfo.lastHeartbeat = new Date().toISOString();
    sensorData.deviceInfo.isOnline = true;
    res.json({ success: true, serverTime: new Date().toISOString() });
});

app.post('/api/device/calibrate', authenticateToken, (req, res) => {
    for (let id in sensorData.sensors) sensorData.sensors[id].value = 0;
    saveData();
    res.json({ success: true, message: '센서 영점 조절 완료' });
});

app.post('/api/device/test-email', authenticateToken, async (req, res) => {
    if (!mailTransporter) return res.status(400).json({ error: '이메일 서비스 미설정' });
    const user = sensorData.users.find(u => u.id === req.user.id);
    if (!user || !user.guardianEmail) return res.status(400).json({ error: '보호자 이메일 미설정' });
    const sent = await sendGuardianEmail(user.id, '[COSS] 테스트 알림', '<div style="font-family:sans-serif;padding:20px;"><h2>✅ COSS 테스트 알림</h2><p>이메일이 정상 수신되었습니다!</p></div>');
    if (sent) res.json({ success: true, message: `테스트 이메일 발송됨: ${user.guardianEmail}` });
    else res.status(500).json({ error: '발송 실패' });
});

app.get('/api/refill/status', authenticateToken, (req, res) => res.json({ 
    success: true, 
    isRefillMode: sensorData.isRefillMode,
    refillStartTime: sensorData.refillStartTime
}));
app.post('/api/refill/start', authenticateToken, (req, res) => { 
    sensorData.isRefillMode = true; 
    sensorData.refillStartTime = new Date().toISOString(); 
    saveData(); 
    res.json({ success: true, isRefillMode: true, refillStartTime: sensorData.refillStartTime }); 
});
app.post('/api/refill/end', authenticateToken, (req, res) => {
    const { refilledSlots, deleteRecordsDuringRefill } = req.body;
    let deletedCount = 0;
    if (deleteRecordsDuringRefill && sensorData.refillStartTime) {
        const refillStart = new Date(sensorData.refillStartTime).getTime();
        const originalLength = sensorData.history.length;
        sensorData.history = sensorData.history.filter(h => new Date(h.timestamp).getTime() < refillStart);
        deletedCount = originalLength - sensorData.history.length;
    }
    sensorData.isRefillMode = false;
    sensorData.refillStartTime = null;
    if (refilledSlots && Array.isArray(refilledSlots)) refilledSlots.forEach(slotId => { if (sensorData.sensors[slotId]) { sensorData.sensors[slotId].todayOpened = false; sensorData.sensors[slotId].missedAlertSent = false; sensorData.sensors[slotId].alarmDismissed = false; } });
    saveData();
    res.json({ success: true, isRefillMode: false, deletedCount });
});

app.delete('/api/history/:index', authenticateToken, (req, res) => {
    const index = parseInt(req.params.index);
    if (isNaN(index) || index < 0 || index >= sensorData.history.length) return res.status(400).json({ error: 'Invalid index' });
    const removed = sensorData.history.splice(index, 1);
    saveData();
    res.json({ success: true, removed: removed[0] });
});

app.get('/api/notifications/settings', authenticateToken, (req, res) => res.json({ success: true, settings: sensorData.notificationSettings }));
app.put('/api/notifications/settings', authenticateToken, (req, res) => {
    const { enabled, nightModeEnabled, nightStart, nightEnd } = req.body;
    if (enabled !== undefined) sensorData.notificationSettings.enabled = enabled;
    if (nightModeEnabled !== undefined) sensorData.notificationSettings.nightModeEnabled = nightModeEnabled;
    if (nightStart) sensorData.notificationSettings.nightStart = nightStart;
    if (nightEnd) sensorData.notificationSettings.nightEnd = nightEnd;
    saveData();
    res.json({ success: true, settings: sensorData.notificationSettings });
});

app.post('/api/data/reset', authenticateToken, async (req, res) => {
    if (req.body.confirmText !== '초기화') return res.status(400).json({ error: '"초기화"를 입력하세요' });
    const userId = req.user.id;
    // 센서 상태 초기화 (전역 - 하드웨어 연동)
    for (let id in sensorData.sensors) { sensorData.sensors[id].value = 0; sensorData.sensors[id].todayOpened = false; sensorData.sensors[id].lastOpened = null; sensorData.sensors[id].missedAlertSent = false; sensorData.sensors[id].alarmDismissed = false; }
    // 사용자별 히스토리만 삭제
    sensorData.history = sensorData.history.filter(h => h.userId !== userId);
    // 사용자 약물 설정 초기화
    sensorData.userMedications[userId] = null;
    saveData();
    res.json({ success: true, message: '초기화 완료' });
});

app.get('/api/dashboard/stats', authenticateToken, (req, res) => {
    const userId = req.user.id;
    const now = new Date(), today = now.toISOString().split('T')[0];
    
    // 사용자별 히스토리 필터링
    const userHistory = sensorData.history.filter(h => h.userId === userId);
    
    // 사용자별 일별 통계 계산
    const userDailyStats = {};
    userHistory.forEach(h => {
        if (h.action === 'removed' && h.timestamp) {
            const dateKey = h.timestamp.split('T')[0];
            if (!userDailyStats[dateKey]) userDailyStats[dateKey] = { date: dateKey, sensors: {} };
            if (!userDailyStats[dateKey].sensors[h.sensorId]) userDailyStats[dateKey].sensors[h.sensorId] = { count: 0, times: [] };
            userDailyStats[dateKey].sensors[h.sensorId].count++;
            userDailyStats[dateKey].sensors[h.sensorId].times.push(h.timestamp);
        }
    });
    
    const todayStats = userDailyStats[today] || { sensors: {} };
    const weekly = [];
    for (let i = 6; i >= 0; i--) {
        const d = new Date(); d.setDate(d.getDate() - i);
        const k = d.toISOString().split('T')[0], s = userDailyStats[k];
        let count = 0;
        if (s && s.sensors) Object.values(s.sensors).forEach(v => { if (v.count > 0) count++; });
        weekly.push({ date: k, completedCount: count, day: ['일', '월', '화', '수', '목', '금', '토'][d.getDay()] });
    }
    res.json({ sensors: sensorData.sensors, today: todayStats, weekly, adherenceRate: calculateAdherenceMetrics(userId).pdc, adherenceMetrics: calculateAdherenceMetrics(userId), lastAction: userHistory[0], isRefillMode: sensorData.isRefillMode });
});

app.get('/api/reports/detailed', authenticateToken, (req, res) => {
    const userId = req.user.id;
    
    // 사용자별 히스토리 필터링
    const userHistory = sensorData.history.filter(h => h.userId === userId);
    
    // 사용자별 일별 통계 계산
    const userDailyStats = {};
    userHistory.forEach(h => {
        if (h.action === 'removed' && h.timestamp) {
            const dateKey = h.timestamp.split('T')[0];
            if (!userDailyStats[dateKey]) userDailyStats[dateKey] = { date: dateKey, sensors: {} };
            if (!userDailyStats[dateKey].sensors[h.sensorId]) userDailyStats[dateKey].sensors[h.sensorId] = { count: 0, times: [] };
            userDailyStats[dateKey].sensors[h.sensorId].count++;
            userDailyStats[dateKey].sensors[h.sensorId].times.push(h.timestamp);
        }
    });
    
    const hourlyDistribution = new Array(24).fill(0), weekdayDistribution = new Array(7).fill(0);
    userHistory.forEach(h => { if (h.action === 'removed' && h.timestamp) { const d = new Date(h.timestamp); hourlyDistribution[d.getHours()]++; weekdayDistribution[d.getDay()]++; } });
    res.json({ sensorStats: sensorData.sensors, history: userHistory.slice(0, 200), totalDays: Object.keys(userDailyStats).length, dailyStats: userDailyStats, adherenceMetrics: calculateAdherenceMetrics(userId), distributions: { hourly: hourlyDistribution, weekday: weekdayDistribution } });
});

app.get('/api/medications', authenticateToken, (req, res) => res.json(Object.values(sensorData.sensors)));

app.get('/api/notifications/check', authenticateToken, (req, res) => {
    const now = new Date(), alerts = [];
    const nowKST = now.toLocaleString('ko-KR', { timeZone: 'Asia/Seoul' });
    console.log(`[알람체크] 서버시간: ${nowKST}`);
    
    if (!sensorData.notificationSettings.enabled) return res.json({ alerts: [] });
    for (let id in sensorData.sensors) {
        const sensor = sensorData.sensors[id];
        // 이미 복용했거나 알람을 확인(dismiss)한 경우 건너뜀
        if (sensor.todayOpened || sensor.alarmDismissed) {
            console.log(`[알람체크] 센서${id} 건너뜀 (todayOpened: ${sensor.todayOpened}, alarmDismissed: ${sensor.alarmDismissed})`);
            continue;
        }
        const [tHour, tMin] = sensor.targetTime.split(':').map(Number);
        const targetDate = new Date(now); targetDate.setHours(tHour, tMin, 0, 0);
        const diffMinutes = Math.round((now - targetDate) / 1000 / 60);
        console.log(`[알람체크] 센서${id} "${sensor.name}" 목표시간: ${sensor.targetTime}, 차이: ${diffMinutes}분`);
        // diffMinutes가 양수이고 30분 이내일 때만 알람 (현재 시간이 목표 시간을 지났을 때)
        // 추가: 현재 시간이 목표 시간보다 이전이면 알람 안 함 (예: 07:00에 22:00 알람 방지)
        if (diffMinutes > 0 && diffMinutes <= 30) {
            console.log(`[알람체크] ⏰ 센서${id} 알람 발생!`);
            alerts.push({ sensorId: id, type: 'warning', message: `🔔 ${sensor.emoji} ${sensor.name} 복용 시간입니다! (${diffMinutes}분 지남)`, playSound: true });
        }
    }
    res.json({ alerts });
});

// 알람 확인(dismiss) API - 오늘 하루 동안 해당 알람 끄기
app.post('/api/notifications/dismiss', authenticateToken, (req, res) => {
    const { sensorId } = req.body;
    if (!sensorId || !sensorData.sensors[sensorId]) {
        return res.status(400).json({ error: 'Invalid sensorId' });
    }
    sensorData.sensors[sensorId].alarmDismissed = true;
    saveData();
    res.json({ success: true, message: `센서 ${sensorId} 알람이 오늘 하루 동안 꺼졌습니다.` });
});

app.post('/api/admin/reset', (req, res) => {
    if (req.body.password !== 'admin2025') return res.status(403).json({ error: '비번 오류' });
    for (let id in sensorData.sensors) { sensorData.sensors[id].value = 0; sensorData.sensors[id].todayOpened = false; sensorData.sensors[id].lastOpened = null; sensorData.sensors[id].missedAlertSent = false; sensorData.sensors[id].alarmDismissed = false; }
    sensorData.history = [];
    sensorData.dailyStats = {};
    saveData();
    res.json({ success: true });
});

function resetDailyFlags() {
    const todayKey = new Date().toISOString().split('T')[0];
    if (!sensorData.lastResetDate || sensorData.lastResetDate !== todayKey) {
        for (let id in sensorData.sensors) { sensorData.sensors[id].todayOpened = false; sensorData.sensors[id].missedAlertSent = false; sensorData.sensors[id].alarmDismissed = false; }
        sensorData.lastResetDate = todayKey;
        saveData();
    }
}

function checkDeviceStatus() {
    const now = Date.now(), lastHB = sensorData.deviceInfo.lastHeartbeat ? new Date(sensorData.deviceInfo.lastHeartbeat).getTime() : 0;
    if ((now - lastHB) >= 30000) sensorData.deviceInfo.isOnline = false;
}

setInterval(resetDailyFlags, 60000);
setInterval(checkMissedMedication, 5000);
setInterval(checkDeviceStatus, 10000);
resetDailyFlags();

app.get('/health', (req, res) => res.json({ 
    status: 'ok',
    serverTime: new Date().toISOString(),
    serverTimeKST: new Date().toLocaleString('ko-KR', { timeZone: 'Asia/Seoul' }),
    timezone: Intl.DateTimeFormat().resolvedOptions().timeZone
}));

app.listen(PORT, () => {
    console.log(`🚀 Server running on port ${PORT}`);
    console.log(`📂 Data: ${DATA_FILE}`);
    if (mailTransporter) console.log('📧 Email enabled');
    else console.log('📧 Email disabled (nodemailer not installed or env vars missing)');
});
