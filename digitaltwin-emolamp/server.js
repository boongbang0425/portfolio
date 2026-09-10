/**
 * EmoLamp Statistics Server v3.0.0
 * 
 * 감정 로그 수집 및 실시간 통계 대시보드 서버
 * Unity에서 전송한 데이터를 수집하고 시각화
 * 
 * v3.0.0 - 고급 통계 기능 추가
 */

require('dotenv').config();
const express = require('express');
const cors = require('cors');
const path = require('path');

const app = express();
const PORT = process.env.PORT || 3000;

// ==================== 미들웨어 ====================
app.use(cors());
app.use(express.json());
app.use(express.static(path.join(__dirname, 'public')));

app.use((req, res, next) => {
    const timestamp = new Date().toISOString();
    console.log(`[${timestamp}] ${req.method} ${req.path}`);
    next();
});

// ==================== 데이터 저장소 ====================
const emotionLogs = [];
const weatherLogs = [];
const stateLogs = [];
const manualStateLogs = []; // v3.0 추가: Manual 상태 로그
const MAX_LOGS = 1000;

let currentState = {
    emotion: 'calm',
    hue: 120,
    saturation: 70,
    brightness: 70,
    colorHex: '#50C878',
    mode: 'AUTO',
    weather: null,
    temperature: null,
    humidity: null,
    timestamp: Date.now()
};

// ==================== 유틸리티 ====================
function getToday() {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
}

function getWeekAgo() {
    return Date.now() - 7 * 24 * 60 * 60 * 1000;
}

function getKoreanTime(timestamp) {
    const date = new Date(timestamp);
    return date.toLocaleString('ko-KR', { timeZone: 'Asia/Seoul' });
}

function getHourFromTimestamp(timestamp) {
    const date = new Date(timestamp);
    return date.getHours();
}

function getDayOfWeek(timestamp) {
    const date = new Date(timestamp);
    const days = ['일', '월', '화', '수', '목', '금', '토'];
    return days[date.getDay()];
}

function addLog(logs, entry) {
    logs.push(entry);
    if (logs.length > MAX_LOGS) {
        logs.shift();
    }
}

function getEmotionEmoji(emotion) {
    const emojis = {
        joy: '😊',
        sadness: '😢',
        anger: '😡',
        calm: '😌',
        excited: '🤩',
        fear: '😨',
        surprise: '😲'
    };
    return emojis[emotion] || '😐';
}

function getEmotionKorean(emotion) {
    const korean = {
        joy: '기쁨',
        sadness: '슬픔',
        anger: '분노',
        calm: '평온',
        excited: '설렘',
        fear: '두려움',
        surprise: '놀람'
    };
    return korean[emotion] || '중립';
}

function getWeatherEmoji(condition) {
    const emojis = {
        Clear: '☀️',
        Clouds: '☁️',
        Overcast: '🌥️',
        Rain: '🌧️',
        Snow: '❄️',
        Fog: '🌫️',
        Storm: '⛈️'
    };
    return emojis[condition] || '🌤️';
}

function getWeatherKorean(condition) {
    const korean = {
        Clear: '맑음',
        Clouds: '구름',
        Overcast: '흐림',
        Rain: '비',
        Snow: '눈',
        Fog: '안개',
        Storm: '폭풍'
    };
    return korean[condition] || '보통';
}

// ==================== 고급 통계 계산 함수 ====================

// 연속 동일 감정 최대 기록
function calculateConsecutiveEmotions() {
    if (emotionLogs.length === 0) return { emotion: null, count: 0 };
    
    let maxCount = 1;
    let maxEmotion = emotionLogs[0].emotion;
    let currentCount = 1;
    let currentEmotion = emotionLogs[0].emotion;
    
    for (let i = 1; i < emotionLogs.length; i++) {
        if (emotionLogs[i].emotion === currentEmotion) {
            currentCount++;
            if (currentCount > maxCount) {
                maxCount = currentCount;
                maxEmotion = currentEmotion;
            }
        } else {
            currentEmotion = emotionLogs[i].emotion;
            currentCount = 1;
        }
    }
    
    return {
        emotion: maxEmotion,
        emoji: getEmotionEmoji(maxEmotion),
        korean: getEmotionKorean(maxEmotion),
        count: maxCount
    };
}

