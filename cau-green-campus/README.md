# 🌍 CAU Green Campus Action

> AI 기반 탄소발자국 분석 및 캠퍼스 실천 가이드 웹 플랫폼

![License](https://img.shields.io/badge/license-MIT-green)
![HTML5](https://img.shields.io/badge/HTML5-E34F26?logo=html5&logoColor=white)
![CSS3](https://img.shields.io/badge/CSS3-1572B6?logo=css3&logoColor=white)
![JavaScript](https://img.shields.io/badge/JavaScript-F7DF1E?logo=javascript&logoColor=black)

## 📌 프로젝트 개요

**CAU Green Campus Action**은 2025 생성형 AI 모델 활용 경진대회 2부 출품작으로, 기후위기라는 사회 문제와 중앙대학교의 기후 대응 1위라는 강점을 연결하여 재학생들의 탄소 절감 실천을 유도하는 인터랙티브 웹 플랫폼입니다.

### 🎯 핵심 목표

- **인식 전환**: 기후위기를 개인의 일상과 직결된 구체적 수치로 체감
- **행동 유도**: AI 기반 개인화 분석을 통한 맞춤형 탄소 절감 방법 제시
- **자원 연결**: 중앙대학교 친환경 시설 및 프로그램과 학생 연결

## 🌐 웹사이트 구조

```
cau-green-campus/
├── index.html          # 메인 랜딩 페이지
├── calculator.html     # AI 탄소발자국 계산기
├── action.html         # 캠퍼스 실천 가이드
└── README.md           # 프로젝트 설명서
```

### 페이지별 기능

| 페이지 | 핵심 기능 | 목적 |
|:---|:---|:---|
| **index.html** | 기후위기 현황 데이터 시각화 + 중앙대 1위 달성 분석 | 문제 인식 및 동기 부여 |
| **calculator.html** | AI 기반 개인 탄소발자국 계산기 + 맞춤 실천 가이드 | 자가 진단 및 개인화 솔루션 |
| **action.html** | 캠퍼스 친환경 시설 안내 + 프로그램 소개 + 실천 체크리스트 | 즉각적 행동 연결 |

## 🚀 실행 방법

### 로컬 실행

1. 저장소를 클론합니다:
```bash
git clone https://github.com/boongbang0425/CAU.git
```

2. 프로젝트 폴더로 이동합니다:
```bash
cd CAU
```

3. 의존성을 설치합니다:
```bash
npm install
```

4. 서버를 시작합니다:
```bash
npm start
```

5. 브라우저에서 `http://localhost:3000` 에 접속합니다.

### Cloudtype 배포

#### CLI 배포
```bash
ctype apply -f cau.yaml
```

#### GitHub Actions 자동 배포
1. GitHub 저장소 Settings > Secrets에서 다음 시크릿 추가:
   - `CLOUDTYPE_TOKEN`: Cloudtype API 토큰
   - `GHP_TOKEN`: GitHub Personal Access Token
2. `main` 브랜치에 push하면 자동 배포됩니다.

### 배포 URL
- https://port-0-cau-mi0mrp7146847b27.sel3.cloudtype.app

## 🎨 디자인 시스템

### 컬러 팔레트

| 역할 | 색상 | HEX |
|:---|:---|:---|
| Primary | Emerald | `#10B981` |
| Secondary | Sky | `#0EA5E9` |
| Accent | CAU Red | `#C41E3A` |
| Background | Snow | `#F8FAFC` |
| Text | Slate | `#1E293B` |

### 타이포그래피

- **폰트**: Pretendard
- **반응형**: Mobile (~ 768px) / Tablet (769px ~ 1024px) / Desktop (1025px ~)

## 🤖 AI 활용 방법

### 1. 기후위기 현황 분석
- 한국의 기후 데이터를 대학생 관점에서 해석
- 복잡한 통계를 직관적인 스토리텔링으로 변환

### 2. 탄소발자국 계산 및 분석
- 사용자 입력(통학, 식습관, 에너지, 소비) 기반 월간 CO₂ 배출량 산출
- 전국 대학생 평균과 비교 분석
- 개인 상황에 최적화된 3가지 절감 전략 제시

### 3. 중앙대 1위 요인 분석
- 학술 기반, 캠퍼스 인프라, 학생 참여, 제도적 지원의 4가지 축으로 분석

## 📊 심사 기준 충족

| 평가 항목 | 충족 내용 |
|:---|:---|
| **적합성** (25%) | 사회문제(기후위기) + 대학특징(1위) + 대안(측정→실천) 완벽 연결 |
| **창의성** (25%) | AI 기반 개인화 분석, 정량적 접근, 인터랙티브 경험 |
| **효과성** (25%) | 즉시 사용 가능한 계산기, 캠퍼스 자원 직접 연결 |
| **완성도** (25%) | 전문 UI/UX 디자인, 완벽한 반응형, 즉시 배포 가능 |

## 🛠️ 기술 스택

- **HTML5**: 시맨틱 마크업
- **CSS3**: Flexbox, Grid, 애니메이션
- **JavaScript**: 바닐라 JS, Chart.js
- **Font**: Pretendard (CDN)

## 📱 반응형 지원

- ✅ Desktop (1280px+)
- ✅ Laptop (1024px)
- ✅ Tablet (768px)
- ✅ Mobile (320px+)

## 👥 팀 정보

- **대회**: 2025 생성형 AI 모델 활용 경진대회 2부
- **주최**: 중앙대학교 교육혁신원
- **주제**: 기후 위기와 탄소중립

## 📄 라이선스

This project is licensed under the MIT License.

---

<p align="center">
  <strong>🌍 CAU Green Campus Action</strong><br>
  변화는 측정에서 시작됩니다
</p>
