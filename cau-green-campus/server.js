const express = require('express');
const path = require('path');
const compression = require('compression');

const app = express();
const PORT = process.env.PORT || 3000;
const NODE_ENV = process.env.NODE_ENV || 'development';

// Gzip 압축
app.use(compression());

// JSON 파싱
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// CORS 설정
app.use((req, res, next) => {
    res.header('Access-Control-Allow-Origin', '*');
    res.header('Access-Control-Allow-Headers', 'Origin, X-Requested-With, Content-Type, Accept');
    next();
});

// 정적 파일 서빙 (public 폴더)
app.use(express.static(path.join(__dirname, 'public')));

// 메인 페이지
app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

// 탄소계산기 페이지
app.get('/calculator', (req, res) => {
    res.sendFile(path.join(__dirname, 'public', 'calculator.html'));
});

// 실천가이드 페이지
app.get('/action', (req, res) => {
    res.sendFile(path.join(__dirname, 'public', 'action.html'));
});

// Health check (Cloudtype용)
app.get('/health', (req, res) => {
    res.status(200).json({ 
        status: 'ok', 
        timestamp: new Date().toISOString(),
        environment: NODE_ENV
    });
});

// API 엔드포인트 - 탄소 계산 (선택적 확장용)
app.post('/api/calculate', (req, res) => {
    const { transport, distance, meat, delivery, hvac, standby, clothing, disposable } = req.body;
    
    // 이동 부문 계산
    const transportFactors = { car: 0.21, public: 0.05, bike: 0, walk: 0 };
    const transportEmission = (distance || 0) * (transportFactors[transport] || 0) * 22;
    
    // 식습관 부문 계산
    const meatEmission = (meat || 0) * 6.5 * 4;
    const deliveryEmission = (delivery || 0) * 1.5 * 4;
    const dietEmission = meatEmission + deliveryEmission;
    
    // 에너지 부문 계산
    const hvacEmission = (hvac || 0) * 0.5 * 30;
    const standbyFactors = { good: 5, normal: 15, bad: 30 };
    const energyEmission = hvacEmission + (standbyFactors[standby] || 15);
    
    // 소비 부문 계산
    const clothingEmission = (clothing || 0) * 20;
    const disposableFactors = { low: 5, normal: 15, high: 30 };
    const consumptionEmission = clothingEmission + (disposableFactors[disposable] || 15);
    
    // 총합
    const total = Math.round(transportEmission + dietEmission + energyEmission + consumptionEmission);
    
    res.json({
        total,
        breakdown: {
            transport: Math.round(transportEmission),
            diet: Math.round(dietEmission),
            energy: Math.round(energyEmission),
            consumption: Math.round(consumptionEmission)
        }
    });
});

// 404 처리
app.use((req, res) => {
    res.status(404).sendFile(path.join(__dirname, 'public', 'index.html'));
});

// 에러 처리
app.use((err, req, res, next) => {
    console.error('Server Error:', err);
    res.status(500).json({ 
        error: 'Internal Server Error',
        message: NODE_ENV === 'development' ? err.message : undefined
    });
});

// 서버 시작
app.listen(PORT, '0.0.0.0', () => {
    console.log('');
    console.log('🌍 CAU Green Campus Action Server');
    console.log('================================');
    console.log(`✅ 서버가 시작되었습니다!`);
    console.log(`📍 포트: ${PORT}`);
    console.log(`🔧 환경: ${NODE_ENV}`);
    console.log('');
    if (NODE_ENV === 'development') {
        console.log('페이지 목록:');
        console.log(`  - 메인: http://localhost:${PORT}/`);
        console.log(`  - 탄소계산기: http://localhost:${PORT}/calculator.html`);
        console.log(`  - 실천가이드: http://localhost:${PORT}/action.html`);
        console.log('');
    }
});

module.exports = app;
