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

  window.MM = MM;
  window.showToast = MM.toast; // توافق مع الصفحات القديمة
  window.openModal = window.openModal || MM.openModal;
  window.closeModal = window.closeModal || MM.closeModal;
})();
