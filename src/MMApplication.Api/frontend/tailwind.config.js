/**
 * إعداد Tailwind للوحة التحكم.
 *
 * الألوان المحايدة (slate) وخلفيات الألوان الدلالية الفاتحة (50-300) معرّفة كمتغيرات CSS،
 * فتتبدّل تلقائياً بين الوضع الفاتح والداكن (انظر input.css) دون إضافة dark: لكل عنصر.
 * الدرجات الداكنة (400+) ثابتة لأنها تُستخدم كخلفيات صلبة لأزرار نصها أبيض.
 */
const v = (name) => `rgb(var(--${name}) / <alpha-value>)`;
const ramp = (hue, steps) => Object.fromEntries(steps.map((s) => [s, v(`${hue}-${s}`)]));

const themed = (hue, fixed) => ({
  ...ramp(hue, [50, 100, 200, 300]),
  ...fixed,
});

module.exports = {
  content: ['../Views/**/*.cshtml', '../wwwroot/js/**/*.js'],
  darkMode: ['selector', '[data-theme="dark"]'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Cairo"', 'system-ui', '-apple-system', 'Segoe UI', 'Tahoma', 'sans-serif'],
        mono: ['ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
      },
      colors: {
        // أسطح الواجهة (تتبدل مع الوضع)
        surface: v('surface'),
        'surface-2': v('surface-2'),
        canvas: v('canvas'),
        line: v('line'),
        ink: { DEFAULT: v('ink'), 2: v('ink-2'), 3: v('ink-3') },

        slate: ramp('slate', [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950]),

        // هوية المرأة مول: أخضر زمردي داكن (مطابق لتطبيق الجوال #064E3B) مع الذهبي
        brand: themed('brand', { 400: '#34d399', 500: '#059669', 600: v('brand-600'), 700: v('brand-700'), 800: v('brand-800'), 900: '#022c22', 950: '#011a14' }),
        gold: themed('gold', { 400: '#fbbf24', 500: '#f59e0b', 600: '#d97706', 700: '#b45309', 800: '#92400e', 900: '#78350f' }),
        emerald: themed('emerald', { 400: '#34d399', 500: '#10b981', 600: '#059669', 700: '#047857', 800: '#065f46', 900: '#064e3b' }),
        amber: themed('amber', { 400: '#fbbf24', 500: '#f59e0b', 600: '#d97706', 700: '#b45309', 800: '#92400e', 900: '#78350f' }),
        rose: themed('rose', { 400: '#fb7185', 500: '#f43f5e', 600: '#e11d48', 700: '#be123c', 800: '#9f1239', 900: '#881337' }),
        purple: themed('purple', { 400: '#c084fc', 500: '#a855f7', 600: '#9333ea', 700: '#7e22ce', 800: '#6b21a8', 900: '#581c87' }),
        indigo: themed('indigo', { 400: '#818cf8', 500: '#6366f1', 600: '#4f46e5', 700: '#4338ca', 800: '#3730a3', 900: '#312e81' }),
        orange: themed('orange', { 400: '#fb923c', 500: '#f97316', 600: '#ea580c', 700: '#c2410c', 800: '#9a3412', 900: '#7c2d12' }),
      },
      boxShadow: {
        card: '0 1px 2px 0 rgb(15 23 42 / 0.04), 0 1px 3px 0 rgb(15 23 42 / 0.03)',
        pop: '0 10px 30px -10px rgb(15 23 42 / 0.25)',
      },
      keyframes: {
        'fade-in': { from: { opacity: 0, transform: 'translateY(4px)' }, to: { opacity: 1, transform: 'none' } },
        shimmer: { '100%': { transform: 'translateX(-100%)' } },
      },
      animation: {
        'fade-in': 'fade-in .25s ease-out both',
      },
    },
  },
  plugins: [],
};
