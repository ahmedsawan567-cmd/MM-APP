import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
import '../widgets/common.dart';

/// بعد مسح باركود المتجر أو قراءة بطاقة NFC: العميل يدخل مبلغ الشراء ويُرسل الطلب لكاشير المتجر.
class StoreClaimScreen extends StatefulWidget {
  final String code;
  final String method; // QR أو NFC
  const StoreClaimScreen({super.key, required this.code, required this.method});

  @override
  State<StoreClaimScreen> createState() => _StoreClaimScreenState();
}

class _StoreClaimScreenState extends State<StoreClaimScreen> {
  late final Future<Map<String, dynamic>> _store = LoyaltyApi.resolveStoreCode(widget.code);
  final _amount = TextEditingController();
  final _note = TextEditingController();
  bool _sending = false;

  @override
  void dispose() {
    _amount.dispose();
    _note.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final amount = double.tryParse(_amount.text.trim().replaceAll(',', '.'));
    if (amount == null || amount <= 0) {
      showMessage(context, 'اكتب مبلغ الشراء كما في جهاز الكاشير.', error: true);
      return;
    }
    setState(() => _sending = true);
    try {
      final r = await LoyaltyApi.claimPurchase(code: widget.code, amount: amount, method: widget.method, note: _note.text.trim().isEmpty ? null : _note.text.trim());
      Session.instance.notifyDataChanged();
      if (!mounted) return;
      await showModalBottomSheet(
        context: context,
        builder: (ctx) => Padding(
          padding: const EdgeInsets.fromLTRB(24, 8, 24, 28),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const IconBadge(LucideIcons.send, tone: Tone.success, size: 64),
              const SizedBox(height: 14),
              Text('أُرسل طلبك', style: Theme.of(ctx).textTheme.titleLarge),
              const SizedBox(height: 8),
              Text('${r['message'] ?? ''}', textAlign: TextAlign.center, style: TextStyle(color: ctx.palette.muted, height: 1.6)),
              const SizedBox(height: 20),
              SizedBox(width: double.infinity, child: ElevatedButton(onPressed: () => Navigator.pop(ctx), child: const Text('تم'))),
            ],
          ),
        ),
      );
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) showMessage(context, e.toString(), error: true);
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Scaffold(
      appBar: AppBar(title: Text(widget.method == 'NFC' ? 'بطاقة المتجر' : 'باركود المتجر')),
      body: FutureBuilder<Map<String, dynamic>>(
        future: _store,
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
          if (snap.hasError) {
            return EmptyView(icon: LucideIcons.circleAlert, title: 'تعذّر التعرف على المتجر', subtitle: snap.error.toString());
          }
          final store = Map<String, dynamic>.from(snap.data!['store'] as Map);
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
            children: [
              AppCard(
                child: Row(children: [
                  StoreLogo(name: '${store['nameAr']}', logoUrl: store['logoUrl'] as String?, size: 60),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text('${store['nameAr']}', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
                      Text('${store['category'] ?? ''}', style: TextStyle(color: p.muted)),
                    ]),
                  ),
                  const Icon(LucideIcons.badgeCheck, color: Colors.green),
                ]),
              ),
              const SizedBox(height: 20),
              const Text('كم دفعت؟', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
              const SizedBox(height: 8),
              TextField(
                controller: _amount,
                autofocus: true,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))],
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 28, fontWeight: FontWeight.w700),
                decoration: const InputDecoration(hintText: '0.00', suffixText: 'د.ل'),
              ),
              const SizedBox(height: 14),
              TextField(
                controller: _note,
                maxLength: 200,
                decoration: const InputDecoration(labelText: 'ملاحظة للكاشير (اختياري)', hintText: 'مثال: رقم الإيصال أو ما اشتريته'),
              ),
              const SizedBox(height: 8),
              AppCard(
                color: p.brandSoft,
                child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Icon(LucideIcons.info, size: 18, color: p.brand),
                  const SizedBox(width: 10),
                  const Expanded(
                    child: Text('يصل طلبك لكاشير المتجر ليتأكد من المبلغ، وتُضاف نقاطك فور تأكيده. اطلب منه التأكيد قبل أن تغادر.',
                        style: TextStyle(height: 1.6)),
                  ),
                ]),
              ),
              const SizedBox(height: 20),
              SizedBox(
                height: 52,
                child: ElevatedButton.icon(
                  onPressed: _sending ? null : _send,
                  icon: _sending ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(LucideIcons.send),
                  label: Text(_sending ? 'جارٍ الإرسال...' : 'إرسال الطلب للكاشير'),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