// 감정 전환 빈도 계산
function calculateEmotionTransitions() {
    const transitions = {};
    
    for (let i = 1; i < emotionLogs.length; i++) {
        const from = emotionLogs[i - 1].emotion;
        const to = emotionLogs[i].emotion;
        
        if (from !== to) {
            const key = `${from}→${to}`;
            transitions[key] = (transitions[key] || 0) + 1;
        }
    }
    
    // 정렬하여 상위 5개 반환
    const sorted = Object.entries(transitions)
        .map(([transition, count]) => {
            const [from, to] = transition.split('→');
            return {
                from,
                to,
                fromEmoji: getEmotionEmoji(from),
                toEmoji: getEmotionEmoji(to),
                count
            };
        })
        .sort((a, b) => b.count - a.count)
        .slice(0, 5);
    
    return {
        total: emotionLogs.length > 1 ? emotionLogs.length - 1 : 0,
        transitions: sorted
    };
}

// 평균 분석 간격 계산
function calculateAverageInterval() {
    if (emotionLogs.length < 2) return { minutes: 0, formatted: '데이터 부족' };
    
    let totalInterval = 0;
    for (let i = 1; i < emotionLogs.length; i++) {
        totalInterval += emotionLogs[i].timestamp - emotionLogs[i - 1].timestamp;
    }
    
    const avgMs = totalInterval / (emotionLogs.length - 1);
    const avgMinutes = Math.round(avgMs / 60000);
    
    let formatted;
    if (avgMinutes < 60) {
        formatted = `${avgMinutes}분`;
    } else {
        const hours = Math.floor(avgMinutes / 60);
        const mins = avgMinutes % 60;
        formatted = `${hours}시간 ${mins}분`;
    }
    
    return { minutes: avgMinutes, formatted };
}

// AUTO vs MANUAL 비율 계산
function calculateModeRatio() {
    const modeChanges = stateLogs.filter(log => log.action === 'mode_change');
    
    let autoCount = 0;
    let manualCount = 0;
    
    modeChanges.forEach(log => {
        if (log.mode === 'AUTO') autoCount++;
        else if (log.mode === 'MANUAL') manualCount++;
    });
    
    const total = autoCount + manualCount;
    
    return {
        auto: autoCount,
        manual: manualCount,
        autoPercent: total > 0 ? Math.round((autoCount / total) * 100) : 50,
        manualPercent: total > 0 ? Math.round((manualCount / total) * 100) : 50,
        totalChanges: total
    };
}

// 모드 전환 빈도 계산
function calculateModeTransitions() {
    let transitions = 0;
    let lastMode = null;
    
    stateLogs.forEach(log => {
        if (log.mode && log.mode !== lastMode) {
            if (lastMode !== null) transitions++;
            lastMode = log.mode;
        }
    });
    
    return transitions;
}

