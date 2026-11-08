import 'dart:io';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
import '../models/invoice.dart';
import '../widgets/common.dart';
import '../core/services/store_code_reader.dart';
import 'invoices_screen.dart';
import 'store_claim_screen.dart';
import 'store_qr_scanner_screen.dart';

/// اختيار صورة فاتورة من الكاميرا أو المعرض بحجم مناسب للرفع.
Future<File?> pickReceiptImage(BuildContext context, ImageSource source) async {
  try {
    final picked = await ImagePicker().pickImage(source: source, imageQuality: 85, maxWidth: 2000, maxHeight: 2000);
    return picked == null ? null : File(picked.path);
  } catch (_) {
    if (context.mounted) {
      showMessage(context, 'تعذر الوصول إلى الكاميرا أو المعرض. تحقق من صلاحيات التطبيق.', error: true);
    }
    return null;
  }
}

class ScanTab extends StatefulWidget {
  final void Function(int) onTabChange;
  const ScanTab({super.key, required this.onTabChange});

  @override
  State<ScanTab> createState() => _ScanTabState();
}

class _ScanTabState extends State<ScanTab> with SingleTickerProviderStateMixin {
  File? _image;
  bool _uploading = false;
  late final AnimationController _scan = AnimationController(vsync: this, duration: const Duration(milliseconds: 1600));

  @override
  void dispose() {
    _scan.dispose();
    super.dispose();
  }

  Future<void> _pick(ImageSource source) async {
    final file = await pickReceiptImage(context, source);
    if (file != null) setState(() => _image = file);
  }

  Future<void> _upload() async {
    final image = _image;
    if (image == null) return;
    setState(() => _uploading = true);
    _scan.repeat(reverse: true);
    try {
      final result = await LoyaltyApi.uploadInvoice(image);
      Session.instance.notifyDataChanged();
      if (!mounted) return;
      setState(() => _image = null);
      await _showResult(result);
    } catch (e) {
      if (mounted) showMessage(context, e.toString(), error: true);
    } finally {
      _scan.stop();
      if (mounted) setState(() => _uploading = false);
    }
  }

