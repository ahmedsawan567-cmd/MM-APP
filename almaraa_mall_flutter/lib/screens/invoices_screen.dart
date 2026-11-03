import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../models/invoice.dart';
import '../widgets/common.dart';
import 'scan_tab.dart';

class InvoicesScreen extends StatefulWidget {
  const InvoicesScreen({super.key});

  @override
  State<InvoicesScreen> createState() => _InvoicesScreenState();
}

class _InvoicesScreenState extends State<InvoicesScreen> {
  Future<List<Invoice>>? _future;

  @override
  void initState() {
    super.initState();
    _load();
    Session.instance.dataVersion.addListener(_load);
  }

  @override
  void dispose() {
    Session.instance.dataVersion.removeListener(_load);
    super.dispose();
  }

  void _load() {
    setState(() => _future = LoyaltyApi.getInvoices());
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('سجل الفواتير')),
      body: AsyncView<List<Invoice>>(
        future: _future,
        onRetry: _load,
        builder: (context, invoices) => RefreshIndicator(
          color: AppConstants.primaryGreen,
          onRefresh: () async {
            _load();
            await _future;
          },
          child: invoices.isEmpty
              ? ListView(children: const [
                  EmptyView(
                    icon: LucideIcons.receipt,
                    title: 'لم ترفع أي فاتورة بعد',
                    subtitle: 'صوّر فاتورة مشترياتك من تبويب "مسح فاتورة" واكسب النقاط',
                  ),
                ])
              : ListView.builder(
                  padding: const EdgeInsets.all(16),
                  itemCount: invoices.length,
                  itemBuilder: (_, i) => _InvoiceTile(invoice: invoices[i]),
                ),
        ),
      ),
    );
  }
}

class _InvoiceTile extends StatelessWidget {
  final Invoice invoice;
  const _InvoiceTile({required this.invoice});

  @override
  Widget build(BuildContext context) {
    final inv = invoice;
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () => Navigator.of(context)
            .push(MaterialPageRoute(builder: (_) => InvoiceDetailsScreen(invoiceId: inv.id, initial: inv))),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              StoreLogo(name: inv.merchantName, size: 46),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(inv.merchantName,
                        maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.bold)),
                    const SizedBox(height: 2),
                    Text('${formatMoney(inv.totalAmount)} • ${formatDate(inv.invoiceDate)}',
                        style: const TextStyle(fontSize: 12, color: AppConstants.textMuted)),
                    const SizedBox(height: 6),
                    StatusChip.invoice(inv.status),
                  ],
                ),
              ),
              if (inv.pointsEarned > 0)
                Text('+${formatPoints(inv.pointsEarned)}',
                    textDirection: TextDirection.ltr,
                    style: const TextStyle(color: AppConstants.successColor, fontWeight: FontWeight.bold, fontSize: 16)),
              const SizedBox(width: 4),
              const Icon(Icons.chevron_left, color: AppConstants.textMuted),
            ],
          ),
        ),
      ),
    );
  }
}

class InvoiceDetailsScreen extends StatefulWidget {
  final String invoiceId;
  final Invoice? initial;

  const InvoiceDetailsScreen({super.key, required this.invoiceId, this.initial});

  @override
  State<InvoiceDetailsScreen> createState() => _InvoiceDetailsScreenState();
}

