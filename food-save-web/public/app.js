// ==========================================
//   FoodSave - Frontend JavaScript
//   Modern UI/UX with Full CRUD Operations
// ==========================================

// Global State
const APP_STATE = {
    user: null,
    token: null,
    favorites: [],
    categories: [],
    currentPage: 1,
    itemsPerPage: 12
};

// API Base URL
const API_BASE = window.location.origin;

// ==========================================
//   Initialize App
// ==========================================
document.addEventListener('DOMContentLoaded', () => {
    console.log('🚀 FoodSave App Initialized');
    
    // Check authentication
    checkAuth();
    
    // Load initial data
    loadCategories();
    loadFeaturedDonations();

//수정시작
    initDashboard(); // 추가
    initProfile(); // 추가
//수정끝
    
    // Initialize animations
    initScrollAnimations();
    initCounterAnimation();
    
    // Set up event listeners
    setupEventListeners();
});

// ==========================================
//   Authentication Functions
// ==========================================

function checkAuth() {
    const token = localStorage.getItem('foodsave_token');
    const userStr = localStorage.getItem('foodsave_user');
    
    if (token && userStr) {
        try {
            APP_STATE.token = token;
            APP_STATE.user = JSON.parse(userStr);
            updateNavForLoggedInUser();
            loadUserFavorites();
        } catch (e) {
            console.error('Auth check error:', e);
            logout();
        }
    }
}

function updateNavForLoggedInUser() {
    const loginBtn = document.getElementById('loginBtn');
    const userMenu = document.getElementById('userMenu');
    const userName = document.getElementById('userName');
    const userAvatar = document.getElementById('userAvatar');
    
    if (APP_STATE.user) {
        loginBtn.style.display = 'none';
        userMenu.style.display = 'flex';
        userName.textContent = APP_STATE.user.name;
        
        if (APP_STATE.user.profileImage) {
            userAvatar.src = API_BASE + APP_STATE.user.profileImage;
        } else {
            userAvatar.src = `https://ui-avatars.com/api/?name=${encodeURIComponent(APP_STATE.user.name)}&background=8B4513&color=fff`;
        }
    } else {
        loginBtn.style.display = 'block';
        userMenu.style.display = 'none';
    }
}

async function handleLogin(event) {
    event.preventDefault();
    
    const form = event.target;
    const formData = new FormData(form);
    
    const credentials = {
        email: formData.get('email'),
        password: formData.get('password')
    };
    
    try {
        const response = await fetch(`${API_BASE}/api/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(credentials)
        });
        
        const data = await response.json();
        
        if (!response.ok) {
            throw new Error(data.error || '로그인에 실패했습니다.');
        }
        
        // Save auth data
        localStorage.setItem('foodsave_token', data.token);
        localStorage.setItem('foodsave_user', JSON.stringify(data.user));
        
        APP_STATE.token = data.token;
        APP_STATE.user = data.user;
        
        showToast('로그인 성공!', 'success');
        closeAuthModal();
        updateNavForLoggedInUser();
        loadUserFavorites();
        
        form.reset();

//수정시작
        checkLoginStatus();
//수정끝
        
    } catch (error) {
        console.error('Login error:', error);
        showToast(error.message, 'error');
    }
}

async function handleRegister(event) {
    event.preventDefault();
    
    const form = event.target;
    const formData = new FormData(form);
    
    const userData = {
        name: formData.get('name'),
        email: formData.get('email'),
        password: formData.get('password'),
        userType: formData.get('userType'),
        phone: formData.get('phone') || null,
        address: formData.get('address') || null
    };
    
    try {
        const response = await fetch(`${API_BASE}/api/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(userData)
        });
        
        const data = await response.json();
        
        if (!response.ok) {
            throw new Error(data.error || '회원가입에 실패했습니다.');
        }
        
        // Save auth data
        localStorage.setItem('foodsave_token', data.token);
        localStorage.setItem('foodsave_user', JSON.stringify(data.user));
        
        APP_STATE.token = data.token;
        APP_STATE.user = data.user;
        
        showToast('회원가입 완료! 환영합니다!', 'success');
        closeAuthModal();
        updateNavForLoggedInUser();
        
        form.reset();
        
    } catch (error) {
        console.error('Register error:', error);
        showToast(error.message, 'error');
    }
}