// 날씨별 감정 분포 계산
function calculateWeatherEmotionCorrelation() {
    const weatherEmotions = {};
    
    // 각 감정 로그에 대해 가장 가까운 날씨 로그 찾기
    emotionLogs.forEach(emotionLog => {
        // 해당 시점 ±30분 이내의 날씨 찾기
        const nearbyWeather = weatherLogs.find(w => 
            Math.abs(w.timestamp - emotionLog.timestamp) < 30 * 60 * 1000
        );
        
        if (nearbyWeather) {
            const weather = nearbyWeather.condition;
            if (!weatherEmotions[weather]) {
                weatherEmotions[weather] = {
                    total: 0,
                    emotions: {},
                    avgTemperature: 0,
                    temperatures: []
                };
            }
            
            weatherEmotions[weather].total++;
            weatherEmotions[weather].emotions[emotionLog.emotion] = 
                (weatherEmotions[weather].emotions[emotionLog.emotion] || 0) + 1;
            weatherEmotions[weather].temperatures.push(nearbyWeather.temperature);
        }
    });
    
    // 결과 정리
    const result = Object.entries(weatherEmotions).map(([weather, data]) => {
        // 해당 날씨의 주요 감정 찾기
        let dominantEmotion = 'calm';
        let maxCount = 0;
        
        Object.entries(data.emotions).forEach(([emotion, count]) => {
            if (count > maxCount) {
                maxCount = count;
                dominantEmotion = emotion;
            }
        });
        
        // 평균 온도 계산
        const avgTemp = data.temperatures.length > 0
            ? Math.round(data.temperatures.reduce((a, b) => a + b, 0) / data.temperatures.length)
            : 0;
        
        return {
            weather,
            weatherEmoji: getWeatherEmoji(weather),
            weatherKorean: getWeatherKorean(weather),
            total: data.total,
            dominantEmotion,
            dominantEmoji: getEmotionEmoji(dominantEmotion),
            dominantKorean: getEmotionKorean(dominantEmotion),
            dominantPercent: data.total > 0 ? Math.round((maxCount / data.total) * 100) : 0,
            avgTemperature: avgTemp,
            emotionBreakdown: Object.entries(data.emotions).map(([e, c]) => ({
                emotion: e,
                emoji: getEmotionEmoji(e),
                count: c,
                percent: Math.round((c / data.total) * 100)
            })).sort((a, b) => b.count - a.count)
        };
    }).sort((a, b) => b.total - a.total);
    
    return result;
}

// 시간대별 감정 패턴 (아침/점심/저녁/밤)
function calculateTimeOfDayPattern() {
    const periods = {
        morning: { name: '아침', icon: '🌅', range: [6, 12], emotions: {} },
        afternoon: { name: '점심', icon: '☀️', range: [12, 18], emotions: {} },
        evening: { name: '저녁', icon: '🌆', range: [18, 22], emotions: {} },
        night: { name: '밤', icon: '🌙', range: [22, 6], emotions: {} }
    };
    
    emotionLogs.forEach(log => {
        const hour = getHourFromTimestamp(log.timestamp);
        let period;
        
        if (hour >= 6 && hour < 12) period = 'morning';
        else if (hour >= 12 && hour < 18) period = 'afternoon';
        else if (hour >= 18 && hour < 22) period = 'evening';
        else period = 'night';
        
        periods[period].emotions[log.emotion] = (periods[period].emotions[log.emotion] || 0) + 1;
    });
    
    // 각 시간대의 주요 감정 계산
    return Object.entries(periods).map(([key, data]) => {
        let dominant = 'calm';
        let maxCount = 0;
        let total = 0;
        
        Object.entries(data.emotions).forEach(([emotion, count]) => {
            total += count;
            if (count > maxCount) {
                maxCount = count;
                dominant = emotion;
            }
        });
        
        return {
            period: key,
            name: data.name,
            icon: data.icon,
            total,
            dominantEmotion: dominant,
            dominantEmoji: getEmotionEmoji(dominant),
            dominantPercent: total > 0 ? Math.round((maxCount / total) * 100) : 0
        };
    });
}

// 요일별 감정 패턴
function calculateDayOfWeekPattern() {
    const days = {
        '일': { emotions: {}, total: 0 },
        '월': { emotions: {}, total: 0 },
        '화': { emotions: {}, total: 0 },
        '수': { emotions: {}, total: 0 },
        '목': { emotions: {}, total: 0 },
        '금': { emotions: {}, total: 0 },
        '토': { emotions: {}, total: 0 }
    };
    
    emotionLogs.forEach(log => {
        const day = getDayOfWeek(log.timestamp);
        days[day].emotions[log.emotion] = (days[day].emotions[log.emotion] || 0) + 1;
        days[day].total++;
    });
    
    return Object.entries(days).map(([day, data]) => {
        let dominant = 'calm';
        let maxCount = 0;
        
        Object.entries(data.emotions).forEach(([emotion, count]) => {
            if (count > maxCount) {
                maxCount = count;
                dominant = emotion;
            }
        });
        
        return {
            day,
            total: data.total,
            dominantEmotion: dominant,
            dominantEmoji: getEmotionEmoji(dominant),
            dominantPercent: data.total > 0 ? Math.round((maxCount / data.total) * 100) : 0
        };
    });
}