class _InvoiceDetailsScreenState extends State<InvoiceDetailsScreen> {
  late Future<Invoice> _future;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _future = widget.initial != null ? Future.value(widget.initial!) : LoyaltyApi.getInvoice(widget.invoiceId);
  }

  void _reload() {
    setState(() => _future = LoyaltyApi.getInvoice(widget.invoiceId));
  }

  Future<void> _dispute() async {
    final controller = TextEditingController();
    final reason = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('الاعتراض على الرفض'),
        content: TextField(
          controller: controller,
          maxLines: 4,
          maxLength: 500,
          decoration: const InputDecoration(
            hintText: 'اشرح سبب اعتراضك (مثلاً: الفاتورة أصلية وتم الشراء من المتجر في تاريخها)',
            border: OutlineInputBorder(),
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('إلغاء')),
          ElevatedButton(onPressed: () => Navigator.pop(ctx, controller.text.trim()), child: const Text('إرسال')),
        ],
      ),
    );
    controller.dispose();
    if (reason == null) return;
    if (reason.length < 5) {
      if (mounted) showMessage(context, 'يرجى كتابة سبب واضح للاعتراض.', error: true);
      return;
    }
    await _run(() => LoyaltyApi.disputeInvoice(widget.invoiceId, reason));
  }

  Future<void> _resubmit() async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(LucideIcons.camera),
              title: const Text('تصوير الفاتورة من جديد'),
              onTap: () => Navigator.pop(ctx, ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(LucideIcons.image),
              title: const Text('اختيار من المعرض'),
              onTap: () => Navigator.pop(ctx, ImageSource.gallery),
            ),
          ],
        ),
      ),
    );
    if (source == null || !mounted) return;
    final file = await pickReceiptImage(context, source);
    if (file == null) return;
    await _run(() => LoyaltyApi.resubmitInvoice(widget.invoiceId, file));
  }

  Future<void> _run(Future<String> Function() action) async {
    setState(() => _busy = true);
    try {
      final message = await action();
      Session.instance.notifyDataChanged();
      if (!mounted) return;
      showMessage(context, message);
      _reload();
    } catch (e) {
      if (mounted) showMessage(context, e.toString(), error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('تفاصيل الفاتورة')),
      body: AsyncView<Invoice>(
        future: _future,
        onRetry: _reload,
        builder: (context, inv) {
          final image = AppConstants.resolveImageUrl(inv.imageUrl);
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    children: [
                      Row(
                        children: [
                          StoreLogo(name: inv.merchantName, size: 52),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Text(inv.merchantName,
                                style: const TextStyle(fontSize: 17, fontWeight: FontWeight.bold)),
                          ),
                          StatusChip.invoice(inv.status),
                        ],
                      ),
                      const Divider(height: 28),
                      _row('رقم الفاتورة', inv.invoiceNumber),
                      _row('تاريخ الفاتورة', formatDate(inv.invoiceDate)),
                      _row('المبلغ الإجمالي', formatMoney(inv.totalAmount)),
                      _row('النقاط المكتسبة', inv.pointsEarned > 0 ? '+${formatPoints(inv.pointsEarned)}' : '—'),
                      _row('تاريخ الرفع', formatDateTime(inv.createdAt)),
                    ],
                  ),
                ),
              ),
              if (inv.rejectionReason != null) ...[
                const SizedBox(height: 12),
                Container(
                  padding: const EdgeInsets.all(14),
                  decoration: BoxDecoration(
                    color: AppConstants.dangerColor.withOpacity(0.08),
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(LucideIcons.alertCircle, color: AppConstants.dangerColor, size: 20),
                      const SizedBox(width: 10),
                      Expanded(child: Text('السبب: ${inv.rejectionReason}')),
                    ],
                  ),
                ),
              ],
              if (inv.canResubmit || inv.canDispute) ...[
                const SizedBox(height: 16),
                if (inv.canResubmit)
                  ElevatedButton.icon(
                    onPressed: _busy ? null : _resubmit,
                    icon: const Icon(LucideIcons.camera),
                    label: const Text('إعادة رفع صورة أوضح'),
                  ),
                if (inv.canDispute) ...[
                  const SizedBox(height: 10),
                  OutlinedButton.icon(
                    onPressed: _busy ? null : _dispute,
                    icon: const Icon(LucideIcons.messageSquare),
                    label: const Text('الاعتراض على الرفض'),
                    style: OutlinedButton.styleFrom(padding: const EdgeInsets.symmetric(vertical: 14)),
                  ),
                ],
                if (_busy) const Padding(padding: EdgeInsets.only(top: 16), child: LoadingView()),
              ],
              if (image != null) ...[
                const SizedBox(height: 16),
                const Text('صورة الفاتورة', style: TextStyle(fontWeight: FontWeight.bold)),
                const SizedBox(height: 8),
                ClipRRect(
                  borderRadius: BorderRadius.circular(14),
                  child: Image.network(
                    image,
                    fit: BoxFit.contain,
                    errorBuilder: (_, __, ___) => const EmptyView(icon: LucideIcons.imageOff, title: 'تعذر تحميل الصورة'),
                  ),
                ),
              ],
            ],
          );
        },
      ),
    );
  }

  Widget _row(String label, String value) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(label, style: const TextStyle(color: AppConstants.textMuted)),
            Flexible(child: Text(value, style: const TextStyle(fontWeight: FontWeight.w600), textAlign: TextAlign.end)),
          ],
        ),
      );
}