function logout() {
    localStorage.removeItem('foodsave_token');
    localStorage.removeItem('foodsave_user');
    
    APP_STATE.token = null;
    APP_STATE.user = null;
    APP_STATE.favorites = [];
    
    updateNavForLoggedInUser();
    showToast('로그아웃되었습니다.', 'info');
    
    // Redirect to home if on protected page
    if (window.location.pathname.includes('/dashboard') || 
        window.location.pathname.includes('/profile')) {
        window.location.href = '/';
    }
}

// ==========================================
//   Modal Functions
// ==========================================

function showAuthModal() {
    const modal = document.getElementById('authModal');
    modal.classList.add('active');
    document.body.style.overflow = 'hidden';
}

function closeAuthModal() {
    const modal = document.getElementById('authModal');
    modal.classList.remove('active');
    document.body.style.overflow = '';
}

function switchAuthTab(tab) {
    const loginForm = document.getElementById('loginForm');
    const registerForm = document.getElementById('registerForm');
    const tabs = document.querySelectorAll('.auth-tab');
    
    tabs.forEach(t => t.classList.remove('active'));
    
    if (tab === 'login') {
        loginForm.style.display = 'block';
        registerForm.style.display = 'none';
        tabs[0].classList.add('active');
    } else {
        loginForm.style.display = 'none';
        registerForm.style.display = 'block';
        tabs[1].classList.add('active');
    }
}

function showDonateModal() {
    if (!APP_STATE.user) {
        showToast('로그인이 필요합니다.', 'warning');
        showAuthModal();
        return;
    }
    
    if (APP_STATE.user.userType !== 'donor') {
        showToast('기부자 계정만 나눔을 등록할 수 있습니다.', 'warning');
        return;
    }
    
    // Redirect to dashboard create page
    window.location.href = '/dashboard?action=create';
}

// ==========================================
//   API Functions
// ==========================================

async function apiRequest(endpoint, options = {}) {
    const defaultOptions = {
        headers: {
            'Content-Type': 'application/json'
        }
    };
    
    if (APP_STATE.token) {
        defaultOptions.headers['Authorization'] = `Bearer ${APP_STATE.token}`;
    }
    
    const mergedOptions = {
        ...defaultOptions,
        ...options,
        headers: {
            ...defaultOptions.headers,
            ...options.headers
        }
    };
    
    try {
        const response = await fetch(`${API_BASE}${endpoint}`, mergedOptions);
        const data = await response.json();
        
        if (!response.ok) {
            // Check for auth errors
            if (response.status === 401 || response.status === 403) {
                logout();
                throw new Error('인증이 만료되었습니다. 다시 로그인해주세요.');
            }
            throw new Error(data.error || '요청에 실패했습니다.');
        }
        
        return data;
    } catch (error) {
        console.error('API Request Error:', error);
        throw error;
    }
}

// ==========================================
//   Categories
// ==========================================

async function loadCategories() {
    try {
        const categories = await apiRequest('/api/categories');
        APP_STATE.categories = categories;
    } catch (error) {
        console.error('Failed to load categories:', error);
    }
}

// ==========================================
//   Donations (Featured)
// ==========================================

async function loadFeaturedDonations() {
    const grid = document.getElementById('featuredGrid');
    
    if (!grid) return;
    
    // Show loading state
    grid.innerHTML = `
        <div class="col-span-full text-center py-8">
            <div class="spinner mx-auto"></div>
            <p class="mt-4 text-gray">로딩 중...</p>
        </div>
    `;
    
    try {
        const data = await apiRequest('/api/donations?limit=6&status=available');
        
        if (data.donations && data.donations.length > 0) {
            grid.innerHTML = data.donations.map(donation => createDonationCard(donation)).join('');
        } else {
            grid.innerHTML = `
                <div class="col-span-full text-center py-8">
                    <i class="fas fa-inbox text-6xl text-gray mb-4"></i>
                    <p class="text-lg text-gray">아직 등록된 나눔이 없습니다.</p>
                </div>
            `;
        }
    } catch (error) {
        console.error('Failed to load donations:', error);
        grid.innerHTML = `
            <div class="col-span-full text-center py-8">
                <i class="fas fa-exclamation-circle text-6xl text-danger mb-4"></i>
                <p class="text-lg text-gray">나눔 목록을 불러오는데 실패했습니다.</p>
            </div>
        `;
    }
}

