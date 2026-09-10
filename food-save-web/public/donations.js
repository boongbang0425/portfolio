// ==========================================
//   Donations Page Script
// ==========================================

const DONATIONS_STATE = {
    donations: [],
    currentPage: 1,
    itemsPerPage: 12,
    totalItems: 0,
    totalPages: 0,
    filters: {
        category: '',
        search: '',
        sort: 'newest'
    },
    viewMode: 'grid'
};

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    loadCategoriesForFilter();
    parseURLParams();
    loadDonations();
});

// Parse URL parameters
function parseURLParams() {
    const params = new URLSearchParams(window.location.search);
    
    if (params.has('search')) {
        DONATIONS_STATE.filters.search = params.get('search');
        document.getElementById('searchInput').value = params.get('search');
    }
    
    if (params.has('category')) {
        DONATIONS_STATE.filters.category = params.get('category');
    }
    
    if (params.has('page')) {
        DONATIONS_STATE.currentPage = parseInt(params.get('page')) || 1;
    }
}

// Load categories for filter dropdown
async function loadCategoriesForFilter() {
    try {
        const response = await fetch('/api/categories');
        const categories = await response.json();
        
        const select = document.getElementById('categoryFilter');
        categories.forEach(cat => {
            const option = document.createElement('option');
            option.value = cat.slug;
            option.textContent = cat.name;
            if (cat.slug === DONATIONS_STATE.filters.category) {
                option.selected = true;
            }
            select.appendChild(option);
        });
    } catch (error) {
        console.error('Failed to load categories:', error);
    }
}
// Load donations with filters
async function loadDonations() {
    const grid = document.getElementById('donationsGrid');
    
    // Show loading
    grid.innerHTML = `
        <div class="col-span-full text-center py-12">
            <div class="spinner mx-auto"></div>
            <p class="mt-4 text-gray">나눔 목록을 불러오는 중...</p>
        </div>
    `;
    
    try {
        const queryParams = new URLSearchParams({
            page: DONATIONS_STATE.currentPage,
            limit: DONATIONS_STATE.itemsPerPage,
            status: 'available'
        });
        
        if (DONATIONS_STATE.filters.category) {
            queryParams.append('category', DONATIONS_STATE.filters.category);
        }
        
        if (DONATIONS_STATE.filters.search) {
            queryParams.append('search', DONATIONS_STATE.filters.search);
        }
        
        const response = await fetch(`/api/donations?${queryParams}`);
        const data = await response.json();
        
        DONATIONS_STATE.donations = data.donations;
        DONATIONS_STATE.totalItems = data.pagination.total;
        DONATIONS_STATE.totalPages = data.pagination.totalPages;
        
        renderDonations();
        renderPagination();
        updateDonationsCount();
        
    } catch (error) {
        console.error('Failed to load donations:', error);
        grid.innerHTML = `
            <div class="col-span-full text-center py-12">
                <i class="fas fa-exclamation-circle text-6xl text-danger mb-4"></i>
                <p class="text-lg text-gray mb-4">나눔 목록을 불러오는데 실패했습니다.</p>
                <button class="btn btn-primary" onclick="loadDonations()">
                    <i class="fas fa-redo"></i> 다시 시도
                </button>
            </div>
        `;
    }
}