// Hue 분포 히스토그램 (30도 단위)
function calculateHueDistribution() {
    const buckets = {};
    const bucketSize = 30;
    
    // 0-360을 30도 단위로 12개 버킷
    for (let i = 0; i < 360; i += bucketSize) {
        buckets[i] = { min: i, max: i + bucketSize, count: 0, color: `hsl(${i + 15}, 70%, 50%)` };
    }
    
    emotionLogs.forEach(log => {
        const bucket = Math.floor(log.hue / bucketSize) * bucketSize;
        if (buckets[bucket]) {
            buckets[bucket].count++;
        }
    });
    
    return Object.values(buckets);
}

// 가장 많이 사용된 색상 HEX
function calculateTopColors() {
    const colorCounts = {};
    
    emotionLogs.forEach(log => {
        if (log.colorHex) {
            colorCounts[log.colorHex] = (colorCounts[log.colorHex] || 0) + 1;
        }
    });
    
    // Manual 상태 로그도 포함
    manualStateLogs.forEach(log => {
        if (log.colorHex) {
            colorCounts[log.colorHex] = (colorCounts[log.colorHex] || 0) + 1;
        }
    });
    
    return Object.entries(colorCounts)
        .map(([color, count]) => ({ color, count }))
        .sort((a, b) => b.count - a.count)
        .slice(0, 10);
}

// ==================== 로그 수집 API ====================

// 감정 분석 로그 저장
app.post('/api/log/emotion', (req, res) => {
    try {
        const { emotion, hue, saturation, brightness, summary, colorHex } = req.body;
        
        const entry = {
            emotion: emotion || 'calm',
            hue: hue || 120,
            saturation: saturation || 70,
            brightness: brightness || 70,
            summary: summary || '',
            colorHex: colorHex || '#50C878',
            emoji: getEmotionEmoji(emotion),
            emotionKorean: getEmotionKorean(emotion),
            timestamp: Date.now()
        };
        
        addLog(emotionLogs, entry);
        
        Object.assign(currentState, {
            emotion: entry.emotion,
            hue: entry.hue,
            saturation: entry.saturation,
            brightness: entry.brightness,
            colorHex: entry.colorHex,
            timestamp: entry.timestamp
        });
        
        console.log(`[EMOTION] ${entry.emoji} ${entry.emotionKorean} (H:${entry.hue})`);
        res.json({ success: true, entry });
    } catch (error) {
        console.error('[ERROR] log/emotion:', error);
        res.status(500).json({ error: error.message });
    }
});

// 날씨 정보 로그 저장
app.post('/api/log/weather', (req, res) => {
    try {
        const { temperature, humidity, condition, description, cityName } = req.body;
        
        const entry = {
            temperature: temperature || 0,
            humidity: humidity || 0,
            condition: condition || 'Clouds',
            description: description || '',
            cityName: cityName || 'Seoul',
            emoji: getWeatherEmoji(condition),
            timestamp: Date.now()
        };
        
        addLog(weatherLogs, entry);
        
        currentState.weather = entry.condition;
        currentState.temperature = entry.temperature;
        currentState.humidity = entry.humidity;
        
        console.log(`[WEATHER] ${entry.emoji} ${entry.temperature}°C ${entry.condition}`);
        res.json({ success: true, entry });
    } catch (error) {
        console.error('[ERROR] log/weather:', error);
        res.status(500).json({ error: error.message });
    }
});

