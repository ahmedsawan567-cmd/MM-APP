/** إعداد Tailwind للوحة التحكم — يفحص صفحات Razor ويولّد CSS يحتوي فقط ما تستخدمه الصفحات. */
module.exports = {
  content: ['../Views/**/*.cshtml', '../wwwroot/js/**/*.js'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Cairo"', 'system-ui', '-apple-system', 'Segoe UI', 'Tahoma', 'sans-serif'],
        mono: ['ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
      },
      colors: {
        // هوية المرأة مول: أخضر زمردي داكن (مطابق لتطبيق الجوال #064E3B) مع الذهبي
        brand: {
          50: '#ecfdf5',
          100: '#d1fae5',
          200: '#a7f3d0',
          300: '#6ee7b7',
          400: '#34d399',
          500: '#059669',
          600: '#047857',
          700: '#065f46',
          800: '#064e3b',
          900: '#022c22',
          950: '#011a14',
        },
        gold: {
          50: '#fffbeb',
          100: '#fef3c7',
          400: '#fbbf24',
          500: '#f59e0b',
          600: '#d97706',
          700: '#b45309',
        },
      },
    },
  },
  plugins: [],
};
