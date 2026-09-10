// ==========================================
//   FoodSave Backend Server
// ==========================================

require('dotenv').config();
const express = require('express');
const mysql = require('mysql2');
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');
const cors = require('cors');
const path = require('path');
const multer = require('multer');

const app = express();
const PORT = process.env.PORT || 3000;

// 시작 로그
console.log('🚀 Starting FoodSave Server...');
console.log('📁 Project Directory:', __dirname);

// 파일 업로드 설정
const storage = multer.diskStorage({
  destination: (req, file, cb) => {
    cb(null, path.join(__dirname, 'public', 'uploads'));
  },
  filename: (req, file, cb) => {
    const uniqueSuffix = Date.now() + '-' + Math.round(Math.random() * 1E9);
    cb(null, uniqueSuffix + path.extname(file.originalname));
  }
});

const upload = multer({ 
  storage: storage,
  limits: { fileSize: 5 * 1024 * 1024 } // 5MB
});

// 미들웨어
app.use(cors());
app.use(express.json());
app.use(express.urlencoded({ extended: true }));
app.use(express.static(path.join(__dirname, 'public')));

// 업로드 폴더 생성
const fs = require('fs');
const uploadsDir = path.join(__dirname, 'public', 'uploads');
if (!fs.existsSync(uploadsDir)) {
  fs.mkdirSync(uploadsDir, { recursive: true });
  console.log('📁 Created uploads directory');
}

// DB 연결
const db = mysql.createConnection({
  host: process.env.DB_HOST || 'localhost',
  user: process.env.DB_USER || 'root',
  password: process.env.DB_PASSWORD || '',
  database: process.env.DB_NAME || 'foodsave',
  port: process.env.DB_PORT || 3306
});

let dbConnected = false;
db.connect((err) => {
  if (err) {
    console.error('⚠️  Database connection failed:', err.message);
    console.log('📌 Running in DEMO mode without database');
    dbConnected = false;
  } else {
    console.log('✅ Connected to MariaDB database');
    dbConnected = true;
  }
});

// JWT 인증 미들웨어
const authenticateToken = (req, res, next) => {
  const authHeader = req.headers['authorization'];
  const token = authHeader && authHeader.split(' ')[1];

  if (!token) {
    return res.status(401).json({ error: '인증이 필요합니다.' });
  }

  jwt.verify(token, process.env.JWT_SECRET || 'demo_secret', (err, user) => {
    if (err) {
      return res.status(403).json({ error: '유효하지 않은 토큰입니다.' });
    }
    req.user = user;
    next();
  });
};

// ==========================================
//   페이지 라우트
// ==========================================
app.get('/', (req, res) => res.sendFile(path.join(__dirname, 'public', 'index.html')));
app.get('/donations', (req, res) => res.sendFile(path.join(__dirname, 'public', 'donations.html')));
app.get('/donation/:id', (req, res) => res.sendFile(path.join(__dirname, 'public', 'donation-detail.html')));
app.get('/info', (req, res) => res.sendFile(path.join(__dirname, 'public', 'info.html')));
app.get('/mediaart', (req, res) => res.sendFile(path.join(__dirname, 'public', 'mediaart.html')));
app.get('/dashboard', (req, res) => res.sendFile(path.join(__dirname, 'public', 'dashboard.html')));
app.get('/profile', (req, res) => res.sendFile(path.join(__dirname, 'public', 'profile.html')));

// ==========================================
//   API - 인증
// ==========================================