function createDonationCard(donation) {
    const isUrgent = donation.expiry_time && 
        new Date(donation.expiry_time) - new Date() < 3 * 60 * 60 * 1000; // 3 hours
    
    const imageUrl = donation.image_url 
        ? (donation.image_url.startsWith('http') ? donation.image_url : API_BASE + donation.image_url)
        : 'https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=400';
    
    const timeLeft = donation.expiry_time 
        ? getTimeRemaining(donation.expiry_time)
        : '시간 제한 없음';
    
    return `
        <div class="donation-card" onclick="viewDonation(${donation.id})">
            <div class="donation-card-image">
                <img src="${imageUrl}" alt="${donation.title}" onerror="this.src='https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=400'">
                ${isUrgent ? '<div class="donation-badge urgent">긴급</div>' : '<div class="donation-badge">NEW</div>'}
            </div>
            <div class="donation-card-content">
                <div class="donation-category">
                    <i class="fas ${donation.category_icon || 'fa-utensils'}"></i>
                    ${donation.category_name || '기타'}
                </div>
                <h3 class="donation-title">${donation.title}</h3>
                <div class="donation-meta">
                    <span><i class="far fa-clock"></i> ${timeLeft}</span>
                    <span><i class="fas fa-map-marker-alt"></i> ${donation.pickup_address ? donation.pickup_address.split(' ').slice(0, 2).join(' ') : '위치 정보 없음'}</span>
                </div>
                <div class="donation-actions">
                    <button class="btn btn-primary btn-small" onclick="event.stopPropagation(); reserveDonation(${donation.id})">
                        <i class="fas fa-hand-holding-heart"></i> 예약하기
                    </button>
                    <button class="btn btn-secondary btn-small" onclick="event.stopPropagation(); toggleFavorite(${donation.id})">
                        <i class="far fa-heart"></i>
                    </button>
                </div>
            </div>
        </div>
    `;
}

function viewDonation(id) {
    window.location.href = `/donation/${id}`;
}

function getTimeRemaining(expiryTime) {
    const now = new Date();
    const expiry = new Date(expiryTime);
    const diff = expiry - now;
    
    if (diff <= 0) return '마감';
    
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    
    if (hours > 24) {
        const days = Math.floor(hours / 24);
        return `${days}일 남음`;
    } else if (hours > 0) {
        return `${hours}시간 남음`;
    } else {
        return `${minutes}분 남음`;
    }
}

// ==========================================
//   Favorites
// ==========================================

async function loadUserFavorites() {
    if (!APP_STATE.user) return;
    
    try {
        const favorites = await apiRequest('/api/my/favorites');
        APP_STATE.favorites = favorites.map(f => f.id);
        updateFavoriteCount();
    } catch (error) {
        console.error('Failed to load favorites:', error);
    }
}

async function toggleFavorite(donationId) {
    if (!APP_STATE.user) {
        showToast('로그인이 필요합니다.', 'warning');
        showAuthModal();
        return;
    }
    
    try {
        const isFavorited = APP_STATE.favorites.includes(donationId);
        
        if (isFavorited) {
            await apiRequest(`/api/favorites/${donationId}`, { method: 'DELETE' });
            APP_STATE.favorites = APP_STATE.favorites.filter(id => id !== donationId);
            showToast('찜 목록에서 제거되었습니다.', 'info');
        } else {
            await apiRequest('/api/favorites', {
                method: 'POST',
                body: JSON.stringify({ donationId })
            });
            APP_STATE.favorites.push(donationId);
            showToast('찜 목록에 추가되었습니다.', 'success');
        }
        
        updateFavoriteCount();
        
    } catch (error) {
        showToast(error.message, 'error');
    }
}

function updateFavoriteCount() {
    const favCount = document.getElementById('favCount');
    if (favCount) {
        favCount.textContent = APP_STATE.favorites.length;
        favCount.style.display = APP_STATE.favorites.length > 0 ? 'block' : 'none';
    }
}

function showFavorites() {
    if (!APP_STATE.user) {
        showToast('로그인이 필요합니다.', 'warning');
        showAuthModal();
        return;
    }
    
    window.location.href = '/dashboard?tab=favorites';
}