  Future<void> _showResult(InvoiceUploadResult r) {
    final approved = r.isApproved;
    final inv = r.invoice;
    return showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (ctx) {
        final p = ctx.palette;
        final tone = approved ? Tone.success : Tone.gold;
        final (fg, bg) = tone.colors(p);
        return Padding(
          padding: const EdgeInsets.fromLTRB(24, 0, 24, 28),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TweenAnimationBuilder<double>(
                tween: Tween(begin: 0.6, end: 1),
                duration: const Duration(milliseconds: 500),
                curve: Curves.easeOutBack,
                builder: (_, v, child) => Transform.scale(scale: v, child: child),
                child: Container(
                  width: 84,
                  height: 84,
                  decoration: BoxDecoration(color: bg, shape: BoxShape.circle),
                  child: Icon(approved ? LucideIcons.circleCheck : LucideIcons.clock, color: fg, size: 42),
                ),
              ),
              const SizedBox(height: 16),
              Text(approved ? 'تمت إضافة النقاط' : 'استلمنا فاتورتك', style: Theme.of(ctx).textTheme.titleLarge),
              const SizedBox(height: 6),
              Text(
                approved ? r.message : 'ستُراجع الفاتورة خلال وقت قصير وتُضاف النقاط بعد اعتمادها.',
                textAlign: TextAlign.center,
                style: TextStyle(color: p.muted, height: 1.5),
              ),
              if (approved && r.pointsEarned > 0) ...[
                const SizedBox(height: 14),
                AnimatedCount(
                  value: r.pointsEarned,
                  style: TextStyle(fontSize: 40, fontWeight: FontWeight.w700, color: p.success),
                ),
                Text('نقطة جديدة', style: TextStyle(color: p.success, fontWeight: FontWeight.w700)),
              ],
              const SizedBox(height: 18),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                decoration: BoxDecoration(color: p.surfaceAlt, borderRadius: BorderRadius.circular(AppRadius.md)),
                child: Column(
                  children: [
                    if (inv != null) ...[
                      InfoRow('المتجر', inv.merchantName),
                      if (inv.invoiceNumber.isNotEmpty) InfoRow('رقم الفاتورة', inv.invoiceNumber, ltr: true),
                      if (inv.totalAmount > 0) InfoRow('المبلغ', formatMoney(inv.totalAmount)),
                    ],
                    InfoRow('رصيدك الحالي', '${formatPoints(r.newBalance)} نقطة'),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () {
                        Navigator.pop(ctx);
                        pushPage(context, const InvoicesScreen());
                      },
                      child: const Text('سجل الفواتير'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: ElevatedButton(
                      onPressed: () {
                        Navigator.pop(ctx);
                        widget.onTabChange(3);
                      },
                      child: const Text('المحفظة'),
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Scaffold(
      appBar: AppBar(
        title: const Text('اكسب نقاطك'),
        actions: [
          IconButton(
            tooltip: 'سجل الفواتير',
            icon: const Icon(LucideIcons.history),
            onPressed: () => pushPage(context, const InvoicesScreen()),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
        children: [
          _methods(p),
          const SizedBox(height: 18),
          Text('تصوير الفاتورة', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 10),
          _viewfinder(p),
          const SizedBox(height: 16),
          AnimatedSwitcher(
            duration: const Duration(milliseconds: 250),
            child: _image == null
                ? Row(
                    key: const ValueKey('pick'),
                    children: [
                      Expanded(
                        child: ElevatedButton.icon(
                          onPressed: () => _pick(ImageSource.camera),
                          icon: const Icon(LucideIcons.camera, size: 20),
                          label: const Text('التقط صورة'),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: () => _pick(ImageSource.gallery),
                          icon: const Icon(LucideIcons.image, size: 20),
                          label: const Text('من المعرض'),
                        ),
                      ),
                    ],
                  )
                : Column(
                    key: const ValueKey('send'),
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      ElevatedButton.icon(
                        onPressed: _uploading ? null : _upload,
                        icon: _uploading
                            ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                            : const Icon(LucideIcons.send, size: 20),
                        label: Text(_uploading ? 'جاري قراءة الفاتورة...' : 'إرسال الفاتورة'),
                      ),
                      const SizedBox(height: 10),
                      TextButton.icon(
                        onPressed: _uploading ? null : () => setState(() => _image = null),
                        icon: const Icon(LucideIcons.rotateCcw, size: 18),
                        label: const Text('اختيار صورة أخرى'),
                      ),
                    ],
                  ),
          ),
          const SizedBox(height: 22),
          _tips(p),
        ],
      ),
    );
  }

  /// ثلاث طرق لكسب النقاط: تصوير الفاتورة، باركود المتجر، بطاقة NFC.
  Widget _methods(AppPalette p) {
    Widget card(IconData icon, String title, String sub, Tone tone, VoidCallback onTap) => Expanded(
          child: AppCard(
            padding: const EdgeInsets.all(14),
            onTap: onTap,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                IconBadge(icon, tone: tone, size: 42),
                const SizedBox(height: 10),
                Text(title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                const SizedBox(height: 2),
                Text(sub, style: TextStyle(fontSize: 12, color: p.muted, height: 1.4)),
              ],
            ),
          ),
        );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('متجر بدون فاتورة؟', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 4),
        Text('امسح باركود المتجر أو قرّب هاتفك من بطاقته، وتنزل النقاط في رصيدك فوراً.',
            style: TextStyle(color: p.muted, fontSize: 13)),
        const SizedBox(height: 10),
        Row(children: [
          card(LucideIcons.qrCode, 'باركود المتجر', 'امسح الكود عند الكاشير', Tone.brand, _scanStoreQr),
          const SizedBox(width: 10),
          card(LucideIcons.nfc, 'بطاقة NFC', 'قرّب الهاتف من البطاقة', Tone.info, _readNfc),
        ]),
      ],
    );
  }

  Future<void> _openClaim(String code, String method) => claimStorePoints(context, code: code, method: method);

  Future<void> _scanStoreQr() async {
    final code = await pushPage<String>(context, const StoreQrScannerScreen());
    if (code != null && mounted) await _openClaim(code, 'QR');
  }

  Future<void> _readNfc() async {
    if (!await StoreNfcReader.isAvailable()) {
      if (mounted) showMessage(context, 'قارئ NFC غير متاح أو غير مفعّل في هاتفك. فعّله من الإعدادات أو استخدم باركود المتجر.', error: true);
      return;
    }
    if (!mounted) return;
    // نافذة انتظار أثناء تقريب البطاقة
    var cancelled = false;
    showModalBottomSheet(
      context: context,
      isDismissible: true,
      builder: (ctx) => Padding(
        padding: const EdgeInsets.fromLTRB(24, 8, 24, 32),
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          const IconBadge(LucideIcons.nfc, tone: Tone.info, size: 72),
          const SizedBox(height: 16),
          Text('قرّب هاتفك من بطاقة المتجر', style: Theme.of(ctx).textTheme.titleLarge),
          const SizedBox(height: 8),
          Text('ضع ظهر الهاتف على البطاقة حتى تسمع إشارة القراءة', style: TextStyle(color: ctx.palette.muted)),
          const SizedBox(height: 18),
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('إلغاء')),
        ]),
      ),
    ).whenComplete(() {
      cancelled = true;
      StoreNfcReader.cancel();
    });
    final text = await StoreNfcReader.read();
    if (!mounted) return;
    if (!cancelled) Navigator.of(context).pop();
    if (text == null) {
      if (!cancelled) showMessage(context, 'لم تُقرأ البطاقة. حاول مرة أخرى أو امسح باركود المتجر.', error: true);
      return;
    }
    if (!text.toUpperCase().contains('MMALL-STORE')) {
      showMessage(context, 'هذه البطاقة لا تخص متجراً في المرأة مول.', error: true);
      return;
    }
    await _openClaim(text, 'NFC');
  }

  Widget _viewfinder(AppPalette p) {
    const gold = Brand.magenta;
    return AspectRatio(
      aspectRatio: 4 / 5,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(AppRadius.xl),
        child: Stack(
          fit: StackFit.expand,
          children: [
            const DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  colors: [Brand.navy, Brand.night],
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                ),
              ),
            ),
            if (_image != null) Image.file(_image!, fit: BoxFit.contain),
            if (_image == null)
              Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(
                      width: 76,
                      height: 76,
                      decoration: BoxDecoration(color: gold.withValues(alpha: 0.12), shape: BoxShape.circle),
                      child: const Icon(LucideIcons.receiptText, size: 36, color: gold),
                    ),
                    const SizedBox(height: 18),
                    const Text('صوّر فاتورة الشراء',
                        style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.w700)),
                    const SizedBox(height: 6),
                    Text('نقرأ المتجر والمبلغ تلقائياً ونضيف نقاطك',
                        style: TextStyle(color: Colors.white.withValues(alpha: 0.65), fontSize: 13)),
                  ],
                ),
              ),
            // إطار الزوايا
            const Padding(padding: EdgeInsets.all(22), child: CustomPaint(painter: _CornersPainter(gold))),
            // خط المسح أثناء الرفع
            if (_uploading)
              AnimatedBuilder(
                animation: _scan,
                builder: (_, __) => Align(
                  alignment: Alignment(0, -0.85 + 1.7 * _scan.value),
                  child: Container(
                    height: 3,
                    margin: const EdgeInsets.symmetric(horizontal: 28),
                    decoration: BoxDecoration(
                      color: Brand.magenta,
                      boxShadow: [BoxShadow(color: Brand.magenta.withValues(alpha: 0.7), blurRadius: 16, spreadRadius: 2)],
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _tips(AppPalette p) {
    const tips = [
      (LucideIcons.maximize, 'الفاتورة كاملة وغير مقطوعة'),
      (LucideIcons.sun, 'إضاءة جيدة وبدون انعكاسات'),
      (LucideIcons.fileText, 'يظهر اسم المتجر ورقم الفاتورة والمبلغ'),
      (LucideIcons.copyX, 'كل فاتورة تُستخدم مرة واحدة فقط'),
    ];
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(LucideIcons.lightbulb, color: p.gold, size: 20),
              const SizedBox(width: 8),
              const Text('لقبول الفاتورة بسرعة', style: TextStyle(fontWeight: FontWeight.w700)),
            ],
          ),
          const SizedBox(height: 12),
          for (final (icon, text) in tips)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 5),
              child: Row(
                children: [
                  Icon(icon, size: 16, color: p.brand),
                  const SizedBox(width: 10),
                  Expanded(child: Text(text, style: TextStyle(fontSize: 13, color: p.muted))),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// يرسم زوايا إطار المسح.
class _CornersPainter extends CustomPainter {
  final Color color;
  const _CornersPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = 4
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;
    const l = 34.0;
    final w = size.width, h = size.height;
    final path = Path()
      ..moveTo(0, l)..lineTo(0, 0)..lineTo(l, 0)
      ..moveTo(w - l, 0)..lineTo(w, 0)..lineTo(w, l)
      ..moveTo(w, h - l)..lineTo(w, h)..lineTo(w - l, h)
      ..moveTo(l, h)..lineTo(0, h)..lineTo(0, h - l);
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant _CornersPainter old) => old.color != color;
}
