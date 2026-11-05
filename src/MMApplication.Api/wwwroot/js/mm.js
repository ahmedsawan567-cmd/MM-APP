/* منظومة المرأة مول — سلوك واجهة لوحة التحكم المشترك */
(function () {
  'use strict';

  const charts = [];
  const root = document.documentElement;

  const MM = {
    /** لون من متغيرات النظام كـ rgb() — مثال: MM.color('chart-grid') */
    color(name, alpha) {
      // يُرجع لوناً بصيغة hex أو rgba() بفواصل (ApexCharts لا يفهم صيغة rgb الحديثة بالمسافات)
      const [r, g, b] = getComputedStyle(root).getPropertyValue('--' + name).trim().split(/\s+/).map(Number);
      if (alpha != null) return `rgba(${r}, ${g}, ${b}, ${alpha})`;
      return '#' + [r, g, b].map(x => x.toString(16).padStart(2, '0')).join('');
    },

    isDark() { return root.getAttribute('data-theme') === 'dark'; },

    toggleTheme() {
      const next = MM.isDark() ? 'light' : 'dark';
      root.classList.add('theme-transition');
      root.setAttribute('data-theme', next);
      try { localStorage.setItem('mm-theme', next); } catch (e) { }
      setTimeout(() => root.classList.remove('theme-transition'), 250);
      charts.forEach(({ chart, build }) => chart.updateOptions(MM.chartTheme(build()), false, false));
      window.dispatchEvent(new CustomEvent('mm-theme', { detail: next }));
    },

    /** فتح/إغلاق القائمة الجانبية على الجوال */
    nav(show) {
      const sidebar = document.getElementById('sidebar');
      const backdrop = document.getElementById('nav-backdrop');
      if (!sidebar) return;
      sidebar.classList.toggle('translate-x-full', !show);
      backdrop.classList.toggle('hidden', !show);
      document.body.classList.toggle('overflow-hidden', !!show);
    },

    dropdown(id) {
      const el = document.getElementById(id);
      if (!el) return;
      const open = el.classList.contains('hidden');
      document.querySelectorAll('.dropdown').forEach(d => { if (d !== el && d.id !== 'quick-nav-results') d.classList.add('hidden'); });
      el.classList.toggle('hidden', !open);
      el.previousElementSibling?.setAttribute('aria-expanded', String(open));
    },

    toast(message, type = 'success') {
      Swal.mixin({ toast: true, position: 'top-start', showConfirmButton: false, timer: 2800, timerProgressBar: true })
        .fire({ icon: type, title: message });
    },

    help() {
      Swal.fire({
        title: 'الدليل السريع',
        html: `<div class="text-right text-sm space-y-2 leading-relaxed">
          <p><b>مراجعة الفواتير:</b> فواتير لم تُعتمد تلقائياً. قارن الصورة بما قرأه الذكاء الاصطناعي ثم اعتمد أو ارفض.</p>
          <p><b>التقارير:</b> المبيعات والنقاط والعملاء حسب الفترة والمتجر، مع تصدير Excel.</p>
          <p><b>المكافآت والقسائم:</b> كتالوج المكافآت والتحقق من كود قسيمة العميل عند الدفع.</p>
          <p><b>اختصارات:</b> اضغط <kbd>/</kbd> للانتقال السريع بين الصفحات.</p></div>`,
        confirmButtonText: 'حسناً'
      });
    },

    /** خيارات ApexCharts المشتركة حسب الوضع الحالي */
    chartTheme(options) {
      const axis = MM.color('chart-axis');
      return Object.assign({}, options, {
        theme: { mode: MM.isDark() ? 'dark' : 'light' },
        chart: Object.assign({ fontFamily: '"IBM Plex Sans Arabic", sans-serif', toolbar: { show: false }, background: 'transparent', foreColor: axis }, options.chart || {}),
        grid: Object.assign({ borderColor: MM.color('chart-grid'), strokeDashArray: 0 }, options.grid || {}),
        xaxis: Object.assign({}, options.xaxis || {}, {
          labels: Object.assign({ style: { colors: axis, fontSize: '11px' } }, (options.xaxis || {}).labels || {}),
          axisBorder: { show: false }, axisTicks: { show: false }
        }),
        yaxis: Object.assign({}, options.yaxis || {}, {
          labels: Object.assign({}, (options.yaxis || {}).labels || {}, { style: { colors: axis, fontSize: '11px' } })
        })
      });
    },

    /** إنشاء مخطط يتبع الوضع الفاتح/الداكن. build() تُرجع الخيارات. */
    chart(el, build) {
      if (!el || !window.ApexCharts) return null;
      const chart = new ApexCharts(el, MM.chartTheme(build()));
      chart.render();
      charts.push({ chart, build });
      return chart;
    },

    esc(v) {
      return String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }
  };

  // ---------- الانتقال السريع بين الصفحات ----------
  function setupQuickNav() {
    const input = document.getElementById('quick-nav');
    const box = document.getElementById('quick-nav-results');
    if (!input || !box) return;
    let index = 0;

    const render = () => {
      const q = input.value.trim();
      const items = (window.MM_NAV || []).filter(n => !q || n.label.includes(q) || n.section.includes(q));
      if (!items.length) { box.innerHTML = '<div class="px-3 py-2 text-sm text-ink-3">لا توجد نتائج</div>'; box.classList.remove('hidden'); return; }
      index = Math.min(index, items.length - 1);
      box.innerHTML = items.map((n, i) =>
        `<a href="${n.url}" class="dropdown-item ${i === index ? 'bg-slate-100 text-ink' : ''}">
           <i data-lucide="${n.icon}" class="h-4 w-4"></i><span>${MM.esc(n.label)}</span>
           <span class="mr-auto text-[11px] text-ink-3">${MM.esc(n.section)}</span></a>`).join('');
      box.classList.remove('hidden');
      window.lucide && lucide.createIcons();
    };

    input.addEventListener('focus', render);
    input.addEventListener('input', () => { index = 0; render(); });
    input.addEventListener('keydown', e => {
      const links = box.querySelectorAll('a');
      if (e.key === 'ArrowDown') { index = Math.min(index + 1, links.length - 1); render(); e.preventDefault(); }
      else if (e.key === 'ArrowUp') { index = Math.max(index - 1, 0); render(); e.preventDefault(); }
      else if (e.key === 'Enter' && links[index]) { location.href = links[index].href; e.preventDefault(); }
      else if (e.key === 'Escape') { box.classList.add('hidden'); input.blur(); }
    });
    document.addEventListener('click', e => { if (!input.parentElement.contains(e.target)) box.classList.add('hidden'); });
  }

  document.addEventListener('keydown', e => {
    const tag = (e.target.tagName || '').toLowerCase();
    if (e.key === '/' && !['input', 'textarea', 'select'].includes(tag) && !e.target.isContentEditable) {
      document.getElementById('quick-nav')?.focus();
      e.preventDefault();
    }
    if (e.key === 'Escape') {
      MM.nav(false);
      document.querySelectorAll('.dropdown').forEach(d => d.classList.add('hidden'));
    }
  });

  document.addEventListener('click', e => {
    if (!e.target.closest('[aria-haspopup="menu"]') && !e.target.closest('.dropdown')) {
      document.querySelectorAll('.dropdown').forEach(d => { if (d.id !== 'quick-nav-results') d.classList.add('hidden'); });
    }
  });

  document.addEventListener('DOMContentLoaded', () => {
    // نقل نوافذ الحوار إلى body حتى تغطي الشريط الجانبي والعلوي (خارج سياق تكديس المحتوى).
    document.querySelectorAll('.modal-overlay').forEach(m => document.body.appendChild(m));
    window.lucide && lucide.createIcons();
    setupQuickNav();
    MM.notifications.start();
  });

  // نوافذ الحوار (modal-overlay): فتح/إغلاق موحّد لكل الصفحات، مع الإغلاق بـ Esc أو بالنقر خارج النافذة.
  MM.openModal = id => {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.remove('hidden');
    window.lucide && lucide.createIcons();
    setTimeout(() => el.querySelector('input:not([type=hidden]):not([disabled]), select, textarea')?.focus(), 30);
  };
  MM.closeModal = id => document.getElementById(id)?.classList.add('hidden');

  document.addEventListener('keydown', e => {
    if (e.key !== 'Escape') return;
    const open = [...document.querySelectorAll('.modal-overlay:not(.hidden)')].pop();
    open && open.classList.add('hidden');
  });
  document.addEventListener('mousedown', e => {
    if (e.target.classList?.contains('modal-overlay')) e.target.classList.add('hidden');
  });

  // ---------- الإشعارات (جرس الشريط العلوي) ----------
  MM.notifications = (() => {
    const POLL_MS = 30000;
    let lastSeen = null, open = false, loaded = false;
    const el = id => document.getElementById(id);
    const token = () => document.querySelector('#mm-af input[name="__RequestVerificationToken"]')?.value || '';
    const ago = iso => {
      const s = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
      if (s < 60) return 'الآن';
      if (s < 3600) return `منذ ${Math.floor(s / 60)} د`;
      if (s < 86400) return `منذ ${Math.floor(s / 3600)} س`;
      return new Date(iso).toLocaleDateString('ar-LY');
    };
    const ICON = { REVIEW_NEEDED: 'scan-search', INVOICE_DISPUTED: 'message-circle-warning', INVOICE_RESUBMITTED: 'image-up', FRAUD_DUPLICATE: 'shield-alert' };

    function render(data) {
      const badge = el('notif-badge');
      if (badge) {
        badge.textContent = data.unread > 99 ? '99+' : data.unread;
        badge.classList.toggle('hidden', !data.unread);
      }
      document.title = document.title.replace(/^\(\d+\+?\) /, '');
      if (data.unread) document.title = `(${data.unread > 99 ? '99+' : data.unread}) ${document.title}`;
      const list = el('notif-list');
      if (!list) return;
      list.innerHTML = data.items.length === 0
        ? '<div class="px-4 py-10 text-center text-sm text-ink-3">لا توجد إشعارات بعد</div>'
        : data.items.map(n => `
            <button type="button" class="w-full text-right px-4 py-3 flex gap-3 hover:bg-slate-50 ${n.isRead ? '' : 'bg-brand-50/60'}" data-id="${n.id}" data-link="${MM.esc(n.link || '')}">
              <span class="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-xl ${n.type === 'FRAUD_DUPLICATE' ? 'bg-rose-50 text-rose-600' : 'bg-brand-50 text-brand-700'}">
                <i data-lucide="${ICON[n.type] || 'bell'}" class="h-4 w-4"></i>
              </span>
              <span class="min-w-0 flex-1">
                <span class="flex items-start justify-between gap-2">
                  <span class="font-semibold text-ink text-sm">${MM.esc(n.title)}</span>
                  ${n.isRead ? '' : '<span class="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand-600"></span>'}
                </span>
                <span class="block text-sm text-ink-3 leading-relaxed">${MM.esc(n.body)}</span>
                <span class="block text-xs text-ink-3 mt-1">${ago(n.createdAt)}</span>
              </span>
            </button>`).join('');
      list.querySelectorAll('button[data-id]').forEach(b => b.onclick = () => openItem(b.dataset.id, b.dataset.link));
      window.lucide && lucide.createIcons();
    }

    function desktopAlert(items) {
      if (!('Notification' in window) || Notification.permission !== 'granted' || !document.hidden) return;
      items.slice(0, 3).forEach(n => {
        const notice = new Notification(n.title, { body: n.body, icon: '/img/brand/icon-192.png', tag: n.id });
        notice.onclick = () => { window.focus(); if (n.link) location.href = n.link; };
      });
    }

    async function refresh() {
      try {
        const res = await fetch('/AdminDashboard/NotificationsJson', { headers: { 'Accept': 'application/json' } });
        if (!res.ok) return;
        const data = await res.json();
        const fresh = lastSeen ? data.items.filter(n => !n.isRead && new Date(n.createdAt) > lastSeen) : [];
        if (data.items[0]) lastSeen = new Date(Math.max(lastSeen || 0, new Date(data.items[0].createdAt)));
        else lastSeen = lastSeen || new Date();
        loaded = true;
        render(data);
        if (fresh.length) {
          desktopAlert(fresh);
          if (!document.hidden) MM.toast(fresh[0].title, 'info');
        }
      } catch { }
    }

    async function post(id) {
      await fetch('/AdminDashboard/NotificationsRead' + (id ? `?id=${encodeURIComponent(id)}` : ''), {
        method: 'POST', headers: { 'RequestVerificationToken': token() }
      }).catch(() => {});
    }

    async function openItem(id, link) {
      await post(id);
      if (link) location.href = link; else refresh();
    }

    return {
      start() {
        if (!el('notif-button')) return;
        refresh();
        setInterval(refresh, POLL_MS);
        document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(); });
        if ('Notification' in window && Notification.permission === 'default') el('notif-desktop')?.classList.remove('hidden');
      },
      toggle() {
        open = !el('notif-menu').classList.toggle('hidden') ? true : false;
        el('notif-button').setAttribute('aria-expanded', String(open));
        if (open && !loaded) refresh();
      },
      async readAll() { await post(null); refresh(); },
      async enableDesktop() {
        const p = await Notification.requestPermission();
        el('notif-desktop')?.classList.add('hidden');
        MM.toast(p === 'granted' ? 'ستصلك تنبيهات المتصفح عند وصول إشعار جديد.' : 'لم يُسمح بتنبيهات المتصفح.', p === 'granted' ? 'success' : 'info');
      },
    };
  })();

  window.MM = MM;
  window.showToast = MM.toast; // توافق مع الصفحات القديمة
  window.openModal = window.openModal || MM.openModal;
  window.closeModal = window.closeModal || MM.closeModal;
})();