// ==========================================
//   Reservations
// ==========================================

async function reserveDonation(donationId) {
    if (!APP_STATE.user) {
        showToast('로그인이 필요합니다.', 'warning');
        showAuthModal();
        return;
    }
    
    if (APP_STATE.user.userType === 'donor') {
        showToast('기부자는 예약할 수 없습니다.', 'warning');
        return;
    }
    
    const notes = prompt('전달 사항이 있으시면 입력해주세요:');
    
    try {
        const result = await apiRequest('/api/reservations', {
            method: 'POST',
            body: JSON.stringify({ 
                donationId, 
                notes: notes || '' 
            })
        });
        
        showToast(`예약이 완료되었습니다! 픽업 코드: ${result.pickupCode}`, 'success');
        
        // Refresh the page after a delay
        setTimeout(() => {
            window.location.reload();
        }, 2000);
        
    } catch (error) {
        showToast(error.message, 'error');
    }
}

//수정시작
function initDashboard() {
    if (window.location.pathname !== '/dashboard') return;
    
    const user = getCurrentUser();
    if (!user) {
        window.location.href = '/';
        return;
    }
    
    // 실제 데이터 로드
    loadUserDashboardData();
}

function loadUserDashboardData() {
    const user = getCurrentUser();
    
    // localStorage에서 데이터 가져오기
    const allDonations = JSON.parse(localStorage.getItem('donations') || '[]');
    const userFavorites = JSON.parse(localStorage.getItem(`favorites_${user.id}`) || '[]');
    const userApplications = JSON.parse(localStorage.getItem(`applications_${user.id}`) || '[]');
    
    // 사용자의 나눔 필터링
    const userDonations = allDonations.filter(d => d.donorId === user.id);
    
    // 통계 업데이트
    const statsElements = {
        totalDonations: document.getElementById('totalDonations'),
        totalFavorites: document.getElementById('totalFavorites'),
        completedApplications: document.getElementById('completedApplications')
    };
    
    if (statsElements.totalDonations) {
        statsElements.totalDonations.textContent = userDonations.length;
    }
    if (statsElements.totalFavorites) {
        statsElements.totalFavorites.textContent = userFavorites.length;
    }
    if (statsElements.completedApplications) {
        const completed = userApplications.filter(a => a.status === 'completed').length;
        statsElements.completedApplications.textContent = completed;
    }
    
    // 리스트 렌더링
    renderDashboardLists(userDonations, userFavorites, userApplications);
}

function renderDashboardLists(donations, favorites, applications) {
    // 나눔 목록
    const donationsList = document.getElementById('donationsList');
    if (donationsList) {
        if (donations.length === 0) {
            donationsList.innerHTML = '<p class="empty-message">아직 등록한 나눔이 없습니다</p>';
        } else {
            donationsList.innerHTML = donations.map(d => `
                <div class="list-item">
                    <h4>${d.title}</h4>
                    <p>${d.category} · ${d.quantity}</p>
                    <span>${formatDate(d.createdAt)}</span>
                </div>
            `).join('');
        }
    }
    
    // 찜 목록
    const favoritesList = document.getElementById('favoritesList');
    if (favoritesList) {
        if (favorites.length === 0) {
            favoritesList.innerHTML = '<p class="empty-message">찜한 나눔이 없습니다</p>';
        } else {
            favoritesList.innerHTML = favorites.map(f => `
                <div class="list-item">
                    <h4>${f.title}</h4>
                    <p>${f.location}</p>
                </div>
            `).join('');
        }
    }
}

function initProfile() {
    if (window.location.pathname !== '/profile') return;
    
    const user = getCurrentUser();
    if (!user) {
        window.location.href = '/';
        return;
    }
    
    // 프로필 정보 표시
    displayProfileInfo(user);
    
    // 이벤트 리스너 설정
    setupProfileListeners();
}

function displayProfileInfo(user) {
    const elements = {
        userName: document.getElementById('profileName'),
        userEmail: document.getElementById('profileEmail'),
        userPhone: document.getElementById('profilePhone'),
        userAddress: document.getElementById('profileAddress'),
        userAvatar: document.getElementById('profileAvatar')
    };
    
    if (elements.userName) elements.userName.value = user.name || '';
    if (elements.userEmail) elements.userEmail.value = user.email || '';
    if (elements.userPhone) elements.userPhone.value = user.phone || '';
    if (elements.userAddress) elements.userAddress.value = user.address || '';
    if (elements.userAvatar && user.photo) {
        elements.userAvatar.src = user.photo;
    }
}

