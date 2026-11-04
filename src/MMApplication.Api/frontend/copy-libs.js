// ينسخ مكتبات الواجهة والخطوط إلى wwwroot/lib لتُخدم محلياً بدون CDN
const fs = require('fs');
const path = require('path');
const out = path.join(__dirname, '..', 'wwwroot', 'lib');
const files = {
  'lucide.min.js': 'node_modules/lucide/dist/umd/lucide.min.js',
  'apexcharts.min.js': 'node_modules/apexcharts/dist/apexcharts.min.js',
  'apexcharts.css': 'node_modules/apexcharts/dist/apexcharts.css',
  'sweetalert2.all.min.js': 'node_modules/sweetalert2/dist/sweetalert2.all.min.js',
};
fs.mkdirSync(path.join(out, 'fonts'), { recursive: true });
for (const [name, src] of Object.entries(files)) fs.copyFileSync(path.join(__dirname, src), path.join(out, name));
for (const script of ['arabic', 'latin']) {
  for (const w of [400, 600, 700, 800]) {
    const f = `cairo-${script}-${w}-normal.woff2`;
    fs.copyFileSync(path.join(__dirname, 'node_modules/@fontsource/cairo/files', f), path.join(out, 'fonts', f));
  }
}
console.log('libs copied to', out);