// 상태 변경 로그 저장
app.post('/api/log/state', (req, res) => {
    try {
        const { mode, action, details } = req.body;
        
        const entry = {
            mode: mode || 'AUTO',
            action: action || 'unknown',
            details: details || {},
            timestamp: Date.now()
        };
        
        addLog(stateLogs, entry);
        currentState.mode = entry.mode;
        
        console.log(`[STATE] ${entry.action} (mode: ${entry.mode})`);
        res.json({ success: true, entry });
    } catch (error) {
        console.error('[ERROR] log/state:', error);
        res.status(500).json({ error: error.message });
    }
});

// Manual 상태 실시간 업데이트
app.post('/api/log/manualstate', (req, res) => {
    try {
        const { mode, hue, saturation, brightness, colorHex } = req.body;
        
        const entry = {
            mode: mode || 'MANUAL',
            hue: hue || 0,
            saturation: saturation || 0,
            brightness: brightness || 0,
            colorHex: colorHex || '#FFFFFF',
            timestamp: Date.now()
        };
        
        addLog(manualStateLogs, entry);
        
        currentState.mode = entry.mode;
        currentState.hue = entry.hue;
        currentState.saturation = entry.saturation;
        currentState.brightness = entry.brightness;
        currentState.colorHex = entry.colorHex;
        currentState.timestamp = entry.timestamp;
        
        console.log(`[MANUAL] Color: ${colorHex} HSV(${hue}, ${saturation}, ${brightness})`);
        res.json({ success: true });
    } catch (error) {
        console.error('[ERROR] log/manualstate:', error);
        res.status(500).json({ error: error.message });
    }
});

// ==================== 통계 API ====================

app.get('/api/stats/today', (req, res) => {
    try {
        const todayStart = getToday();
        const todayLogs = emotionLogs.filter(log => log.timestamp >= todayStart);
        
        const emotionCounts = {};
        todayLogs.forEach(log => {
            emotionCounts[log.emotion] = (emotionCounts[log.emotion] || 0) + 1;
        });
        
        let dominantEmotion = 'calm';
        let maxCount = 0;
        for (const [emotion, count] of Object.entries(emotionCounts)) {
            if (count > maxCount) {
                maxCount = count;
                dominantEmotion = emotion;
            }
        }
        
        res.json({
            count: todayLogs.length,
            emotionCounts,
            dominantEmotion,
            dominantEmoji: getEmotionEmoji(dominantEmotion),
            dominantKorean: getEmotionKorean(dominantEmotion)
        });
    } catch (error) {
        console.error('[ERROR] stats/today:', error);
        res.status(500).json({ error: error.message });
    }
});

app.get('/api/stats/emotions', (req, res) => {
    try {
        const counts = {};
        const total = emotionLogs.length;
        
        emotionLogs.forEach(log => {
            counts[log.emotion] = (counts[log.emotion] || 0) + 1;
        });
        
        const distribution = Object.entries(counts).map(([emotion, count]) => ({
            emotion,
            emoji: getEmotionEmoji(emotion),
            korean: getEmotionKorean(emotion),
            count,
            percentage: total > 0 ? Math.round((count / total) * 100) : 0
        }));
        
        distribution.sort((a, b) => b.count - a.count);
        res.json({ total, distribution });
    } catch (error) {
        console.error('[ERROR] stats/emotions:', error);
        res.status(500).json({ error: error.message });
    }
});

app.get('/api/stats/timeline', (req, res) => {
    try {
        const todayStart = getToday();
        const todayLogs = emotionLogs.filter(log => log.timestamp >= todayStart);
        
        const hourlyData = {};
        for (let i = 0; i < 24; i++) {
            hourlyData[i] = [];
        }
        
        todayLogs.forEach(log => {
            const hour = getHourFromTimestamp(log.timestamp);
            hourlyData[hour].push(log);
        });
        
        const timeline = [];
        for (let hour = 0; hour < 24; hour++) {
            const logs = hourlyData[hour];
            if (logs.length > 0) {
                const lastLog = logs[logs.length - 1];
                timeline.push({
                    hour,
                    emotion: lastLog.emotion,
                    emoji: lastLog.emoji,
                    hue: lastLog.hue,
                    colorHex: lastLog.colorHex,
                    count: logs.length
                });
            } else {
                timeline.push({
                    hour,
                    emotion: null,
                    emoji: null,
                    hue: null,
                    colorHex: null,
                    count: 0
                });
            }
        }
        
        res.json({ timeline });
    } catch (error) {
        console.error('[ERROR] stats/timeline:', error);
        res.status(500).json({ error: error.message });
    }
});