function setupProfileListeners() {
    // 프로필 사진 업로드
    const photoInput = document.getElementById('profilePhotoInput');
    if (photoInput) {
        photoInput.addEventListener('change', function(e) {
            const file = e.target.files[0];
            if (file && file.type.startsWith('image/')) {
                const reader = new FileReader();
                reader.onload = function(event) {
                    const avatar = document.getElementById('profileAvatar');
                    if (avatar) {
                        avatar.src = event.target.result;
                        // 사용자 정보 업데이트
                        const user = getCurrentUser();
                        user.photo = event.target.result;
                        updateUserInfo(user);
                        showToast('프로필 사진이 업데이트되었습니다', 'success');
                    }
                };
                reader.readAsDataURL(file);
            }
        });
    }
    
    // 프로필 저장 버튼
    const saveBtn = document.getElementById('saveProfileBtn');
    if (saveBtn) {
        saveBtn.addEventListener('click', saveProfile);
    }
}

function saveProfile() {
    const user = getCurrentUser();
    
    user.name = document.getElementById('profileName')?.value || user.name;
    user.email = document.getElementById('profileEmail')?.value || user.email;
    user.phone = document.getElementById('profilePhone')?.value || user.phone;
    user.address = document.getElementById('profileAddress')?.value || user.address;
    
    updateUserInfo(user);
    showToast('프로필이 저장되었습니다', 'success');
}

function updateUserInfo(user) {
    // currentUser 업데이트
    localStorage.setItem('currentUser', JSON.stringify(user));
    
    // users 배열 업데이트
    const users = JSON.parse(localStorage.getItem('users') || '[]');
    const index = users.findIndex(u => u.id === user.id);
    if (index > -1) {
        users[index] = user;
        localStorage.setItem('users', JSON.stringify(users));
    }
}

// ==========================================
//   Search
// ==========================================

function toggleSearch() {
    const searchBar = document.getElementById('searchBar');
    const searchInput = document.getElementById('searchInput');
    
    searchBar.classList.toggle('active');
    
    if (searchBar.classList.contains('active')) {
        setTimeout(() => searchInput.focus(), 300);
    }
}

function performSearch() {
    const searchInput = document.getElementById('searchInput');
    const query = searchInput.value.trim();
    
    if (query) {
        window.location.href = `/donations?search=${encodeURIComponent(query)}`;
    }
}

// Handle Enter key in search
document.addEventListener('keydown', (e) => {
    const searchInput = document.getElementById('searchInput');
    if (e.key === 'Enter' && document.activeElement === searchInput) {
        performSearch();
    }
});

// ==========================================
//   Mobile Menu
// ==========================================

function toggleMobileMenu() {
    const navMenu = document.querySelector('.nav-menu');
    const mobileBtn = document.querySelector('.mobile-menu-btn');
    
    navMenu.classList.toggle('active');
    mobileBtn.classList.toggle('active');
}

// ==========================================
//   Newsletter
// ==========================================

async function subscribeNewsletter(event) {
    event.preventDefault();
    
    const form = event.target;
    const email = form.querySelector('input[type="email"]').value;
    
    // Simulate API call
    await new Promise(resolve => setTimeout(resolve, 1000));
    
    showToast('뉴스레터 구독이 완료되었습니다!', 'success');
    form.reset();
}

// ==========================================
//   Animations
// ==========================================

function initScrollAnimations() {
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -100px 0px'
    };
    
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
            }
        });
    }, observerOptions);
    
    const elements = document.querySelectorAll('.donation-card, .process-step, .stat-card');
    elements.forEach(el => {
        el.style.opacity = '0';
        el.style.transform = 'translateY(30px)';
        el.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
        observer.observe(el);
    });
}

function initCounterAnimation() {
    const counters = document.querySelectorAll('.stat-number[data-target]');
    
    const observerOptions = {
        threshold: 0.5
    };
    
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const counter = entry.target;
                const target = parseInt(counter.getAttribute('data-target'));
                animateCounter(counter, target);
                observer.unobserve(counter);
            }
        });
    }, observerOptions);
    
    counters.forEach(counter => observer.observe(counter));
}