// 회원가입
app.post('/api/register', async (req, res) => {
  try {
    const { name, email, password, userType, phone, address } = req.body;
    
    if (!name || !email || !password || !userType) {
      return res.status(400).json({ error: '필수 정보를 입력해주세요.' });
    }

    if (!dbConnected) {
      // 데모 모드
      const token = jwt.sign({ id: 1, email, userType }, process.env.JWT_SECRET || 'demo_secret', { expiresIn: '7d' });
      return res.json({
        token,
        user: { id: 1, name, email, userType, phone, address }
      });
    }
    
    // 이메일 중복 체크
    db.query('SELECT * FROM users WHERE email = ?', [email], async (err, results) => {
      if (err) {
        return res.status(500).json({ error: '서버 오류가 발생했습니다.' });
      }
      
      if (results.length > 0) {
        return res.status(400).json({ error: '이미 사용 중인 이메일입니다.' });
      }
      
      // 비밀번호 암호화
      const hashedPassword = await bcrypt.hash(password, 10);
      
      // 사용자 생성
      const query = 'INSERT INTO users (name, email, password, user_type, phone, address) VALUES (?, ?, ?, ?, ?, ?)';
      db.query(query, [name, email, hashedPassword, userType, phone, address], (err, result) => {
        if (err) {
          console.error('Registration error:', err);
          return res.status(500).json({ error: '회원가입에 실패했습니다.' });
        }
        
        const token = jwt.sign({ id: result.insertId, email, userType }, process.env.JWT_SECRET || 'demo_secret', { expiresIn: '7d' });
        res.json({
          token,
          user: { id: result.insertId, name, email, userType, phone, address }
        });
      });
    });
    
  } catch (error) {
    console.error('Registration error:', error);
    res.status(500).json({ error: '회원가입에 실패했습니다.' });
  }
});

// 로그인
app.post('/api/login', async (req, res) => {
  try {
    const { email, password } = req.body;
    
    if (!email || !password) {
      return res.status(400).json({ error: '이메일과 비밀번호를 입력해주세요.' });
    }

    if (!dbConnected) {
      // 데모 모드
      const token = jwt.sign({ id: 1, email, userType: 'receiver' }, process.env.JWT_SECRET || 'demo_secret', { expiresIn: '7d' });
      return res.json({
        token,
        user: { id: 1, name: 'Demo User', email, userType: 'receiver' }
      });
    }
    
    db.query('SELECT * FROM users WHERE email = ?', [email], async (err, results) => {
      if (err) {
        return res.status(500).json({ error: '서버 오류가 발생했습니다.' });
      }
      
      if (results.length === 0) {
        return res.status(401).json({ error: '이메일 또는 비밀번호가 올바르지 않습니다.' });
      }
      
      const user = results[0];
      const validPassword = await bcrypt.compare(password, user.password);
      
      if (!validPassword) {
        return res.status(401).json({ error: '이메일 또는 비밀번호가 올바르지 않습니다.' });
      }
      
      const token = jwt.sign({ id: user.id, email: user.email, userType: user.user_type }, process.env.JWT_SECRET || 'demo_secret', { expiresIn: '7d' });
      
      res.json({
        token,
        user: {
          id: user.id,
          name: user.name,
          email: user.email,
          userType: user.user_type,
          phone: user.phone,
          address: user.address,
          profileImage: user.profile_image
        }
      });
    });
    
  } catch (error) {
    console.error('Login error:', error);
    res.status(500).json({ error: '로그인에 실패했습니다.' });
  }
});

// ==========================================
//   API - 카테고리
// ==========================================
app.get('/api/categories', (req, res) => {
  const categories = [
    { id: 1, name: '빵/베이커리', slug: 'bakery' },
    { id: 2, name: '과일/채소', slug: 'produce' },
    { id: 3, name: '도시락/식사', slug: 'meals' },
    { id: 4, name: '음료/커피', slug: 'beverages' },
    { id: 5, name: '기타', slug: 'others' }
  ];
  res.json(categories);
});

// ==========================================
//   API - 나눔 (Donations)
// ==========================================

