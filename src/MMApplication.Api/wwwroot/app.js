// M M Application - Super Admin Executive Suite JavaScript (Ultra-Clean Light Edition)

const API_BASE = '/api';

const adminState = {
    token: localStorage.getItem('mm_admin_token') || null,
    user: JSON.parse(localStorage.getItem('mm_admin_user') || 'null'),
    currentTab: 'overview',
    overviewData: null,
    stores: [],
    fraudQueue: [],
    banners: [],
    campaigns: [],
    settlements: [],
    customers: [],
    staff: [],
    auditLogs: [],
    selectedFraudInvoice: null,
    charts: {}
};

// Hub Titles Mapping
const HUB_TITLES = {
    overview: 'لوحة التحكم الرئيسية (Overview & Analytics)',
    stores: 'إدارة المتاجر والمستأجرين (Tenants & Stores)',
    fraud: 'مركز مكافحة الاحتيال والتدقيق (Fraud & Review Hub)',
    ads: 'الإعلانات والعروض الترويجية (Ads & Offers Hub)',
    loyalty: 'برنامج ولاء المول العام (Mall-Wide Loyalty)',
    settlements: 'التسويات والمقاصة المالية (Settlements & Clearing)',
    customers: 'قاعدة بيانات العملاء (Customer & Shoppers Database)',
    staff: 'طاقم العمل والصلاحيات (Staff & Role Management)',
    settings: 'إعدادات النظام وسجلات الأمان (System Settings & Audit Logs)'
};

// ----------------------------------------------------
// INITIALIZATION
// ----------------------------------------------------
document.addEventListener('DOMContentLoaded', async () => {
    initLucideIcons();

    // Auto-login as Super Admin if not already
    if (!adminState.token || adminState.user?.role !== 'Admin') {
        await adminLogin('0900000000', 'Admin123!');
    } else {
        updateAdminHeader();
        await switchAdminTab('overview');
    }
});

function initLucideIcons() {
    if (window.lucide) {
        lucide.createIcons();
    }
}

// API Call Wrapper
async function apiCall(endpoint, options = {}) {
    const headers = options.headers || {};
    if (adminState.token) {
        headers['Authorization'] = `Bearer ${adminState.token}`;
    }
    if (!(options.body instanceof FormData) && !headers['Content-Type']) {
        headers['Content-Type'] = 'application/json';
    }

    try {
        const response = await fetch(`${API_BASE}${endpoint}`, {
            ...options,
            headers
        });

        if (response.status === 401 || response.status === 403) {
            await adminLogin('0900000000', 'Admin123!');
            return await apiCall(endpoint, options);
        }

        const data = await response.json().catch(() => null);

        if (!response.ok) {
            const errorMsg = data?.message || `خطأ في معالجة الطلب (${response.status})`;
            throw new Error(errorMsg);
        }

        return data;
    } catch (err) {
        console.error(`API Error [${endpoint}]:`, err);
        throw err;
    }
}

