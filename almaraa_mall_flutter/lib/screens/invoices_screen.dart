import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart' show DateFormat;
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
import '../models/invoice.dart';
import '../widgets/common.dart';
import 'scan_tab.dart';

enum _Filter { all, review, approved, rejected }

extension on _Filter {
  String get label => switch (this) {
        _Filter.all => 'الكل',
        _Filter.review => 'قيد المراجعة',
        _Filter.approved => 'مقبولة',
        _Filter.rejected => 'تحتاج إجراء',
      };

  bool matches(Invoice i) => switch (this) {
        _Filter.all => true,
        _Filter.approved => i.status == 'APPROVED',
        _Filter.rejected => i.status == 'REJECTED' || i.status == 'RESUBMISSION_REQUESTED',
        _Filter.review => !['APPROVED', 'REJECTED', 'RESUBMISSION_REQUESTED', 'VOIDED'].contains(i.status),
      };
}

class InvoicesScreen extends StatefulWidget {
  const InvoicesScreen({super.key});

  @override
  State<InvoicesScreen> createState() => _InvoicesScreenState();
}

class _InvoicesScreenState extends State<InvoicesScreen> {
  Future<List<Invoice>>? _future;
  _Filter _filter = _Filter.all;

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

  void _load() => setState(() => _future = LoyaltyApi.getInvoices());

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Scaffold(
      appBar: AppBar(title: const Text('فواتيري')),
      body: AsyncView<List<Invoice>>(
        future: _future,
        onRetry: _load,
        builder: (context, invoices) {
          final filtered = invoices.where(_filter.matches).toList();
          // تجميع حسب الشهر
          final groups = <String, List<Invoice>>{};
          for (final inv in filtered) {
            groups.putIfAbsent('${DateFormat.MMMM('ar').format(inv.createdAt)} ${inv.createdAt.year}', () => []).add(inv);
          }
          return Column(
            children: [
              SizedBox(
                height: 48,
                child: ListView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  children: [
                    for (final f in _Filter.values)
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 4),
                        child: ChoiceChip(
                          label: Text('${f.label} (${invoices.where(f.matches).length})'),
                          selected: _filter == f,
                          onSelected: (_) => setState(() => _filter = f),
                        ),
                      ),
                  ],
                ),
              ),
              Expanded(
                child: RefreshIndicator(
                  onRefresh: () async {
                    _load();
                    await _future;
                  },
                  child: filtered.isEmpty
                      ? ListView(children: [
                          EmptyView(
                            icon: LucideIcons.receiptText,
                            title: invoices.isEmpty ? 'لم ترفع أي فاتورة بعد' : 'لا توجد فواتير بهذه الحالة',
                            subtitle: invoices.isEmpty ? 'صوّر فاتورة مشترياتك من تبويب "مسح" واكسب النقاط' : null,
                          ),
                        ])
                      : ListView(
                          padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
                          children: [
                            for (final entry in groups.entries) ...[
                              Padding(
                                padding: const EdgeInsets.fromLTRB(4, 12, 4, 8),
                                child: Text(entry.key, style: TextStyle(color: p.muted, fontWeight: FontWeight.w700, fontSize: 13)),
                              ),
                              for (var i = 0; i < entry.value.length; i++)
                                Padding(
                                  padding: const EdgeInsets.only(bottom: 10),
                                  child: FadeSlideIn(index: i, child: _InvoiceTile(invoice: entry.value[i])),
                                ),
                            ],
                          ],
                        ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _InvoiceTile extends StatelessWidget {
  final Invoice invoice;
  const _InvoiceTile({required this.invoice});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final inv = invoice;
    return AppCard(
      padding: const EdgeInsets.all(14),
      onTap: () => pushPage(context, InvoiceDetailsScreen(invoiceId: inv.id, initial: inv)),
      child: Row(
        children: [
          StoreLogo(name: inv.merchantName, size: 48),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(inv.merchantName, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w700)),
                const SizedBox(height: 3),
                Text('${inv.totalAmount > 0 ? formatMoney(inv.totalAmount) : '—'}  •  ${formatDate(inv.invoiceDate)}',
                    style: TextStyle(fontSize: 12, color: p.muted)),
                const SizedBox(height: 8),
                StatusChip.invoice(inv.status),
              ],
            ),
          ),
          if (inv.pointsEarned > 0 && inv.status == 'APPROVED')
            Column(
              children: [
                Text('+${formatPoints(inv.pointsEarned)}',
                    textDirection: TextDirection.ltr, style: TextStyle(color: p.success, fontWeight: FontWeight.w700, fontSize: 17)),
                Text('نقطة', style: TextStyle(fontSize: 11, color: p.muted)),
              ],
            ),
          const SizedBox(width: 4),
          Icon(LucideIcons.chevronLeft, color: p.muted, size: 20),
        ],
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

  void _reload() => setState(() => _future = LoyaltyApi.getInvoice(widget.invoiceId));

  Future<void> _dispute() async {
    final controller = TextEditingController();
    final reason = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => Padding(
        padding: EdgeInsets.fromLTRB(24, 0, 24, 24 + MediaQuery.of(ctx).viewInsets.bottom),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('الاعتراض على الرفض', style: Theme.of(ctx).textTheme.titleLarge),
            const SizedBox(height: 6),
            Text('اشرح لنا لماذا تعتقد أن الفاتورة صحيحة، وسيراجعها فريق المول.', style: TextStyle(color: ctx.palette.muted)),
            const SizedBox(height: 16),
            TextField(
              controller: controller,
              autofocus: true,
              maxLines: 4,
              maxLength: 500,
              decoration: const InputDecoration(hintText: 'مثال: الفاتورة أصلية وتم الشراء من المتجر في تاريخها'),
            ),
            const SizedBox(height: 8),
            ElevatedButton(onPressed: () => Navigator.pop(ctx, controller.text.trim()), child: const Text('إرسال الاعتراض')),
          ],
        ),
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
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ListTile(
                leading: const IconBadge(LucideIcons.camera, size: 40),
                title: const Text('تصوير الفاتورة من جديد', style: TextStyle(fontWeight: FontWeight.w700)),
                onTap: () => Navigator.pop(ctx, ImageSource.camera),
              ),
              ListTile(
                leading: const IconBadge(LucideIcons.image, tone: Tone.info, size: 40),
                title: const Text('اختيار من المعرض', style: TextStyle(fontWeight: FontWeight.w700)),
                onTap: () => Navigator.pop(ctx, ImageSource.gallery),
              ),
            ],
          ),
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
    final p = context.palette;
    return Scaffold(
      appBar: AppBar(title: const Text('تفاصيل الفاتورة')),
      body: AsyncView<Invoice>(
        future: _future,
        onRetry: _reload,
        builder: (context, inv) {
          final image = AppConstants.resolveImageUrl(inv.imageUrl);
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 4, 16, 28),
            children: [
              AppCard(
                padding: const EdgeInsets.all(18),
                child: Column(
                  children: [
                    StoreLogo(name: inv.merchantName, size: 64),
                    const SizedBox(height: 12),
                    Text(inv.merchantName, textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium),
                    const SizedBox(height: 8),
                    StatusChip.invoice(inv.status),
                    if (inv.pointsEarned > 0 && inv.status == 'APPROVED') ...[
                      const SizedBox(height: 14),
                      Text('+${formatPoints(inv.pointsEarned)} نقطة',
                          textDirection: TextDirection.ltr, style: TextStyle(color: p.success, fontWeight: FontWeight.w700, fontSize: 24)),
                    ],
                    const SizedBox(height: 14),
                    const Divider(),
                    const SizedBox(height: 6),
                    if (inv.invoiceNumber.isNotEmpty) InfoRow('رقم الفاتورة', inv.invoiceNumber, ltr: true),
                    InfoRow('تاريخ الفاتورة', formatDate(inv.invoiceDate)),
                    InfoRow('المبلغ الإجمالي', inv.totalAmount > 0 ? formatMoney(inv.totalAmount) : '—'),
                    InfoRow('تاريخ الرفع', formatDateTime(inv.createdAt)),
                  ],
                ),
              ),
              if (inv.rejectionReason != null) ...[
                const SizedBox(height: 12),
                Container(
                  padding: const EdgeInsets.all(14),
                  decoration: BoxDecoration(color: p.dangerSoft, borderRadius: BorderRadius.circular(AppRadius.md)),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(LucideIcons.circleAlert, color: p.danger, size: 20),
                      const SizedBox(width: 10),
                      Expanded(child: Text(inv.rejectionReason!, style: TextStyle(color: p.danger, fontWeight: FontWeight.w600, height: 1.5))),
                    ],
                  ),
                ),
              ],
              if (inv.canResubmit || inv.canDispute) ...[
                const SizedBox(height: 16),
                if (inv.canResubmit)
                  ElevatedButton.icon(
                    onPressed: _busy ? null : _resubmit,
                    icon: const Icon(LucideIcons.camera, size: 20),
                    label: const Text('إعادة رفع صورة أوضح'),
                  ),
                if (inv.canDispute) ...[
                  const SizedBox(height: 10),
                  OutlinedButton.icon(
                    onPressed: _busy ? null : _dispute,
                    icon: const Icon(LucideIcons.messageSquare, size: 20),
                    label: const Text('الاعتراض على الرفض'),
                  ),
                ],
                if (_busy) const Padding(padding: EdgeInsets.only(top: 16), child: LoadingView()),
              ],
              if (image != null) ...[
                const SizedBox(height: 20),
                const SectionTitle('صورة الفاتورة'),
                ClipRRect(
                  borderRadius: BorderRadius.circular(AppRadius.lg),
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
}