// v3.0 확장된 summary
app.get('/api/stats/summary', (req, res) => {
    try {
        const todayStart = getToday();
        const todayEmotionLogs = emotionLogs.filter(log => log.timestamp >= todayStart);
        
        // 평균 HSV 계산
        let avgHue = 120, avgSaturation = 70, avgBrightness = 70;
        if (emotionLogs.length > 0) {
            avgHue = Math.round(emotionLogs.reduce((sum, log) => sum + log.hue, 0) / emotionLogs.length);
            avgSaturation = Math.round(emotionLogs.reduce((sum, log) => sum + log.saturation, 0) / emotionLogs.length);
            avgBrightness = Math.round(emotionLogs.reduce((sum, log) => sum + log.brightness, 0) / emotionLogs.length);
        }
        
        const emotionCounts = {};
        emotionLogs.forEach(log => {
            emotionCounts[log.emotion] = (emotionCounts[log.emotion] || 0) + 1;
        });
        
        let topEmotion = 'calm';
        let maxCount = 0;
        for (const [emotion, count] of Object.entries(emotionCounts)) {
            if (count > maxCount) {
                maxCount = count;
                topEmotion = emotion;
            }
        }
        
        // 가장 활발한 시간대
        const hourCounts = {};
        emotionLogs.forEach(log => {
            const hour = getHourFromTimestamp(log.timestamp);
            hourCounts[hour] = (hourCounts[hour] || 0) + 1;
        });
        
        let peakHour = 12;
        let peakCount = 0;
        Object.entries(hourCounts).forEach(([hour, count]) => {
            if (count > peakCount) {
                peakCount = count;
                peakHour = parseInt(hour);
            }
        });
        
        res.json({
            totalAnalyses: emotionLogs.length,
            todayAnalyses: todayEmotionLogs.length,
            topEmotion,
            topEmotionEmoji: getEmotionEmoji(topEmotion),
            topEmotionKorean: getEmotionKorean(topEmotion),
            averageHue: avgHue,
            averageSaturation: avgSaturation,
            averageBrightness: avgBrightness,
            peakHour,
            peakHourFormatted: `${peakHour.toString().padStart(2, '0')}:00`,
            weatherLogs: weatherLogs.length,
            stateLogs: stateLogs.length,
            manualStateLogs: manualStateLogs.length
        });
    } catch (error) {
        console.error('[ERROR] stats/summary:', error);
        res.status(500).json({ error: error.message });
    }
});

app.get('/api/stats/recent', (req, res) => {
    try {
        const limit = Math.min(parseInt(req.query.limit) || 10, 50);
        
        const recentEmotions = emotionLogs
            .slice(-limit)
            .reverse()
            .map(log => ({
                ...log,
                timeString: getKoreanTime(log.timestamp)
            }));
        
        res.json({ logs: recentEmotions });
    } catch (error) {
        console.error('[ERROR] stats/recent:', error);
        res.status(500).json({ error: error.message });
    }
});

app.get('/api/stats/current', (req, res) => {
    try {
        res.json({
            ...currentState,
            emotionEmoji: getEmotionEmoji(currentState.emotion),
            emotionKorean: getEmotionKorean(currentState.emotion),
            weatherEmoji: getWeatherEmoji(currentState.weather),
            weatherKorean: getWeatherKorean(currentState.weather),
            timeString: getKoreanTime(currentState.timestamp)
        });
    } catch (error) {
        console.error('[ERROR] stats/current:', error);
        res.status(500).json({ error: error.message });
    }
});

