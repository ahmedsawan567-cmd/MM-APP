import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../models/wallet.dart';
import '../widgets/common.dart';
import 'rewards_screen.dart';

class _WalletData {
  final WalletSummary summary;
  final List<PointTransaction> transactions;
  final List<Voucher> vouchers;
  _WalletData(this.summary, this.transactions, this.vouchers);
}

class WalletTab extends StatefulWidget {
  const WalletTab({super.key});

  @override
  State<WalletTab> createState() => _WalletTabState();
}

class _WalletTabState extends State<WalletTab> {
  Future<_WalletData>? _future;

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
    setState(() => _future = _fetch());
  }

  Future<_WalletData> _fetch() async {
    final results = await Future.wait([
      LoyaltyApi.getWalletSummary(),
      LoyaltyApi.getTransactions(),
      LoyaltyApi.getVouchers(),
    ]);
    return _WalletData(
      results[0] as WalletSummary,
      results[1] as List<PointTransaction>,
      results[2] as List<Voucher>,
    );
  }

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(title: const Text('محفظتي')),
        body: AsyncView<_WalletData>(
          future: _future,
          onRetry: _load,
          builder: (context, data) => NestedScrollView(
            headerSliverBuilder: (_, __) => [
              SliverToBoxAdapter(child: _summaryCard(context, data.summary)),
              const SliverToBoxAdapter(
                child: TabBar(
                  labelColor: AppConstants.primaryGreen,
                  indicatorColor: AppConstants.primaryGreen,
                  unselectedLabelColor: AppConstants.textMuted,
                  tabs: [Tab(text: 'حركات النقاط'), Tab(text: 'قسائمي')],
                ),
              ),
            ],
            body: TabBarView(
              children: [
                _refreshable(TransactionsList(transactions: data.transactions)),
                _refreshable(VouchersList(vouchers: data.vouchers)),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _refreshable(Widget child) => RefreshIndicator(
        color: AppConstants.primaryGreen,
        onRefresh: () async {
          _load();
          await _future;
        },
        child: child,
      );

  Widget _summaryCard(BuildContext context, WalletSummary s) {
    return Container(
      margin: const EdgeInsets.all(16),
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        gradient: const LinearGradient(colors: [AppConstants.bgDark, Color(0xFF1E293B)]),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('الرصيد المتاح', style: TextStyle(color: Colors.white70, fontSize: 13)),
          const SizedBox(height: 4),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(formatPoints(s.pointsBalance),
                  style: const TextStyle(color: Colors.white, fontSize: 34, fontWeight: FontWeight.bold)),
              const SizedBox(width: 6),
              const Padding(
                padding: EdgeInsets.only(bottom: 6),
                child: Text('نقطة', style: TextStyle(color: AppConstants.accentGoldLight)),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              _stat('مكتسبة', formatPoints(s.totalPointsEarned), AppConstants.successColor),
              _stat('مستبدلة', formatPoints(s.totalPointsSpent), AppConstants.accentGoldLight),
              _stat('قسائم نشطة', '${s.activeVouchersCount}', Colors.white),
            ],
          ),
          const SizedBox(height: 16),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton.icon(
              style: ElevatedButton.styleFrom(
                backgroundColor: AppConstants.accentGold,
                padding: const EdgeInsets.symmetric(vertical: 12),
              ),
              onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const RewardsScreen())),
              icon: const Icon(LucideIcons.gift, size: 20),
              label: const Text('استبدل نقاطك بمكافآت'),
            ),
          ),
        ],
      ),
    );
  }

  Widget _stat(String label, String value, Color color) => Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(value, style: TextStyle(color: color, fontSize: 17, fontWeight: FontWeight.bold)),
            Text(label, style: const TextStyle(color: Colors.white60, fontSize: 12)),
          ],
        ),
      );
}

class TransactionsList extends StatelessWidget {
  final List<PointTransaction> transactions;
  const TransactionsList({super.key, required this.transactions});

  IconData _icon(String type) {
    switch (type) {
      case 'EARN_RECEIPT':
      case 'EARN_INVOICE':
        return LucideIcons.receipt;
      case 'REDEEM_PURCHASE':
      case 'REDEEM_REWARD':
        return LucideIcons.gift;
      case 'POINTS_EXPIRED':
        return LucideIcons.clock;
      case 'BONUS':
        return LucideIcons.sparkles;
      default:
        return LucideIcons.settings2;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (transactions.isEmpty) {
      return ListView(children: const [
        EmptyView(
          icon: LucideIcons.history,
          title: 'لا توجد حركات بعد',
          subtitle: 'ارفع أول فاتورة لتبدأ بجمع النقاط',
        ),
      ]);
    }
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: transactions.length,
      separatorBuilder: (_, __) => const Divider(height: 1),
      itemBuilder: (_, i) {
        final t = transactions[i];
        final color = t.isCredit ? AppConstants.successColor : AppConstants.dangerColor;
        return ListTile(
          contentPadding: EdgeInsets.zero,
          leading: CircleAvatar(
            backgroundColor: color.withOpacity(0.1),
            child: Icon(_icon(t.type), color: color, size: 20),
          ),
          title: Text(t.description.isEmpty ? 'حركة نقاط' : t.description,
              maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 14)),
          subtitle: Text(formatDateTime(t.createdAt), style: const TextStyle(fontSize: 12)),
          trailing: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text('${t.isCredit ? '+' : ''}${formatPoints(t.amount)}',
                  textDirection: TextDirection.ltr,
                  style: TextStyle(color: color, fontWeight: FontWeight.bold, fontSize: 16)),
              Text('الرصيد ${formatPoints(t.balanceAfter)}', style: const TextStyle(fontSize: 11)),
            ],
          ),
        );
      },
    );
  }
}