// 나눔 목록 조회
app.get('/api/donations', (req, res) => {
  const { category, search, sort = 'newest', page = 1, limit = 12 } = req.query;
  
  if (!dbConnected) {
    // 데모 데이터 (10개 이상)
    const demoData = [
      {
        id: 1,
        title: '신선한 크로와상 & 베이글',
        description: '오늘 아침에 구운 신선한 빵입니다. 마감 시간 전에 가져가세요!',
        category_name: '빵/베이커리',
        quantity: '10',
        unit: '개',
        expiry_time: new Date(Date.now() + 3 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 강남구 테헤란로 123',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1555507036-ab1f4038808a?w=400'
      },
      {
        id: 2,
        title: '유기농 사과와 배',
        description: '신선한 유기농 과일입니다. 상처 없이 깨끗합니다.',
        category_name: '과일/채소',
        quantity: '5',
        unit: 'kg',
        expiry_time: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 마포구 홍대입구역 2번 출구',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1568702846914-96b305d2aaeb?w=400'
      },
      {
        id: 3,
        title: '김치찌개 도시락 세트',
        description: '집에서 만든 정성 가득 김치찌개와 밥, 반찬 세트입니다.',
        category_name: '도시락/식사',
        quantity: '3',
        unit: '인분',
        expiry_time: new Date(Date.now() + 4 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 송파구 잠실역 인근',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1498654896293-37aacf113fd9?w=400'
      },
      {
        id: 4,
        title: '아메리카노 & 라떼',
        description: '유통기한 임박 커피 음료입니다. 맛은 문제없어요!',
        category_name: '음료/커피',
        quantity: '8',
        unit: '병',
        expiry_time: new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 중구 명동역 근처',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=400'
      },
      {
        id: 5,
        title: '샌드위치 & 샐러드',
        description: '오늘 만든 신선한 샌드위치와 시저 샐러드입니다.',
        category_name: '빵/베이커리',
        quantity: '6',
        unit: '개',
        expiry_time: new Date(Date.now() + 5 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 서초구 강남역 10번 출구',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1528735602780-2552fd46c7af?w=400'
      },
      {
        id: 6,
        title: '방울토마토 1박스',
        description: '싱싱한 방울토마토 한 박스입니다. 조금 작지만 달아요!',
        category_name: '과일/채소',
        quantity: '2',
        unit: 'kg',
        expiry_time: new Date(Date.now() + 48 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 용산구 이촌동',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1592924357228-91a4daadcfea?w=400'
      },
      {
        id: 7,
        title: '불고기 도시락',
        description: '푸짐한 불고기와 밥, 김치가 포함된 도시락입니다.',
        category_name: '도시락/식사',
        quantity: '5',
        unit: '개',
        expiry_time: new Date(Date.now() + 6 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 동대문구 회기역',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1563379091339-03b21ab4a4f8?w=400'
      },
      {
        id: 8,
        title: '생과일 주스',
        description: '직접 짠 신선한 과일 주스입니다. 오렌지, 사과, 당근 혼합',
        category_name: '음료/커피',
        quantity: '10',
        unit: '병',
        expiry_time: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 광진구 건대입구역',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1600271886742-f049cd451bba?w=400'
      },
      {
        id: 9,
        title: '마들렌 & 휘낭시에',
        description: '프랑스 전통 디저트입니다. 오늘 아침 구웠어요.',
        category_name: '빵/베이커리',
        quantity: '15',
        unit: '개',
        expiry_time: new Date(Date.now() + 10 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 성북구 성신여대입구역',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1558326567-98ae2405596b?w=400'
      },
      {
        id: 10,
        title: '혼합 과일 세트',
        description: '사과, 배, 감귤, 바나나가 포함된 과일 세트입니다.',
        category_name: '과일/채소',
        quantity: '3',
        unit: 'kg',
        expiry_time: new Date(Date.now() + 72 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 은평구 연신내역',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1619566636858-adf3ef46400b?w=400'
      },
      {
        id: 11,
        title: '치킨 샐러드 도시락',
        description: '닭가슴살과 신선한 채소가 가득한 샐러드입니다.',
        category_name: '도시락/식사',
        quantity: '4',
        unit: '개',
        expiry_time: new Date(Date.now() + 7 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 영등포구 여의도역',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1540189549336-e6e99c3679fe?w=400'
      },
      {
        id: 12,
        title: '녹차 & 허브티',
        description: '다양한 종류의 티백 모음입니다.',
        category_name: '음료/커피',
        quantity: '20',
        unit: '개',
        expiry_time: new Date(Date.now() + 168 * 60 * 60 * 1000).toISOString(),
        pickup_address: '서울시 종로구 광화문역',
        status: 'available',
        image_url: 'https://images.unsplash.com/photo-1556679343-c7306c1976bc?w=400'
      }
    ];
    
    // 검색 필터 적용
    let filteredData = demoData;
    
    if (category) {
      filteredData = filteredData.filter(d => {
        const categorySlugMap = {
          'bakery': '빵/베이커리',
          'produce': '과일/채소',
          'meals': '도시락/식사',
          'beverages': '음료/커피',
          'others': '기타'
        };
        return d.category_name === categorySlugMap[category];
      });
    }
    
    if (search) {
      filteredData = filteredData.filter(d => 
        d.title.includes(search) || d.description.includes(search)
      );
    }
    
    return res.json({
      donations: filteredData,
      pagination: {
        total: filteredData.length,
        page: parseInt(page),
        totalPages: Math.ceil(filteredData.length / limit)
      }
    });
  }
  
  let query = `
    SELECT d.*, c.name as category_name, u.name as donor_name
    FROM donations d
    LEFT JOIN categories c ON d.category_id = c.id
    LEFT JOIN users u ON d.donor_id = u.id
    WHERE d.status = 'available'
  `;
  
  const params = [];
  
  if (category) {
    query += ' AND c.slug = ?';
    params.push(category);
  }
  
  if (search) {
    query += ' AND (d.title LIKE ? OR d.description LIKE ?)';
    params.push(`%${search}%`, `%${search}%`);
  }
  
  if (sort === 'newest') {
    query += ' ORDER BY d.created_at DESC';
  } else if (sort === 'ending_soon') {
    query += ' ORDER BY d.expiry_time ASC';
  }
  
  const offset = (page - 1) * limit;
  query += ` LIMIT ? OFFSET ?`;
  params.push(parseInt(limit), offset);
  
  db.query(query, params, (err, results) => {
    if (err) {
      console.error('Donations query error:', err);
      return res.status(500).json({ error: '나눔 목록을 불러오는데 실패했습니다.' });
    }
    
    // 총 개수 조회
    let countQuery = 'SELECT COUNT(*) as total FROM donations d LEFT JOIN categories c ON d.category_id = c.id WHERE d.status = "available"';
    const countParams = [];
    
    if (category) {
      countQuery += ' AND c.slug = ?';
      countParams.push(category);
    }
    
    if (search) {
      countQuery += ' AND (d.title LIKE ? OR d.description LIKE ?)';
      countParams.push(`%${search}%`, `%${search}%`);
    }
    
    db.query(countQuery, countParams, (err, countResults) => {
      if (err) {
        return res.status(500).json({ error: '서버 오류가 발생했습니다.' });
      }
      
      const total = countResults[0].total;
      const totalPages = Math.ceil(total / limit);
      
      res.json({
        donations: results,
        pagination: {
          total: total,
          page: parseInt(page),
          totalPages: totalPages
        }
      });
    });
  });
});

// 나눔 상세 조회
app.get('/api/donations/:id', (req, res) => {
  const { id } = req.params;
  
  if (!dbConnected) {
    return res.json({
      id: 1,
      title: '신선한 크로와상 & 베이글',
      description: '오늘 아침에 구운 신선한 빵입니다.',
      category_name: '빵/베이커리',
      quantity: '10',
      unit: '개',
      expiry_time: new Date(Date.now() + 3 * 60 * 60 * 1000).toISOString(),
      pickup_address: '서울시 강남구 테헤란로 123',
      pickup_instructions: '1층 로비에서 받아가세요',
      status: 'available',
      images: []
    });
  }
  
  const query = `
    SELECT d.*, c.name as category_name, u.name as donor_name, u.phone as donor_phone
    FROM donations d
    LEFT JOIN categories c ON d.category_id = c.id
    LEFT JOIN users u ON d.donor_id = u.id
    WHERE d.id = ?
  `;
  
  db.query(query, [id], (err, results) => {
    if (err) {
      return res.status(500).json({ error: '나눔 정보를 불러오는데 실패했습니다.' });
    }
    
    if (results.length === 0) {
      return res.status(404).json({ error: '나눔을 찾을 수 없습니다.' });
    }
    
    res.json(results[0]);
  });
});

// ==========================================
//   API - 예약 (Reservations)
// ==========================================

// 예약 생성
app.post('/api/reservations', authenticateToken, (req, res) => {
  const { donationId, notes } = req.body;
  const userId = req.user.id;
  
  if (!donationId) {
    return res.status(400).json({ error: '나눔 ID가 필요합니다.' });
  }
  
  // 픽업 코드 생성
  const pickupCode = Math.random().toString(36).substring(2, 8).toUpperCase();
  
  if (!dbConnected) {
    return res.json({
      id: 1,
      donationId,
      pickupCode,
      status: 'pending',
      notes
    });
  }
  
  // 나눔 상태 확인
  db.query('SELECT * FROM donations WHERE id = ? AND status = "available"', [donationId], (err, results) => {
    if (err) {
      return res.status(500).json({ error: '서버 오류가 발생했습니다.' });
    }
    
    if (results.length === 0) {
      return res.status(400).json({ error: '예약할 수 없는 나눔입니다.' });
    }
    
    // 예약 생성
    const query = 'INSERT INTO reservations (donation_id, receiver_id, pickup_code, notes, status) VALUES (?, ?, ?, ?, "pending")';
    db.query(query, [donationId, userId, pickupCode, notes], (err, result) => {
      if (err) {
        console.error('Reservation error:', err);
        return res.status(500).json({ error: '예약에 실패했습니다.' });
      }
      
      // 나눔 상태 업데이트
      db.query('UPDATE donations SET status = "reserved" WHERE id = ?', [donationId], (err) => {
        if (err) {
          console.error('Donation update error:', err);
        }
      });
      
      res.json({
        id: result.insertId,
        donationId,
        pickupCode,
        status: 'pending',
        notes
      });
    });
  });
});

// ==========================================
//   API - 찜하기 (Favorites)
// ==========================================

// 찜하기 추가
app.post('/api/favorites', authenticateToken, (req, res) => {
  const { donationId } = req.body;
  const userId = req.user.id;
  
  if (!donationId) {
    return res.status(400).json({ error: '나눔 ID가 필요합니다.' });
  }
  
  if (!dbConnected) {
    return res.json({ success: true, message: '찜 목록에 추가되었습니다.' });
  }
  
  const query = 'INSERT INTO favorites (user_id, donation_id) VALUES (?, ?)';
  db.query(query, [userId, donationId], (err) => {
    if (err) {
      if (err.code === 'ER_DUP_ENTRY') {
        return res.status(400).json({ error: '이미 찜한 나눔입니다.' });
      }
      return res.status(500).json({ error: '찜하기에 실패했습니다.' });
    }
    
    res.json({ success: true, message: '찜 목록에 추가되었습니다.' });
  });
});

// 찜하기 제거
app.delete('/api/favorites/:donationId', authenticateToken, (req, res) => {
  const { donationId } = req.params;
  const userId = req.user.id;
  
  if (!dbConnected) {
    return res.json({ success: true, message: '찜 목록에서 제거되었습니다.' });
  }
  
  const query = 'DELETE FROM favorites WHERE user_id = ? AND donation_id = ?';
  db.query(query, [userId, donationId], (err) => {
    if (err) {
      return res.status(500).json({ error: '찜 제거에 실패했습니다.' });
    }
    
    res.json({ success: true, message: '찜 목록에서 제거되었습니다.' });
  });
});

// 내 찜 목록
app.get('/api/my/favorites', authenticateToken, (req, res) => {
  const userId = req.user.id;
  
  if (!dbConnected) {
    return res.json([]);
  }
  
  const query = `
    SELECT d.*, c.name as category_name, f.created_at as favorited_at
    FROM favorites f
    JOIN donations d ON f.donation_id = d.id
    LEFT JOIN categories c ON d.category_id = c.id
    WHERE f.user_id = ?
    ORDER BY f.created_at DESC
  `;
  
  db.query(query, [userId], (err, results) => {
    if (err) {
      return res.status(500).json({ error: '찜 목록을 불러오는데 실패했습니다.' });
    }
    
    res.json(results);
  });
});

// ==========================================
//   서버 시작
// ==========================================
app.listen(PORT, () => {
  console.log(`✅ FoodSave Server running on http://localhost:${PORT}`);
  console.log(`📊 Database: ${dbConnected ? 'Connected' : 'Demo Mode'}`);
  console.log(`🌍 Environment: ${process.env.NODE_ENV || 'development'}`);
});