// ==================== v3.0 고급 통계 API ====================

// 고급 분석 통계
app.get('/api/stats/advanced', (req, res) => {
    try {
        res.json({
            consecutiveRecord: calculateConsecutiveEmotions(),
            emotionTransitions: calculateEmotionTransitions(),
            averageInterval: calculateAverageInterval(),
            modeRatio: calculateModeRatio(),
            modeTransitions: calculateModeTransitions()
        });
    } catch (error) {
        console.error('[ERROR] stats/advanced:', error);
        res.status(500).json({ error: error.message });
    }
});

// 날씨-감정 상관관계
app.get('/api/stats/weather-correlation', (req, res) => {
    try {
        res.json({
            correlations: calculateWeatherEmotionCorrelation()
        });
    } catch (error) {
        console.error('[ERROR] stats/weather-correlation:', error);
        res.status(500).json({ error: error.message });
    }
});

// 시간대별 패턴
app.get('/api/stats/time-patterns', (req, res) => {
    try {
        res.json({
            timeOfDay: calculateTimeOfDayPattern(),
            dayOfWeek: calculateDayOfWeekPattern()
        });
    } catch (error) {
        console.error('[ERROR] stats/time-patterns:', error);
        res.status(500).json({ error: error.message });
    }
});

// Hue 분포 및 색상 통계
app.get('/api/stats/color-analysis', (req, res) => {
    try {
        res.json({
            hueDistribution: calculateHueDistribution(),
            topColors: calculateTopColors()
        });
    } catch (error) {
        console.error('[ERROR] stats/color-analysis:', error);
        res.status(500).json({ error: error.message });
    }
});

// 날씨 로그 조회
app.get('/api/stats/weather', (req, res) => {
    try {
        const limit = Math.min(parseInt(req.query.limit) || 10, 50);
        
        const recentWeather = weatherLogs
            .slice(-limit)
            .reverse()
            .map(log => ({
                ...log,
                timeString: getKoreanTime(log.timestamp)
            }));
        
        res.json({ logs: recentWeather });
    } catch (error) {
        console.error('[ERROR] stats/weather:', error);
        res.status(500).json({ error: error.message });
    }
});

// ==================== 기타 엔드포인트 ====================

app.get('/api/status', (req, res) => {
    res.json({
        status: 'ok',
        server: 'EmoLamp Statistics Server',
        version: '3.0.0',
        uptime: process.uptime(),
        logs: {
            emotion: emotionLogs.length,
            weather: weatherLogs.length,
            state: stateLogs.length,
            manual: manualStateLogs.length
        },
        timestamp: Date.now()
    });
});

app.get('/healthz', (req, res) => {
    res.status(200).send('OK');
});

app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

app.use((req, res) => {
    res.status(404).json({ error: 'Not Found' });
});

app.use((err, req, res, next) => {
    console.error('[ERROR]', err);
    res.status(500).json({ error: err.message });
});

// ==================== 서버 시작 ====================
app.listen(PORT, () => {
    console.log('========================================');
    console.log(`  EmoLamp Statistics Server v3.0.0`);
    console.log(`  Running on port ${PORT}`);
    console.log(`  http://localhost:${PORT}`);
    console.log('========================================');
    console.log('  New Features:');
    console.log('  - Advanced emotion analytics');
    console.log('  - Weather-emotion correlation');
    console.log('  - Time pattern analysis');
    console.log('  - Color distribution stats');
    console.log('========================================');
});

// 오래된 로그 정리 (1시간마다)
setInterval(() => {
    const now = Date.now();
    const maxAge = 24 * 60 * 60 * 1000;
    
    const clean = (arr) => {
        const filtered = arr.filter(log => now - log.timestamp < maxAge);
        arr.length = 0;
        arr.push(...filtered);
    };
    
    clean(emotionLogs);
    clean(weatherLogs);
    clean(stateLogs);
    clean(manualStateLogs);
}, 60 * 60 * 1000);