class VouchersList extends StatelessWidget {
  final List<Voucher> vouchers;
  const VouchersList({super.key, required this.vouchers});

  @override
  Widget build(BuildContext context) {
    if (vouchers.isEmpty) {
      return ListView(children: const [
        EmptyView(
          icon: LucideIcons.ticket,
          title: 'لا توجد قسائم',
          subtitle: 'استبدل نقاطك بمكافأة لتحصل على قسيمة خصم',
        ),
      ]);
    }
    // القسائم النشطة أولاً
    final sorted = [...vouchers]..sort((a, b) {
        if (a.isActive != b.isActive) return a.isActive ? -1 : 1;
        return b.redeemedAt.compareTo(a.redeemedAt);
      });
    return ListView.builder(
      padding: const EdgeInsets.all(16),
      itemCount: sorted.length,
      itemBuilder: (_, i) => VoucherCard(voucher: sorted[i]),
    );
  }
}

class VoucherCard extends StatelessWidget {
  final Voucher voucher;
  const VoucherCard({super.key, required this.voucher});

  @override
  Widget build(BuildContext context) {
    final v = voucher;
    final active = v.isActive;
    return Opacity(
      opacity: active ? 1 : 0.6,
      child: Card(
        margin: const EdgeInsets.only(bottom: 12),
        child: InkWell(
          borderRadius: BorderRadius.circular(16),
          onTap: () => showVoucherCode(context, code: v.code, title: v.rewardTitle, expiresAt: v.expiresAt),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const Icon(LucideIcons.ticket, color: AppConstants.accentGold),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(v.rewardTitle, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                    ),
                    StatusChip.voucher(v.effectiveStatus),
                  ],
                ),
                if (v.merchantName != null) ...[
                  const SizedBox(height: 4),
                  Text(v.merchantName!, style: const TextStyle(fontSize: 12, color: AppConstants.textMuted)),
                ],
                const SizedBox(height: 12),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(v.code,
                        textDirection: TextDirection.ltr,
                        style: const TextStyle(
                            fontSize: 18, fontWeight: FontWeight.bold, letterSpacing: 2, color: AppConstants.primaryGreen)),
                    if (v.discountValue > 0)
                      Text('خصم ${formatMoney(v.discountValue)}', style: const TextStyle(fontWeight: FontWeight.bold)),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  v.usedAt != null ? 'استُخدمت في ${formatDate(v.usedAt!)}' : 'صالحة حتى ${formatDate(v.expiresAt)}',
                  style: const TextStyle(fontSize: 12, color: AppConstants.textMuted),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// نافذة تعرض كود القسيمة بخط كبير لإظهاره للكاشير، مع زر النسخ.
Future<void> showVoucherCode(BuildContext context,
    {required String code, required String title, required DateTime expiresAt}) {
  return showDialog(
    context: context,
    builder: (ctx) => AlertDialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      title: Text(title, textAlign: TextAlign.center),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('أظهر هذا الكود للكاشير عند الدفع', style: TextStyle(color: AppConstants.textMuted)),
          const SizedBox(height: 16),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(vertical: 18),
            decoration: BoxDecoration(
              color: AppConstants.primaryGreen.withOpacity(0.06),
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: AppConstants.primaryGreen.withOpacity(0.3), width: 1.5),
            ),
            child: Text(code,
                textAlign: TextAlign.center,
                textDirection: TextDirection.ltr,
                style: const TextStyle(
                    fontSize: 28, fontWeight: FontWeight.bold, letterSpacing: 4, color: AppConstants.primaryGreen)),
          ),
          const SizedBox(height: 12),
          Text('صالحة حتى ${formatDate(expiresAt)}', style: const TextStyle(fontSize: 12)),
        ],
      ),
      actions: [
        TextButton.icon(
          onPressed: () {
            Clipboard.setData(ClipboardData(text: code));
            showMessage(context, 'تم نسخ الكود');
          },
          icon: const Icon(LucideIcons.copy, size: 18),
          label: const Text('نسخ'),
        ),
        TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('إغلاق')),
      ],
    ),
  );
}
