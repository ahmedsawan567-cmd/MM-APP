import 'dart:io';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../models/invoice.dart';
import '../widgets/common.dart';
import 'invoices_screen.dart';

/// اختيار صورة فاتورة من الكاميرا أو المعرض بحجم مناسب للرفع.
Future<File?> pickReceiptImage(BuildContext context, ImageSource source) async {
  try {
    final picked = await ImagePicker().pickImage(
      source: source,
      imageQuality: 85,
      maxWidth: 2000,
      maxHeight: 2000,
    );
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

class _ScanTabState extends State<ScanTab> {
  File? _image;
  bool _uploading = false;

  Future<void> _pick(ImageSource source) async {
    final file = await pickReceiptImage(context, source);
    if (file != null) setState(() => _image = file);
  }

  Future<void> _upload() async {
    final image = _image;
    if (image == null) return;
    setState(() => _uploading = true);
    try {
      final result = await LoyaltyApi.uploadInvoice(image);
      Session.instance.notifyDataChanged();
      if (!mounted) return;
      setState(() => _image = null);
      await _showResult(result);
    } catch (e) {
      if (mounted) showMessage(context, e.toString(), error: true);
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  Future<void> _showResult(InvoiceUploadResult r) {
    final approved = r.isApproved;
    final inv = r.invoice;
    return showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(24))),
      builder: (ctx) => Padding(
        padding: const EdgeInsets.fromLTRB(24, 24, 24, 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: (approved ? AppConstants.successColor : AppConstants.accentGold).withOpacity(0.12),
                shape: BoxShape.circle,
              ),
              child: Icon(approved ? LucideIcons.checkCircle2 : LucideIcons.clock,
                  color: approved ? AppConstants.successColor : AppConstants.accentGold, size: 40),
            ),
            const SizedBox(height: 16),
            Text(approved ? 'تمت إضافة النقاط!' : 'تم استلام الفاتورة',
                style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            Text(r.message, textAlign: TextAlign.center, style: const TextStyle(color: AppConstants.textMuted)),
            if (approved && r.pointsEarned > 0) ...[
              const SizedBox(height: 16),
              Text('+${formatPoints(r.pointsEarned)} نقطة',
                  textDirection: TextDirection.ltr,
                  style: const TextStyle(fontSize: 28, fontWeight: FontWeight.bold, color: AppConstants.successColor)),
            ],
            if (inv != null) ...[
              const SizedBox(height: 16),
              _row('المتجر', inv.merchantName),
              _row('رقم الفاتورة', inv.invoiceNumber),
              _row('المبلغ', formatMoney(inv.totalAmount)),
            ],
            _row('رصيدك الحالي', '${formatPoints(r.newBalance)} نقطة'),
            const SizedBox(height: 20),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: () {
                      Navigator.pop(ctx);
                      Navigator.of(context).push(MaterialPageRoute(builder: (_) => const InvoicesScreen()));
                    },
                    style: OutlinedButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
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
      ),
    );
  }

  Widget _row(String label, String value) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(label, style: const TextStyle(color: AppConstants.textMuted)),
            Flexible(child: Text(value, style: const TextStyle(fontWeight: FontWeight.bold), textAlign: TextAlign.end)),
          ],
        ),
      );

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('مسح فاتورة'),
        actions: [
          IconButton(
            tooltip: 'سجل الفواتير',
            icon: const Icon(LucideIcons.history),
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const InvoicesScreen())),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _preview(),
          const SizedBox(height: 16),
          if (_image == null) ...[
            ElevatedButton.icon(
              onPressed: () => _pick(ImageSource.camera),
              icon: const Icon(LucideIcons.camera),
              label: const Text('تصوير الفاتورة'),
            ),
            const SizedBox(height: 10),
            OutlinedButton.icon(
              onPressed: () => _pick(ImageSource.gallery),
              icon: const Icon(LucideIcons.image),
              label: const Text('اختيار من المعرض'),
              style: OutlinedButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
            ),
          ] else ...[
            ElevatedButton.icon(
              onPressed: _uploading ? null : _upload,
              icon: _uploading
                  ? const SizedBox(
                      width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                  : const Icon(LucideIcons.uploadCloud),
              label: Text(_uploading ? 'جاري قراءة الفاتورة...' : 'إرسال الفاتورة'),
            ),
            const SizedBox(height: 10),
            OutlinedButton.icon(
              onPressed: _uploading ? null : () => setState(() => _image = null),
              icon: const Icon(LucideIcons.rotateCcw),
              label: const Text('اختيار صورة أخرى'),
              style: OutlinedButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
            ),
          ],
          const SizedBox(height: 24),
          _tips(),
        ],
      ),
    );
  }

  Widget _preview() {
    return AspectRatio(
      aspectRatio: 3 / 4,
      child: Container(
        clipBehavior: Clip.antiAlias,
        decoration: BoxDecoration(
          color: AppConstants.bgDark,
          borderRadius: BorderRadius.circular(20),
        ),
        child: _image != null
            ? Image.file(_image!, fit: BoxFit.contain)
            : Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(LucideIcons.scanLine, size: 72, color: AppConstants.accentGoldLight.withOpacity(0.9)),
                  const SizedBox(height: 16),
                  const Text('صوّر فاتورة الشراء لتحصل على نقاط',
                      style: TextStyle(color: Colors.white, fontSize: 16, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 6),
                  Text('سيتم قراءة المتجر والمبلغ تلقائياً',
                      style: TextStyle(color: Colors.white.withOpacity(0.7), fontSize: 13)),
                ],
              ),
      ),
    );
  }

  Widget _tips() {
    const tips = [
      'تأكد أن الفاتورة كاملة وواضحة وغير مقطوعة.',
      'صوّر في مكان جيد الإضاءة وبدون انعكاسات.',
      'يجب أن يظهر اسم المتجر ورقم الفاتورة والمبلغ الإجمالي.',
      'لا يمكن استخدام نفس الفاتورة أكثر من مرة.',
    ];
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Row(
              children: [
                Icon(LucideIcons.lightbulb, color: AppConstants.accentGold, size: 20),
                SizedBox(width: 8),
                Text('نصائح لقبول الفاتورة', style: TextStyle(fontWeight: FontWeight.bold)),
              ],
            ),
            const SizedBox(height: 10),
            ...tips.map((t) => Padding(
                  padding: const EdgeInsets.symmetric(vertical: 3),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('•  ', style: TextStyle(color: AppConstants.primaryGreen)),
                      Expanded(child: Text(t, style: const TextStyle(fontSize: 13))),
                    ],
                  ),
                )),
          ],
        ),
      ),
    );
  }
}