// Render donations
function renderDonations() {
    const grid = document.getElementById('donationsGrid');
    
    // Apply sort
    let sortedDonations = [...DONATIONS_STATE.donations];
    
    switch (DONATIONS_STATE.filters.sort) {
        case 'oldest':
            sortedDonations.sort((a, b) => new Date(a.created_at) - new Date(b.created_at));
            break;
        case 'expiring':
            sortedDonations.sort((a, b) => {
                if (!a.expiry_time) return 1;
                if (!b.expiry_time) return -1;
                return new Date(a.expiry_time) - new Date(b.expiry_time);
            });
            break;
        case 'popular':
            sortedDonations.sort((a, b) => (b.views || 0) - (a.views || 0));
            break;
        default: // newest
            sortedDonations.sort((a, b) => new Date(b.created_at) - new Date(a.created_at));
    }
    
    if (sortedDonations.length === 0) {
        grid.innerHTML = `
            <div class="col-span-full text-center py-12">
                <i class="fas fa-inbox text-6xl text-gray mb-4"></i>
                <p class="text-lg text-gray mb-2">검색 결과가 없습니다.</p>
                <p class="text-sm text-medium-gray mb-4">다른 검색어나 필터를 시도해보세요.</p>
                <button class="btn btn-outline" onclick="resetFilters()">
                    <i class="fas fa-redo"></i> 필터 초기화
                </button>
            </div>
        `;
        return;
    }
    
    if (DONATIONS_STATE.viewMode === 'grid') {
        grid.className = 'donations-grid';
        grid.innerHTML = sortedDonations.map(donation => createDonationCard(donation)).join('');
    } else {
        grid.className = 'donations-list';
        grid.innerHTML = sortedDonations.map(donation => createDonationListItem(donation)).join('');
    }
}
// Create donation card (grid view)
function createDonationCard(donation) {
    const isUrgent = donation.expiry_time && 
        new Date(donation.expiry_time) - new Date() < 3 * 60 * 60 * 1000;
    
    const imageUrl = donation.image_url 
        ? (donation.image_url.startsWith('http') ? donation.image_url : window.location.origin + donation.image_url)
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
                <p class="donation-description">${truncateText(donation.description || '', 80)}</p>
                <div class="donation-meta">
                    <span><i class="far fa-clock"></i> ${timeLeft}</span>
                    <span><i class="fas fa-eye"></i> ${donation.views || 0}</span>
                </div>
                <div class="donation-location">
                    <i class="fas fa-map-marker-alt"></i>
                    ${donation.pickup_address ? donation.pickup_address.split(' ').slice(0, 2).join(' ') : '위치 정보 없음'}
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

// Create donation list item (list view)
function createDonationListItem(donation) {
    const isUrgent = donation.expiry_time && 
        new Date(donation.expiry_time) - new Date() < 3 * 60 * 60 * 1000;
    
    const imageUrl = donation.image_url 
        ? (donation.image_url.startsWith('http') ? donation.image_url : window.location.origin + donation.image_url)
        : 'https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=400';
    
    const timeLeft = donation.expiry_time 
        ? getTimeRemaining(donation.expiry_time)
        : '시간 제한 없음';
    
    return `
        <div class="donation-list-item" onclick="viewDonation(${donation.id})">
            <div class="list-item-image">
                <img src="${imageUrl}" alt="${donation.title}" onerror="this.src='https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=400'">
                ${isUrgent ? '<span class="urgent-badge">긴급</span>' : ''}
            </div>
            <div class="list-item-content">
                <div class="list-item-header">
                    <h3>${donation.title}</h3>
                    <span class="category-tag">
                        <i class="fas ${donation.category_icon || 'fa-utensils'}"></i>
                        ${donation.category_name || '기타'}
                    </span>
                </div>
                <p class="list-item-description">${truncateText(donation.description || '', 150)}</p>
                <div class="list-item-meta">
                    <span><i class="far fa-clock"></i> ${timeLeft}</span>
                    <span><i class="fas fa-map-marker-alt"></i> ${donation.pickup_address ? donation.pickup_address.split(' ').slice(0, 2).join(' ') : '위치'}</span>
                    <span><i class="fas fa-eye"></i> ${donation.views || 0} 조회</span>
                    <span><i class="fas fa-box"></i> ${donation.quantity || '수량 미정'}</span>
                </div>
            </div>
            <div class="list-item-actions">
                <button class="btn btn-primary" onclick="event.stopPropagation(); reserveDonation(${donation.id})">
                    <i class="fas fa-hand-holding-heart"></i> 예약하기
                </button>
                <button class="btn btn-outline" onclick="event.stopPropagation(); toggleFavorite(${donation.id})">
                    <i class="far fa-heart"></i> 찜하기
                </button>
            </div>
        </div>
    `;
}
// Render pagination
function renderPagination() {
    const pagination = document.getElementById('pagination');
    
    if (DONATIONS_STATE.totalPages <= 1) {
        pagination.style.display = 'none';
        return;
    }
    
    pagination.style.display = 'flex';
    
    let html = '';
    
    // Previous button
    if (DONATIONS_STATE.currentPage > 1) {
        html += `
            <button class="pagination-btn" onclick="goToPage(${DONATIONS_STATE.currentPage - 1})">
                <i class="fas fa-chevron-left"></i> 이전
            </button>
        `;
    }
    
    // Page numbers
    const maxButtons = 5;
    let startPage = Math.max(1, DONATIONS_STATE.currentPage - Math.floor(maxButtons / 2));
    let endPage = Math.min(DONATIONS_STATE.totalPages, startPage + maxButtons - 1);
    
    if (endPage - startPage < maxButtons - 1) {
        startPage = Math.max(1, endPage - maxButtons + 1);
    }
    
    if (startPage > 1) {
        html += `<button class="pagination-btn" onclick="goToPage(1)">1</button>`;
        if (startPage > 2) {
            html += `<span class="pagination-dots">...</span>`;
        }
    }
    
    for (let i = startPage; i <= endPage; i++) {
        html += `
            <button class="pagination-btn ${i === DONATIONS_STATE.currentPage ? 'active' : ''}" 
                    onclick="goToPage(${i})">
                ${i}
            </button>
        `;
    }
    
    if (endPage < DONATIONS_STATE.totalPages) {
        if (endPage < DONATIONS_STATE.totalPages - 1) {
            html += `<span class="pagination-dots">...</span>`;
        }
        html += `<button class="pagination-btn" onclick="goToPage(${DONATIONS_STATE.totalPages})">${DONATIONS_STATE.totalPages}</button>`;
    }
    
    // Next button
    if (DONATIONS_STATE.currentPage < DONATIONS_STATE.totalPages) {
        html += `
            <button class="pagination-btn" onclick="goToPage(${DONATIONS_STATE.currentPage + 1})">
                다음 <i class="fas fa-chevron-right"></i>
            </button>
        `;
    }
    
    pagination.innerHTML = html;
}

// Go to page
function goToPage(page) {
    DONATIONS_STATE.currentPage = page;
    window.scrollTo({ top: 0, behavior: 'smooth' });
    loadDonations();
    updateURL();
}

// Update donations count
function updateDonationsCount() {
    const countElement = document.getElementById('donationsCount');
    countElement.textContent = `나눔 목록 (${DONATIONS_STATE.totalItems}개)`;
}

// Apply filters
function applyFilters() {
    DONATIONS_STATE.filters.category = document.getElementById('categoryFilter').value;
    DONATIONS_STATE.filters.sort = document.getElementById('sortFilter').value;
    DONATIONS_STATE.currentPage = 1;
    
    loadDonations();
    updateURL();
}

// Reset filters
function resetFilters() {
    DONATIONS_STATE.filters = {
        category: '',
        search: '',
        sort: 'newest'
    };
    DONATIONS_STATE.currentPage = 1;
    
    document.getElementById('categoryFilter').value = '';
    document.getElementById('sortFilter').value = 'newest';
    document.getElementById('searchInput').value = '';
    
    loadDonations();
    window.history.pushState({}, '', '/donations');
}

// Update URL with current filters
function updateURL() {
    const params = new URLSearchParams();
    
    if (DONATIONS_STATE.filters.category) {
        params.append('category', DONATIONS_STATE.filters.category);
    }
    
    if (DONATIONS_STATE.filters.search) {
        params.append('search', DONATIONS_STATE.filters.search);
    }
    
    if (DONATIONS_STATE.currentPage > 1) {
        params.append('page', DONATIONS_STATE.currentPage);
    }
    
    const url = params.toString() ? `/donations?${params}` : '/donations';
    window.history.pushState({}, '', url);
}

// Set view mode
function setView(mode) {
    DONATIONS_STATE.viewMode = mode;
    
    const viewBtns = document.querySelectorAll('.view-btn');
    viewBtns.forEach(btn => btn.classList.remove('active'));
    event.target.closest('.view-btn').classList.add('active');
    
    renderDonations();
}

// Override search function for this page
window.performSearch = function() {
    const searchInput = document.getElementById('searchInput');
    DONATIONS_STATE.filters.search = searchInput.value.trim();
    DONATIONS_STATE.currentPage = 1;
    loadDonations();
    updateURL();
};

// Utility: Get time remaining
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
        return `${hours}시간 ${minutes}분 남음`;
    } else {
        return `${minutes}분 남음`;
    }
}

// Utility: Truncate text
function truncateText(text, maxLength) {
    if (text.length <= maxLength) return text;
    return text.substring(0, maxLength) + '...';
}

console.log('✅ Donations Page Script Loaded');