function animateCounter(element, target) {
    const duration = 2000;
    const start = 0;
    const startTime = performance.now();
    
    function update(currentTime) {
        const elapsed = currentTime - startTime;
        const progress = Math.min(elapsed / duration, 1);
        
        const current = Math.floor(progress * target);
        element.textContent = current.toLocaleString();
        
        if (progress < 1) {
            requestAnimationFrame(update);
        } else {
            element.textContent = target.toLocaleString();
        }
    }
    
    requestAnimationFrame(update);
}

// ==========================================
//   Toast Notifications
// ==========================================

function showToast(message, type = 'info') {
    const toast = document.getElementById('toast');
    
    // Set message and type
    toast.textContent = message;
    toast.className = `toast ${type}`;
    
    // Show toast
    toast.classList.add('show');
    
    // Hide after 3 seconds
    setTimeout(() => {
        toast.classList.remove('show');
    }, 3000);
}

// ==========================================
//   Event Listeners Setup
// ==========================================

function setupEventListeners() {
    // Close modal on overlay click
    document.addEventListener('click', (e) => {
        if (e.target.classList.contains('modal-overlay')) {
            closeAuthModal();
        }
    });
    
    // Close modal on Escape key
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeAuthModal();
        }
    });
    
    // Prevent modal close when clicking inside modal content
    const modalContent = document.querySelector('.modal-content');
    if (modalContent) {
        modalContent.addEventListener('click', (e) => {
            e.stopPropagation();
        });
    }
}

// ==========================================
//   Utility Functions
// ==========================================

function formatDate(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diff = now - date;
    
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);
    const days = Math.floor(diff / 86400000);
    
    if (minutes < 1) return '방금 전';
    if (minutes < 60) return `${minutes}분 전`;
    if (hours < 24) return `${hours}시간 전`;
    if (days < 7) return `${days}일 전`;
    
    return date.toLocaleDateString('ko-KR', {
        year: 'numeric',
        month: 'long',
        day: 'numeric'
    });
}

function truncateText(text, maxLength) {
    if (text.length <= maxLength) return text;
    return text.substring(0, maxLength) + '...';
}

// ==========================================
//   Export Functions for Global Access
// ==========================================

window.FoodSave = {
    showAuthModal,
    closeAuthModal,
    switchAuthTab,
    handleLogin,
    handleRegister,
    logout,
    showDonateModal,
    toggleSearch,
    performSearch,
    toggleMobileMenu,
    subscribeNewsletter,
    viewDonation,
    reserveDonation,
    toggleFavorite,
    showFavorites,
    showToast
};

// Make functions globally accessible
window.showAuthModal = showAuthModal;
window.closeAuthModal = closeAuthModal;
window.switchAuthTab = switchAuthTab;
window.handleLogin = handleLogin;
window.handleRegister = handleRegister;
window.logout = logout;
window.showDonateModal = showDonateModal;
window.toggleSearch = toggleSearch;
window.performSearch = performSearch;
window.toggleMobileMenu = toggleMobileMenu;
window.subscribeNewsletter = subscribeNewsletter;
window.viewDonation = viewDonation;
window.reserveDonation = reserveDonation;
window.toggleFavorite = toggleFavorite;
window.showFavorites = showFavorites;

console.log('✅ FoodSave App Ready');

//수정시작
function checkLoginStatus() {
    const user = getCurrentUser();
    const loginBtn = document.getElementById('loginBtn');
    const userMenu = document.getElementById('userMenu');
    if (user && user.isLoggedIn) {
        if (loginBtn) loginBtn.style.display = 'none';
        if (userMenu) {
            userMenu.style.display = 'flex';
            const userName = document.getElementById('userName');
            const userAvatar = document.getElementById('userAvatar');
            if (userName) userName.textContent = user.name || 'User';
            if (userAvatar) {
                userAvatar.src = user.photo || 'data:image/svg+xml,...';
t           }
        }
    } else {
        if (loginBtn) loginBtn.style.display = 'block';
        if (userMenu) userMenu.style.display = 'none';
    }
}
document.addEventListener('DOMContentLoaded', checkLoginStatus);
//수정