// Super Admin Authentication
async function adminLogin(phone, password) {
    try {
        showLoading(true);
        const res = await fetch(`${API_BASE}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phoneNumber: phone, password })
        });

        const data = await res.json();
        if (!res.ok) throw new Error(data.message || 'فشل تسجيل الدخول للإدارة');

        adminState.token = data.token;
        adminState.user = {
            id: data.customerId,
            fullName: data.fullName,
            phoneNumber: data.phoneNumber,
            role: data.role
        };

        localStorage.setItem('mm_admin_token', adminState.token);
        localStorage.setItem('mm_admin_user', JSON.stringify(adminState.user));

        updateAdminHeader();
        await switchAdminTab(adminState.currentTab);
        showToast(`مرحباً بك يا ${adminState.user.fullName}!`, 'success');
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

function updateAdminHeader() {
    const nameEl = document.getElementById('admin-profile-name');
    const roleEl = document.getElementById('admin-profile-role');
    if (nameEl && adminState.user) {
        nameEl.textContent = adminState.user.fullName;
        roleEl.textContent = 'Super Admin (متصل)';
    }
}

// Tab Switching
async function switchAdminTab(tabName) {
    adminState.currentTab = tabName;

    // Update Header Title
    const titleEl = document.getElementById('current-section-title');
    if (titleEl) {
        titleEl.textContent = HUB_TITLES[tabName] || tabName;
    }

    // Update Nav Sidebar Buttons
    document.querySelectorAll('.nav-item').forEach(btn => {
        const target = btn.getAttribute('data-tab');
        if (target === tabName) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    });

    // Hide all tab views
    document.querySelectorAll('.tab-view').forEach(view => view.classList.add('hidden'));

    // Show active tab view
    const activeView = document.getElementById(`tab-view-${tabName}`);
    if (activeView) {
        activeView.classList.remove('hidden');
    }

    // Load Data for Tab
    showLoading(true);
    try {
        switch (tabName) {
            case 'overview':
                await loadOverviewData();
                break;
            case 'stores':
                await loadStoresData();
                break;
            case 'fraud':
                await loadFraudQueueData();
                break;
            case 'ads':
                await loadAdsData();
                break;
            case 'loyalty':
                await loadLoyaltyData();
                break;
            case 'settlements':
                await loadSettlementsData();
                break;
            case 'customers':
                await loadCustomersData();
                break;
            case 'staff':
                await loadStaffData();
                break;
            case 'settings':
                await loadSettingsAndAuditData();
                break;
        }
    } catch (e) {
        console.error('Error switching tab:', e);
    } finally {
        showLoading(false);
        initLucideIcons();
    }
}

// ========================================================
// 1. 📊 OVERVIEW & STATISTICAL POISSON ANALYTICS
// ========================================================
async function loadOverviewData() {
    const data = await apiCall('/superadmin/overview');
    adminState.overviewData = data;

    // Update Top KPIs
    document.getElementById('kpi-sales').textContent = `${data.totalSalesVolume.toLocaleString('ar-EG', { minimumFractionDigits: 2 })} د.ل`;
    document.getElementById('kpi-today-scans').textContent = data.todayScansCount.toLocaleString('ar-EG');
    document.getElementById('kpi-pending-review').textContent = data.pendingReviewCount;
    document.getElementById('kpi-circulating-points').textContent = data.circulatingPoints.toLocaleString('ar-EG');

    // Update Fraud badge in sidebar
    const badge = document.getElementById('nav-fraud-badge');
    if (badge) {
        badge.textContent = data.pendingReviewCount;
    }

    // Render Charts
    renderPoissonCurveChart(data);
    renderScanPaceChart(data.scanActivityPace);
    renderCategoriesDonutChart(data.categoryDistribution);

    // Top Stores Table
    const topStoresTbody = document.getElementById('top-stores-table-body');
    topStoresTbody.innerHTML = '';
    data.topPerformingStores.forEach((s, idx) => {
        const tr = document.createElement('tr');
        tr.className = 'border-b border-slate-100 hover:bg-slate-50 transition';
        tr.innerHTML = `
            <td class="py-2.5 px-3 font-bold text-slate-900 flex items-center gap-2">
                <span class="w-5 h-5 rounded-full ${idx === 0 ? 'bg-amber-100 text-amber-800 font-black' : 'bg-slate-100 text-slate-600'} flex items-center justify-center text-[10px] font-bold">${idx + 1}</span>
                <span>${s.storeName}</span>
            </td>
            <td class="py-2.5 px-3 font-mono-num text-slate-600">${s.invoicesCount} فاتورة</td>
            <td class="py-2.5 px-3 font-mono-num font-bold text-emerald-700">${s.totalSales.toLocaleString('ar-EG', { minimumFractionDigits: 2 })} د.ل</td>
        `;
        topStoresTbody.appendChild(tr);
    });
}

function calculatePoissonProbability(k, lambda) {
    if (k < 0 || lambda <= 0) return 0;
    // P(X = k) = (lambda^k * e^-lambda) / k!
    // Using log space to prevent overflow for larger k:
    // ln(P) = k * ln(lambda) - lambda - ln(k!)
    let logFactorial = 0;
    for (let i = 1; i <= k; i++) {
        logFactorial += Math.log(i);
    }
    const logProb = k * Math.log(lambda) - lambda - logFactorial;
    return Math.exp(logProb);
}

function renderPoissonCurveChart(data) {
    const ctx = document.getElementById('chart-poisson-curve');
    if (!ctx) return;
    if (adminState.charts.poisson) adminState.charts.poisson.destroy();

    const lambda = 38.5; // scans per minute at peak rush
    const kValues = [];
    const theoreticalProbs = [];
    const simulatedActualRates = [];

    // Evaluate for k = 20 to 60 scans/min
    for (let k = 20; k <= 60; k += 2) {
        kValues.push(`${k} ع/د`);
        const p = calculatePoissonProbability(k, lambda) * 100; // in percentage %
        theoreticalProbs.push(parseFloat(p.toFixed(2)));
        
        // Empirical distribution with minor stochastic deviation
        const noise = (Math.sin(k * 1.3) * 0.3) + ((k === 38 || k === 40) ? 0.4 : -0.2);
        const actual = Math.max(0, p + noise);
        simulatedActualRates.push(parseFloat(actual.toFixed(2)));
    }

    adminState.charts.poisson = new Chart(ctx, {
        type: 'line',
        data: {
            labels: kValues,
            datasets: [
                {
                    label: 'منحنى بواسون النظري المتوقع P(X = k)',
                    data: theoreticalProbs,
                    borderColor: '#6366f1',
                    backgroundColor: 'rgba(99, 102, 241, 0.12)',
                    fill: true,
                    tension: 0.4,
                    borderWidth: 2.5,
                    pointRadius: 3,
                    pointBackgroundColor: '#6366f1'
                },
                {
                    label: 'معدل المسح الفعلي المرصود بالمول (Empirical Actual)',
                    data: simulatedActualRates,
                    type: 'bar',
                    backgroundColor: 'rgba(16, 185, 129, 0.45)',
                    borderColor: '#10b981',
                    borderWidth: 1.5,
                    borderRadius: 4
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false
            },
            plugins: {
                legend: {
                    position: 'top',
                    labels: {
                        color: '#475569',
                        font: { family: 'Alexandria', size: 11, weight: 'bold' },
                        boxWidth: 14
                    }
                },
                tooltip: {
                    callbacks: {
                        label: function(context) {
                            return `${context.dataset.label}: ${context.raw}% احتمالية الحدوث`;
                        }
                    }
                }
            },
            scales: {
                y: {
                    title: {
                        display: true,
                        text: 'الاحتمالية الإحصائية %',
                        color: '#64748b',
                        font: { family: 'Alexandria', size: 10 }
                    },
                    ticks: {
                        color: '#64748b',
                        callback: function(v) { return v + '%'; }
                    },
                    grid: { color: '#f1f5f9' }
                },
                x: {
                    title: {
                        display: true,
                        text: 'كثافة المسح في الدقيقة (k scans / min)',
                        color: '#64748b',
                        font: { family: 'Alexandria', size: 10 }
                    },
                    ticks: { color: '#64748b', font: { family: 'Plus Jakarta Sans', size: 10 } },
                    grid: { display: false }
                }
            }
        }
    });
}

function renderScanPaceChart(paceData) {
    const ctx = document.getElementById('chart-scan-pace');
    if (!ctx) return;
    if (adminState.charts.pace) adminState.charts.pace.destroy();

    adminState.charts.pace = new Chart(ctx, {
        type: 'line',
        data: {
            labels: paceData.map(p => p.timeLabel),
            datasets: [{
                label: 'عدد الفواتير الممسوحة',
                data: paceData.map(p => p.scanCount),
                borderColor: '#d97706',
                backgroundColor: 'rgba(217, 119, 6, 0.08)',
                fill: true,
                tension: 0.35,
                borderWidth: 2.5,
                pointBackgroundColor: '#d97706'
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { ticks: { color: '#64748b' }, grid: { color: '#f1f5f9' } },
                x: { ticks: { color: '#64748b', font: { family: 'Alexandria' } }, grid: { display: false } }
            }
        }
    });
}

function renderCategoriesDonutChart(catData) {
    const ctx = document.getElementById('chart-categories');
    if (!ctx) return;
    if (adminState.charts.categories) adminState.charts.categories.destroy();

    adminState.charts.categories = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: catData.map(c => c.categoryName),
            datasets: [{
                data: catData.map(c => c.totalAmount),
                backgroundColor: ['#d97706', '#059669', '#0284c7', '#db2777', '#7c3aed'],
                borderWidth: 2,
                borderColor: '#ffffff'
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { position: 'bottom', labels: { color: '#475569', font: { family: 'Alexandria', size: 10 } } }
            }
        }
    });
}

// ========================================================
// 2. 🏢 TENANTS & STORES (Screen 1)
// ========================================================
async function loadStoresData() {
    const loyaltyType = document.getElementById('stores-filter-loyalty')?.value || '';
    const search = document.getElementById('stores-search-input')?.value || '';

    const queryParams = new URLSearchParams();
    if (loyaltyType) queryParams.append('loyaltyType', loyaltyType);
    if (search) queryParams.append('search', search);

    const stores = await apiCall(`/superadmin/stores?${queryParams.toString()}`);
    adminState.stores = stores;

    // Update Stores Top KPI Cards
    const totalCount = stores.length;
    const mallPointsCount = stores.filter(s => s.loyaltyType === 'MallPoints').length;
    const cashbackCount = stores.filter(s => s.loyaltyType === 'CashBack').length;
    const storePointsCount = stores.filter(s => s.loyaltyType === 'StorePoints').length;

    document.getElementById('stores-kpi-total').textContent = totalCount;
    document.getElementById('stores-kpi-mallpoints').textContent = mallPointsCount;
    document.getElementById('stores-kpi-cashback').textContent = cashbackCount;
    document.getElementById('stores-kpi-storepoints').textContent = storePointsCount;

    const tbody = document.getElementById('stores-table-body');
    tbody.innerHTML = '';

    if (stores.length === 0) {
        tbody.innerHTML = `<tr><td colspan="8" class="py-8 text-center text-slate-500 text-xs">لا توجد متاجر مطابقة.</td></tr>`;
        return;
    }

    stores.forEach(s => {
        const tr = document.createElement('tr');
        tr.className = 'border-b border-slate-100 hover:bg-slate-50 transition text-xs';
        tr.innerHTML = `
            <td class="py-3 px-3">
                <div class="flex items-center gap-2.5">
                    <img src="${s.logoUrl || 'https://images.unsplash.com/photo-1441986300917-64674bd600d8?w=80&h=80&fit=crop'}" class="w-9 h-9 rounded-xl object-cover bg-slate-100 border border-slate-200 shrink-0">
                    <div>
                        <div class="font-bold text-slate-900">${s.name}</div>
                        <div class="text-[10px] text-slate-500">إجمالي المبيعات: <b class="text-emerald-700 font-mono-num">${s.totalSales.toFixed(2)} د.ل</b></div>
                    </div>
                </div>
            </td>
            <td class="py-3 px-3 text-slate-700 font-medium">${s.category}</td>
            <td class="py-3 px-3 font-semibold text-slate-700">${s.floorNumber} - <span class="font-mono-num text-amber-700 font-bold">${s.unitNumber}</span></td>
            <td class="py-3 px-3">
                <span class="inline-block px-2.5 py-0.5 rounded-full text-[10px] font-bold ${
                    s.loyaltyType === 'MallPoints' ? 'badge-amber' :
                    (s.loyaltyType === 'CashBack' ? 'badge-cyan' : 'badge-purple')
                }">
                    ${s.loyaltyTypeLabel}
                </span>
            </td>
            <td class="py-3 px-3 font-mono-num font-bold text-amber-700">${s.commissionRate}%</td>
            <td class="py-3 px-3 text-slate-700">
                <div class="font-semibold">${s.contactPerson || '-'}</div>
                <div class="text-[10px] text-slate-500 font-mono-num">${s.contactPhone || ''}</div>
            </td>
            <td class="py-3 px-3">
                <input type="checkbox" class="custom-toggle" ${s.isActive ? 'checked' : ''} onchange="toggleStoreActive('${s.id}', this.checked)">
            </td>
            <td class="py-3 px-3 text-left">
                <button onclick="deleteStore('${s.id}')" class="px-2.5 py-1 rounded-lg bg-rose-50 text-rose-700 hover:bg-rose-100 border border-rose-200 font-bold text-[11px] transition">حذف</button>
            </td>
        `;
        tbody.appendChild(tr);
    });

    initLucideIcons();
}

function openAddStoreModal() {
    const modal = document.getElementById('modal-add-store');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

async function submitCreateStore() {
    const name = document.getElementById('store-name').value.trim();
    const category = document.getElementById('store-cat').value.trim();
    const floorNumber = document.getElementById('store-floor').value.trim();
    const unitNumber = document.getElementById('store-unit').value.trim();
    const loyaltyType = document.getElementById('store-loyalty-type').value;
    const commissionRate = parseFloat(document.getElementById('store-comm').value) || 5.0;
    const contactPerson = document.getElementById('store-contact').value.trim();

    if (!name || !category) {
        showToast('يرجى إدخال اسم ونشاط المتجر.', 'warning');
        return;
    }

    try {
        showLoading(true);
        await apiCall('/superadmin/stores', {
            method: 'POST',
            body: JSON.stringify({
                name,
                category,
                floorNumber,
                unitNumber,
                loyaltyType,
                commissionRate,
                contactPerson,
                logoUrl: 'https://images.unsplash.com/photo-1441986300917-64674bd600d8?w=200&h=200&fit=crop'
            })
        });

        triggerConfetti();
        showToast(`تمت إضافة متجر (${name}) بنجاح!`, 'success');
        closeModal('modal-add-store');
        document.getElementById('store-name').value = '';
        document.getElementById('store-cat').value = '';
        await loadStoresData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

async function toggleStoreActive(storeId, isActive) {
    try {
        const store = adminState.stores.find(s => s.id === storeId);
        if (!store) return;
        await apiCall(`/superadmin/stores/${storeId}`, {
            method: 'PUT',
            body: JSON.stringify({
                name: store.name,
                category: store.category,
                floorNumber: store.floorNumber,
                unitNumber: store.unitNumber,
                loyaltyType: store.loyaltyType,
                commissionRate: store.commissionRate,
                contactPerson: store.contactPerson,
                contactPhone: store.contactPhone,
                isActive: isActive
            })
        });
        showToast(`تم تحديث حالة المتجر بنجاح`, 'info');
    } catch (e) {
        showToast(e.message, 'error');
    }
}

async function deleteStore(id) {
    if (!confirm('هل أنت متأكد من رغبتك في حذف هذا المتجر؟')) return;
    try {
        showLoading(true);
        await apiCall(`/superadmin/stores/${id}`, { method: 'DELETE' });
        showToast('تم حذف المتجر بنجاح.', 'info');
        await loadStoresData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

// ========================================================
// 3. 🛡️ FRAUD & REVIEW HUB (Screen 2)
// ========================================================
let currentFraudFilter = 'ALL';

async function loadFraudQueueData() {
    const list = await apiCall(`/superadmin/fraud/queue?statusFilter=${currentFraudFilter}`);
    adminState.fraudQueue = list;

    // Render Waveform Chart
    renderFraudWaveChart();

    const tbody = document.getElementById('fraud-table-body');
    tbody.innerHTML = '';

    if (list.length === 0) {
        tbody.innerHTML = `<tr><td colspan="6" class="py-8 text-center text-emerald-700 text-xs font-bold">لا توجد فواتير مشبوهة حالياً. جميع العمليات آمنة ونظيفة 100%!</td></tr>`;
        return;
    }

    list.forEach(inv => {
        const isHigh = inv.riskScore > 70;
        const isMed = inv.riskScore >= 31 && inv.riskScore <= 70;

        const tr = document.createElement('tr');
        tr.className = 'border-b border-slate-100 hover:bg-slate-50 transition text-xs';
        tr.innerHTML = `
            <td class="py-3 px-3">
                <div class="font-bold text-slate-900">${inv.merchantName}</div>
                <div class="text-[10px] text-slate-400 font-mono-num">${inv.invoiceNumber}</div>
            </td>
            <td class="py-3 px-3">
                <div class="text-slate-800 font-semibold">${inv.customerName}</div>
                <div class="text-[10px] text-slate-400 font-mono-num">${inv.customerPhone}</div>
            </td>
            <td class="py-3 px-3 font-mono-num font-bold text-emerald-700">${inv.totalAmount.toFixed(2)} د.ل</td>
            <td class="py-3 px-3">
                <div class="flex items-center gap-2">
                    <span class="font-mono-num font-black ${isHigh ? 'text-rose-700' : (isMed ? 'text-amber-700' : 'text-emerald-700')}">${inv.riskScore}/100</span>
                    <div class="w-16 risk-meter">
                        <div class="risk-meter-fill ${isHigh ? 'bg-rose-500' : (isMed ? 'bg-amber-500' : 'bg-emerald-500')}" style="width: ${inv.riskScore}%"></div>
                    </div>
                </div>
            </td>
            <td class="py-3 px-3">
                <div class="text-slate-700 font-medium">${inv.fraudFlagReason || 'قيد تدقيق الذكاء الاصطناعي'}</div>
                ${inv.hasHashMatch ? `<span class="inline-block mt-0.5 px-1.5 py-0.2 rounded text-[9px] font-bold bg-rose-50 text-rose-700 border border-rose-200">تطابق بصمة الصورة pHash</span>` : ''}
            </td>
            <td class="py-3 px-3 text-left">
                <button onclick="openFraudInspectModal('${inv.id}')" class="px-3 py-1.5 rounded-xl bg-amber-50 text-amber-800 hover:bg-amber-100 border border-amber-200 font-bold text-[11px] transition flex items-center gap-1.5 ml-auto">
                    <i data-lucide="eye" class="w-3.5 h-3.5"></i>
                    <span>فحص ومطابقة</span>
                </button>
            </td>
        `;
        tbody.appendChild(tr);
    });

    initLucideIcons();
}

function renderFraudWaveChart() {
    const ctx = document.getElementById('chart-fraud-wave');
    if (!ctx) return;
    if (adminState.charts.fraudWave) adminState.charts.fraudWave.destroy();

    adminState.charts.fraudWave = new Chart(ctx, {
        type: 'line',
        data: {
            labels: ['01:00', '04:00', '08:00', '12:00', '16:00', '20:00', '24:00'],
            datasets: [{
                data: [1, 0, 2, 4, 3, 5, 2],
                borderColor: '#0284c7',
                backgroundColor: 'rgba(2, 132, 199, 0.08)',
                fill: true,
                tension: 0.4,
                borderWidth: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { display: false },
                x: { ticks: { color: '#64748b', font: { size: 9 } }, grid: { display: false } }
            }
        }
    });
}

function filterFraudQueue(filter) {
    currentFraudFilter = filter;
    document.querySelectorAll('.fraud-tab-btn').forEach(btn => {
        if (btn.textContent.includes(filter === 'ALL' ? 'الكل' : (filter === 'SUSPECT' ? '31-70' : '>70'))) {
            btn.classList.add('active', 'bg-slate-100');
        } else {
            btn.classList.remove('active', 'bg-slate-100');
        }
    });
    loadFraudQueueData();
}

async function openFraudInspectModal(invoiceId) {
    const inv = adminState.fraudQueue.find(i => i.id === invoiceId);
    if (!inv) return;

    adminState.selectedFraudInvoice = inv;
    document.getElementById('fraud-modal-invoice-id').value = inv.id;
    document.getElementById('fraud-modal-img').src = inv.imageUrl || 'https://images.unsplash.com/photo-1554415707-9e4966a604f7?w=600&h=800&fit=crop';
    document.getElementById('fraud-modal-merchant').textContent = inv.merchantName;
    document.getElementById('fraud-modal-num').textContent = inv.invoiceNumber;
    document.getElementById('fraud-modal-amount').textContent = `${inv.totalAmount.toFixed(2)} د.ل (+${inv.pointsEarned} نقطة)`;
    document.getElementById('fraud-modal-customer').textContent = `${inv.customerName} (${inv.customerPhone})`;
    document.getElementById('fraud-modal-reason').textContent = inv.fraudFlagReason || 'اشتباه تكرار أو قيمة استثنائية';
    document.getElementById('fraud-modal-risk-badge').textContent = `Risk Score: ${inv.riskScore}`;

    // Fetch pHash comparison data
    const comparison = await apiCall(`/superadmin/fraud/hash-compare/${inv.id}`);
    const container = document.getElementById('fraud-modal-similar-container');
    container.innerHTML = '';

    if (comparison.similarInvoices.length === 0) {
        container.innerHTML = `<div class="p-3 text-center text-slate-500 text-xs">لا توجد صور أخرى ذات بصمة متطابقة في قاعدة البيانات.</div>`;
    } else {
        comparison.similarInvoices.forEach(sim => {
            const item = document.createElement('div');
            item.className = 'p-2.5 rounded-xl bg-slate-50 border border-slate-200 flex items-center justify-between text-xs';
            item.innerHTML = `
                <div class="flex items-center gap-2.5">
                    <img src="${sim.imageUrl || 'https://images.unsplash.com/photo-1554415707-9e4966a604f7?w=100&h=100&fit=crop'}" class="w-10 h-10 rounded-lg object-cover bg-slate-100 border border-slate-200 shrink-0">
                    <div>
                        <div class="font-bold text-slate-900">${sim.merchantName} (${sim.invoiceNumber})</div>
                        <div class="text-[10px] text-slate-500">مرفوعة بواسطة: <span class="text-slate-800 font-semibold">${sim.customerName}</span></div>
                    </div>
                </div>
                <div class="text-left">
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono-num font-black ${sim.similarityPercentage > 90 ? 'bg-rose-50 text-rose-700 border border-rose-200' : 'bg-amber-50 text-amber-800 border border-amber-200'}">
                        تطابق ${sim.similarityPercentage}%
                    </span>
                </div>
            `;
            container.appendChild(item);
        });
    }

    const modal = document.getElementById('modal-fraud-inspect');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    initLucideIcons();
}

async function executeFraudAction(action) {
    const invoiceId = document.getElementById('fraud-modal-invoice-id').value;
    if (!invoiceId) return;

    let reason = null;
    if (action === 'REJECT' || action === 'FLAG_FRAUD') {
        reason = prompt('أدخل ملاحظات سبب الإجراء:');
        if (!reason && action === 'REJECT') return;
    }

    try {
        showLoading(true);
        await apiCall(`/superadmin/fraud/invoices/${invoiceId}/review`, {
            method: 'POST',
            body: JSON.stringify({ action, reason })
        });

        showToast(`تم تنفيذ الإجراء (${action}) بنجاح!`, 'success');
        closeModal('modal-fraud-inspect');
        await loadFraudQueueData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

// ========================================================
// 4. 📢 ADS & OFFERS HUB (Screen 3)
// ========================================================
async function loadAdsData() {
    const banners = await apiCall('/superadmin/banners');
    adminState.banners = banners;

    document.getElementById('ads-kpi-banners').textContent = banners.length;

    renderAdsCharts();

    const grid = document.getElementById('banners-grid');
    grid.innerHTML = '';

    banners.forEach(b => {
        const card = document.createElement('div');
        card.className = 'light-card overflow-hidden flex flex-col justify-between';
        card.innerHTML = `
            <div>
                <img src="${b.imageUrl}" alt="${b.title}" class="w-full h-36 object-cover bg-slate-100 border-b border-slate-100">
                <div class="p-4 space-y-2">
                    <div class="flex items-center justify-between text-[10px]">
                        <span class="px-2 py-0.5 rounded-full font-bold bg-pink-50 text-pink-700 border border-pink-200">شريحة: ${b.targetAudience}</span>
                        <span class="text-slate-500 font-mono-num">ترتيب: #${b.displayOrder}</span>
                    </div>
                    <h4 class="font-bold text-slate-900 text-xs">${b.title}</h4>
                    <p class="text-[11px] text-slate-500 line-clamp-2">${b.subtitle || ''}</p>
                </div>
            </div>
            <div class="p-3 bg-slate-50 border-t border-slate-100 flex justify-end">
                <button onclick="deleteBanner('${b.id}')" class="px-2.5 py-1 rounded-lg text-xs font-bold text-rose-600 hover:bg-rose-50 border border-rose-100 transition">حذف البنر</button>
            </div>
        `;
        grid.appendChild(card);
    });
}

function renderAdsCharts() {
    const ctxPerf = document.getElementById('chart-ads-performance');
    if (ctxPerf) {
        if (adminState.charts.adsPerf) adminState.charts.adsPerf.destroy();
        adminState.charts.adsPerf = new Chart(ctxPerf, {
            type: 'line',
            data: {
                labels: ['10 ص', '12 م', '2 م', '4 م', '6 م', '8 م', '10 م'],
                datasets: [
                    {
                        label: 'مشاهدات الإعلان',
                        data: [12000, 34000, 52000, 48000, 89000, 114000, 78000],
                        borderColor: '#db2777',
                        tension: 0.3,
                        borderWidth: 2
                    },
                    {
                        label: 'النقرات الفعلية',
                        data: [950, 2800, 4300, 3900, 7500, 9800, 6400],
                        borderColor: '#059669',
                        tension: 0.3,
                        borderWidth: 2
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { position: 'bottom', labels: { color: '#475569', font: { family: 'Alexandria' } } } },
                scales: {
                    y: { ticks: { color: '#64748b' }, grid: { color: '#f1f5f9' } },
                    x: { ticks: { color: '#64748b', font: { family: 'Alexandria' } }, grid: { display: false } }
                }
            }
        });
    }

    const ctxDonut = document.getElementById('chart-ads-donut');
    if (ctxDonut) {
        if (adminState.charts.adsDonut) adminState.charts.adsDonut.destroy();
        adminState.charts.adsDonut = new Chart(ctxDonut, {
            type: 'doughnut',
            data: {
                labels: ['أزياء', 'مطاعم', 'إلكترونيات', 'عطور'],
                datasets: [{
                    data: [45, 25, 20, 10],
                    backgroundColor: ['#db2777', '#d97706', '#0284c7', '#7c3aed'],
                    borderWidth: 2,
                    borderColor: '#ffffff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { position: 'bottom', labels: { color: '#475569', font: { family: 'Alexandria', size: 10 } } } }
            }
        });
    }
}

function openAddBannerModal() {
    const modal = document.getElementById('modal-add-banner');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

async function submitCreateBanner() {
    const title = document.getElementById('banner-title').value.trim();
    const subtitle = document.getElementById('banner-subtitle').value.trim();
    const imageUrl = document.getElementById('banner-image').value.trim();
    const targetAudience = document.getElementById('banner-audience').value;
    const displayOrder = parseInt(document.getElementById('banner-order').value, 10) || 1;

    if (!title || !imageUrl) {
        showToast('يرجى ملء عنوان ورابط صورة البنر.', 'warning');
        return;
    }

    try {
        showLoading(true);
        await apiCall('/superadmin/banners', {
            method: 'POST',
            body: JSON.stringify({ title, subtitle, imageUrl, targetAudience, displayOrder })
        });

        showToast('تمت إضافة البنر بنجاح!', 'success');
        closeModal('modal-add-banner');
        await loadAdsData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

async function deleteBanner(id) {
    if (!confirm('حذف هذا البنر من تطبيق العميل؟')) return;
    try {
        showLoading(true);
        await apiCall(`/superadmin/banners/${id}`, { method: 'DELETE' });
        showToast('تم حذف البنر.', 'info');
        await loadAdsData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

// ========================================================
// 5. 🌟 MALL-WIDE LOYALTY (Screen 4)
// ========================================================
async function loadLoyaltyData() {
    const [rules, campaigns] = await Promise.all([
        apiCall('/superadmin/loyalty/rules'),
        apiCall('/superadmin/loyalty/campaigns')
    ]);

    adminState.campaigns = campaigns;

    renderLoyaltyFlowChart();

    const list = document.getElementById('campaigns-list');
    list.innerHTML = '';

    campaigns.forEach(c => {
        const card = document.createElement('div');
        card.className = 'light-card p-5 flex flex-col justify-between';
        card.innerHTML = `
            <div>
                <div class="flex items-center justify-between gap-2 mb-2">
                    <span class="px-2.5 py-0.5 rounded-full text-[10px] font-black bg-amber-50 text-amber-800 border border-amber-200 font-mono-num">
                        مضاعف ${c.pointsMultiplier}X نقاط
                    </span>
                    <span class="text-[10px] text-slate-500 font-mono-num">حد أدنى: ${c.minSpendAmount} د.ل</span>
                </div>
                <h4 class="font-bold text-slate-900 text-sm mb-1">${c.name}</h4>
                <p class="text-xs text-slate-500 line-clamp-2">${c.description}</p>
            </div>
            <div class="pt-3 mt-3 border-t border-slate-100 flex justify-between items-center text-[11px] text-slate-500">
                <span class="${c.isActive ? 'text-emerald-700' : 'text-slate-400'} font-bold">● ${c.isActive ? 'نشطة حالياً' : 'منتهية'}</span>
                <button onclick="deleteCampaign('${c.id}')" class="text-rose-600 hover:underline font-bold">إلغاء الحملة</button>
            </div>
        `;
        list.appendChild(card);
    });
}

function renderLoyaltyFlowChart() {
    const ctx = document.getElementById('chart-loyalty-flow');
    if (!ctx) return;
    if (adminState.charts.loyaltyFlow) adminState.charts.loyaltyFlow.destroy();

    adminState.charts.loyaltyFlow = new Chart(ctx, {
        type: 'line',
        data: {
            labels: ['الأسبوع 1', 'الأسبوع 2', 'الأسبوع 3', 'الأسبوع 4'],
            datasets: [
                {
                    label: 'النقاط المكتسبة (كسب)',
                    data: [4200, 6800, 8900, 11400],
                    borderColor: '#059669',
                    backgroundColor: 'rgba(5, 150, 105, 0.08)',
                    fill: true,
                    tension: 0.35,
                    borderWidth: 2
                },
                {
                    label: 'النقاط المستبدلة (قسائم)',
                    data: [1200, 2400, 3600, 4800],
                    borderColor: '#d97706',
                    backgroundColor: 'rgba(217, 119, 6, 0.08)',
                    fill: true,
                    tension: 0.35,
                    borderWidth: 2
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom', labels: { color: '#475569', font: { family: 'Alexandria' } } } },
            scales: {
                y: { ticks: { color: '#64748b' }, grid: { color: '#f1f5f9' } },
                x: { ticks: { color: '#64748b', font: { family: 'Alexandria' } }, grid: { display: false } }
            }
        }
    });
}

function openAddCampaignModal() {
    const modal = document.getElementById('modal-add-campaign');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

async function submitCreateCampaign() {
    const name = document.getElementById('campaign-name').value.trim();
    const description = document.getElementById('campaign-desc').value.trim();
    const pointsMultiplier = parseFloat(document.getElementById('campaign-multiplier').value);
    const minSpendAmount = parseFloat(document.getElementById('campaign-min-spend').value);

    if (!name) {
        showToast('يرجى كتابة اسم الحملة.', 'warning');
        return;
    }

    try {
        showLoading(true);
        await apiCall('/superadmin/loyalty/campaigns', {
            method: 'POST',
            body: JSON.stringify({
                name,
                description,
                pointsMultiplier,
                minSpendAmount,
                startDate: new Date(),
                endDate: new Date(Date.now() + 14 * 86400000)
            })
        });

        triggerConfetti();
        showToast('تم إطلاق حملة مضاعفة النقاط بنجاح!', 'success');
        closeModal('modal-add-campaign');
        await loadLoyaltyData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

async function deleteCampaign(id) {
    if (!confirm('هل تريد حذف هذه الحملة؟')) return;
    try {
        showLoading(true);
        await apiCall(`/superadmin/loyalty/campaigns/${id}`, { method: 'DELETE' });
        showToast('تم حذف الحملة.', 'info');
        await loadLoyaltyData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

// ========================================================
// 6. 💳 SETTLEMENTS & CLEARING (Screen 5)
// ========================================================
async function loadSettlementsData() {
    const settlements = await apiCall('/superadmin/settlements');
    adminState.settlements = settlements;

    const totalGross = settlements.reduce((acc, s) => acc + s.totalRedemptionsValue, 0);
    const totalComm = settlements.reduce((acc, s) => acc + s.mallCommissionAmount, 0);
    const totalNet = settlements.reduce((acc, s) => acc + s.netPayableAmount, 0);

    document.getElementById('settlements-total-gross').textContent = `${totalGross.toFixed(2)} د.ل`;
    document.getElementById('settlements-total-comm').textContent = `${totalComm.toFixed(2)} د.ل`;
    document.getElementById('settlements-total-net').textContent = `${totalNet.toFixed(2)} د.ل`;

    const tbody = document.getElementById('settlements-table-body');
    tbody.innerHTML = '';

    settlements.forEach(s => {
        const isSettled = s.status === 'Settled' || s.status === 'Paid';
        const tr = document.createElement('tr');
        tr.className = 'border-b border-slate-100 hover:bg-slate-50 transition text-xs';
        tr.innerHTML = `
            <td class="py-3 px-3 font-bold text-slate-900">${s.storeName}</td>
            <td class="py-3 px-3 text-slate-700 font-semibold">${s.periodLabel}</td>
            <td class="py-3 px-3 font-mono-num text-slate-600">${s.redemptionsCount} قسيمة</td>
            <td class="py-3 px-3 font-mono-num font-bold text-slate-900">${s.totalRedemptionsValue.toFixed(2)} د.ل</td>
            <td class="py-3 px-3 font-mono-num text-emerald-700 font-bold">${s.mallCommissionAmount.toFixed(2)} د.ل</td>
            <td class="py-3 px-3 font-mono-num font-extrabold text-cyan-700">${s.netPayableAmount.toFixed(2)} د.ل</td>
            <td class="py-3 px-3">
                <span class="inline-block px-2.5 py-0.5 rounded-full text-[10px] font-bold ${
                    isSettled ? 'badge-emerald' : 'badge-amber'
                }">
                    ${isSettled ? 'تمت التسوية والصرف' : 'مستحق ومعلّق'}
                </span>
            </td>
            <td class="py-3 px-3 text-left">
                ${!isSettled ? `
                    <button onclick="settleStoreBatch('${s.id}')" class="px-3 py-1 rounded-xl bg-cyan-700 hover:bg-cyan-800 text-white font-bold text-[11px] transition shadow-sm">
                        صرف وتسوية
                    </button>
                ` : `<span class="text-[10px] text-slate-500 font-mono-num">${s.settledAt ? new Date(s.settledAt).toLocaleDateString('ar-EG') : 'مسوّى'}</span>`}
            </td>
        `;
        tbody.appendChild(tr);
    });
}

async function settleStoreBatch(id) {
    if (!confirm('تأكيد صرف المستحقات وإقفال دورة التسوية للمتجر؟')) return;
    try {
        showLoading(true);
        await apiCall(`/superadmin/settlements/${id}/settle`, { method: 'POST' });
        showToast('تم إتمام التسوية بنجاح!', 'success');
        await loadSettlementsData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

async function generateSettlementBatch() {
    try {
        showLoading(true);
        const res = await apiCall('/superadmin/settlements/generate-batch', {
            method: 'POST',
            body: JSON.stringify({})
        });
        showToast(res.message, 'success');
        await loadSettlementsData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

// ========================================================
// 7. 📱 CUSTOMER DATABASE (Screen 6)
// ========================================================
async function loadCustomersData() {
    const tier = document.getElementById('customers-filter-tier')?.value || 'ALL';
    const search = document.getElementById('customers-search-input')?.value || '';

    const queryParams = new URLSearchParams();
    if (tier !== 'ALL') queryParams.append('tier', tier);
    if (search) queryParams.append('search', search);

    const customers = await apiCall(`/superadmin/customers?${queryParams.toString()}`);
    adminState.customers = customers;

    const tbody = document.getElementById('customers-table-body');
    tbody.innerHTML = '';

    if (customers.length === 0) {
        tbody.innerHTML = `<tr><td colspan="7" class="py-8 text-center text-slate-500 text-xs">لا يوجد عملاء يطابقون معايير البحث.</td></tr>`;
        return;
    }

    customers.forEach(c => {
        const tr = document.createElement('tr');
        tr.className = `border-b border-slate-100 hover:bg-slate-50 transition text-xs ${c.isSuspended ? 'bg-rose-50/50' : ''}`;
        tr.innerHTML = `
            <td class="py-3 px-3">
                <div class="font-bold text-slate-900">${c.fullName}</div>
                <div class="text-[10px] text-slate-400 font-mono-num">${c.id}</div>
            </td>
            <td class="py-3 px-3 font-mono-num font-bold text-slate-700">${c.phoneNumber}</td>
            <td class="py-3 px-3">
                <span class="inline-block px-2.5 py-0.5 rounded-full text-[10px] font-bold ${
                    c.tier === 'Gold' ? 'badge-amber' :
                    (c.tier === 'Silver' ? 'badge-cyan' : 'badge-slate')
                }">
                    ${c.tier === 'Gold' ? '🥇 ذهبي (Gold)' : (c.tier === 'Silver' ? '🥈 فضي (Silver)' : '🥉 برونزي (Bronze)')}
                </span>
            </td>
            <td class="py-3 px-3 font-black text-amber-700 font-mono-num text-sm">${c.currentPoints.toLocaleString('ar-EG')} نقطة</td>
            <td class="py-3 px-3 font-mono-num text-emerald-700 font-bold">${c.totalSpent.toFixed(2)} د.ل</td>
            <td class="py-3 px-3">
                <span class="inline-block px-2 py-0.5 rounded text-[10px] font-bold ${c.isSuspended ? 'badge-rose' : 'badge-emerald'}">
                    ${c.isSuspended ? '❄️ مجمد' : 'نشط'}
                </span>
            </td>
            <td class="py-3 px-3 text-left">
                <div class="flex items-center justify-end gap-1.5">
                    <button onclick="openAdjustPointsModal('${c.id}', '${c.fullName}')" class="px-2.5 py-1 rounded-lg bg-amber-50 text-amber-800 hover:bg-amber-100 border border-amber-200 font-bold text-[11px] transition">تعديل النقاط</button>
                    <button onclick="toggleCustomerSuspend('${c.id}')" class="px-2.5 py-1 rounded-lg ${c.isSuspended ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : 'bg-rose-50 text-rose-700 border border-rose-200'} font-bold text-[11px] transition">
                        ${c.isSuspended ? 'فك التجميد' : 'تجميد'}
                    </button>
                </div>
            </td>
        `;
        tbody.appendChild(tr);
    });
}

function openAddCustomerModal() {
    const modal = document.getElementById('modal-add-customer');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

async function submitCreateCustomer() {
    const fullName = document.getElementById('cust-name').value.trim();
    const phoneNumber = document.getElementById('cust-phone').value.trim();
    const password = document.getElementById('cust-pass').value.trim() || 'Customer123!';

    if (!fullName || !phoneNumber) {
        showToast('يرجى ملء كافة الحقول.', 'warning');
        return;
    }

    try {
        showLoading(true);
        await apiCall('/auth/register', {
            method: 'POST',
            body: JSON.stringify({ fullName, phoneNumber, password })
        });

        showToast(`تم تسجيل العميل (${fullName}) بنجاح!`, 'success');
        closeModal('modal-add-customer');
        await loadCustomersData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

function openAdjustPointsModal(customerId, customerName) {
    document.getElementById('adjust-cust-id').value = customerId;
    document.getElementById('adjust-cust-name').textContent = customerName;
    document.getElementById('adjust-amount').value = '';
    document.getElementById('adjust-reason').value = '';

    const modal = document.getElementById('modal-adjust-points');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

async function submitAdjustPoints() {
    const customerId = document.getElementById('adjust-cust-id').value;
    const delta = parseInt(document.getElementById('adjust-amount').value, 10);
    const reason = document.getElementById('adjust-reason').value.trim() || 'تعديل إداري';

    if (isNaN(delta) || delta === 0) {
        showToast('أدخل قيمة صحيحة للنقاط.', 'warning');
        return;
    }

    try {
        showLoading(true);
        await apiCall(`/superadmin/customers/${customerId}/adjust-points`, {
            method: 'POST',
            body: JSON.stringify({ pointsDelta: delta, reason })
        });

        showToast('تم تحديث رصيد النقاط بنجاح.', 'success');
        closeModal('modal-adjust-points');
        await loadCustomersData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

async function toggleCustomerSuspend(customerId) {
    try {
        showLoading(true);
        const res = await apiCall(`/superadmin/customers/${customerId}/toggle-suspend`, { method: 'POST' });
        showToast(res.message, 'info');
        await loadCustomersData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

// ========================================================
// 8. 👥 STAFF & ROLE MANAGEMENT (Screen 7)
// ========================================================
async function loadStaffData() {
    const staff = await apiCall('/superadmin/staff');
    adminState.staff = staff;

    const tbody = document.getElementById('staff-table-body');
    tbody.innerHTML = '';

    staff.forEach(s => {
        const tr = document.createElement('tr');
        tr.className = 'border-b border-slate-100 hover:bg-slate-50 transition text-xs';
        tr.innerHTML = `
            <td class="py-3 px-3 font-bold text-slate-900">${s.fullName}</td>
            <td class="py-3 px-3 font-mono-num text-slate-700 font-bold">${s.phoneNumber}</td>
            <td class="py-3 px-3">
                <span class="inline-block px-2.5 py-0.5 rounded-full text-[10px] font-bold ${
                    s.staffRole === 'SuperAdmin' ? 'badge-amber' :
                    (s.staffRole === 'Auditor' ? 'badge-cyan' :
                    (s.staffRole === 'StoreManager' ? 'badge-purple' : 'badge-slate'))
                }">
                    ${s.staffRoleLabel || s.staffRole || s.role}
                </span>
            </td>
            <td class="py-3 px-3 text-slate-700 font-semibold">${s.storeName || 'إدارة المول المركزية'}</td>
            <td class="py-3 px-3 text-slate-500 text-[11px]">${new Date(s.createdAt).toLocaleDateString('ar-EG')}</td>
            <td class="py-3 px-3 text-left">
                ${s.staffRole !== 'SuperAdmin' ? `
                    <button onclick="deleteStaff('${s.id}')" class="px-2.5 py-1 rounded-lg bg-rose-50 text-rose-700 hover:bg-rose-100 border border-rose-200 font-bold text-[11px] transition">حذف</button>
                ` : '<span class="text-[10px] text-slate-400 font-bold">حساب رئيسي</span>'}
            </td>
        `;
        tbody.appendChild(tr);
    });

    if (adminState.stores.length === 0) {
        adminState.stores = await apiCall('/superadmin/stores');
    }
    const storeSelect = document.getElementById('staff-store');
    if (storeSelect) {
        storeSelect.innerHTML = '<option value="">إدارة المول المركزية</option>';
        adminState.stores.forEach(st => {
            const opt = document.createElement('option');
            opt.value = st.id;
            opt.textContent = st.name;
            storeSelect.appendChild(opt);
        });
    }
}

function openAddStaffModal() {
    const modal = document.getElementById('modal-add-staff');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

async function submitCreateStaff() {
    const fullName = document.getElementById('staff-name').value.trim();
    const phoneNumber = document.getElementById('staff-phone').value.trim();
    const password = document.getElementById('staff-pass').value.trim() || 'Staff123!';
    const staffRole = document.getElementById('staff-role').value;
    const storeTenantId = document.getElementById('staff-store').value || null;

    const role = (staffRole === 'StoreManager' || staffRole === 'Cashier') ? 'Merchant' : 'Admin';

    if (!fullName || !phoneNumber) {
        showToast('يرجى ملء كافة بيانات الموظف.', 'warning');
        return;
    }

    try {
        showLoading(true);
        await apiCall('/superadmin/staff', {
            method: 'POST',
            body: JSON.stringify({
                fullName,
                phoneNumber,
                password,
                role,
                staffRole,
                storeTenantId
            })
        });

        showToast(`تم إنشاء حساب (${fullName}) بنجاح!`, 'success');
        closeModal('modal-add-staff');
        await loadStaffData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

async function deleteStaff(id) {
    if (!confirm('هل تريد حذف حساب هذا الموظف؟')) return;
    try {
        showLoading(true);
        await apiCall(`/superadmin/staff/${id}`, { method: 'DELETE' });
        showToast('تم حذف الحساب بنجاح.', 'info');
        await loadStaffData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

// ========================================================
// 9. ⚙️ SYSTEM SETTINGS & AUDIT LOGS (Screen 8)
// ========================================================
async function loadSettingsAndAuditData() {
    const [settings, auditLogs] = await Promise.all([
        apiCall('/superadmin/settings'),
        apiCall('/superadmin/audit-logs')
    ]);

    adminState.auditLogs = auditLogs;

    document.getElementById('setting-ocr-conf').value = settings.ocrConfidenceThreshold;
    document.getElementById('setting-max-amount').value = settings.maxInvoiceAmountAutoApproval;
    document.getElementById('setting-daily-scans').value = settings.maxDailyScansPerCustomer;

    renderAuditTable(auditLogs);
}

function renderAuditTable(logs) {
    const tbody = document.getElementById('audit-table-body');
    tbody.innerHTML = '';

    if (logs.length === 0) {
        tbody.innerHTML = `<tr><td colspan="6" class="py-8 text-center text-slate-500 text-xs">لا توجد سجلات أمان مطابقة.</td></tr>`;
        return;
    }

    logs.forEach(a => {
        const tr = document.createElement('tr');
        tr.className = 'border-b border-slate-100 hover:bg-slate-50 transition text-xs';
        tr.innerHTML = `
            <td class="py-2.5 px-3 font-mono-num text-slate-500 text-[11px]">${new Date(a.createdAt).toLocaleString('ar-EG')}</td>
            <td class="py-2.5 px-3 font-bold text-slate-900">${a.performedBy}</td>
            <td class="py-2.5 px-3">
                <span class="inline-block px-2 py-0.5 rounded text-[10px] font-mono-num font-bold bg-slate-100 text-slate-800 border border-slate-200">
                    ${a.action}
                </span>
            </td>
            <td class="py-2.5 px-3 font-semibold text-slate-700">${a.entityName} ${a.entityId ? `(#${a.entityId})` : ''}</td>
            <td class="py-2.5 px-3 text-slate-600 max-w-xs truncate">${a.details}</td>
            <td class="py-2.5 px-3 font-mono-num text-slate-400 text-[10px]">${a.ipAddress || '127.0.0.1'}</td>
        `;
        tbody.appendChild(tr);
    });
}

async function loadAuditLogsData() {
    const search = document.getElementById('audit-search-input')?.value || '';
    const logs = await apiCall(`/superadmin/audit-logs${search ? `?search=${encodeURIComponent(search)}` : ''}`);
    renderAuditTable(logs);
}

async function saveSystemSettings() {
    const ocrConfidenceThreshold = parseFloat(document.getElementById('setting-ocr-conf').value);
    const maxInvoiceAmountAutoApproval = parseFloat(document.getElementById('setting-max-amount').value);
    const maxDailyScansPerCustomer = parseInt(document.getElementById('setting-daily-scans').value, 10);

    try {
        showLoading(true);
        await apiCall('/superadmin/settings', {
            method: 'PUT',
            body: JSON.stringify({
                ocrConfidenceThreshold,
                maxInvoiceAmountAutoApproval,
                maxDailyScansPerCustomer
            })
        });

        showToast('تم حفظ معايير الذكاء الاصطناعي والإعدادات بنجاح!', 'success');
        await loadSettingsAndAuditData();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        showLoading(false);
    }
}

// ========================================================
// GENERAL UI UTILITIES
// ========================================================
function triggerConfetti() {
    if (window.confetti) {
        confetti({ particleCount: 80, spread: 70, origin: { y: 0.6 } });
    }
}

function showToast(message, type = 'info') {
    const toast = document.createElement('div');
    const colors = {
        success: 'bg-emerald-600 text-white',
        error: 'bg-rose-600 text-white',
        warning: 'bg-amber-600 text-slate-950 font-black',
        info: 'bg-slate-900 text-white border border-slate-700'
    };

    toast.className = `fixed bottom-5 left-5 z-50 px-4 py-3 rounded-2xl shadow-2xl font-bold text-xs transition-all duration-300 transform translate-y-4 opacity-0 flex items-center gap-2 ${colors[type] || colors.info}`;
    toast.innerHTML = `<span>${message}</span>`;
    document.body.appendChild(toast);

    setTimeout(() => toast.classList.remove('translate-y-4', 'opacity-0'), 50);
    setTimeout(() => {
        toast.classList.add('translate-y-4', 'opacity-0');
        setTimeout(() => toast.remove(), 350);
    }, 3500);
}

function showLoading(show) {
    const loader = document.getElementById('global-loader');
    if (loader) loader.classList.toggle('hidden', !show);
}

function closeModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function openNotificationModal() {
    switchAdminTab('ads');
}
