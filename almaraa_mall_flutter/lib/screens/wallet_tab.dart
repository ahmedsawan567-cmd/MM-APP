import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
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

  void _load() => setState(() => _future = _fetch());

  Future<_WalletData> _fetch() async {
    final r = await Future.wait([LoyaltyApi.getWalletSummary(), LoyaltyApi.getTransactions(), LoyaltyApi.getVouchers()]);
    return _WalletData(r[0] as WalletSummary, r[1] as List<PointTransaction>, r[2] as List<Voucher>);
  }

  Future<void> _refresh() async {
    _load();
    await _future;
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
          loading: const SkeletonList(header: Skeleton(height: 190, radius: AppRadius.xl), count: 4),
          builder: (context, data) => NestedScrollView(
            headerSliverBuilder: (_, __) => [
              SliverToBoxAdapter(child: FadeSlideIn(child: _BalanceCard(summary: data.summary))),
              SliverToBoxAdapter(child: _segmentedTabs(context, data)),
            ],
            body: TabBarView(
              children: [
                RefreshIndicator(onRefresh: _refresh, child: TransactionsList(transactions: data.transactions)),
                RefreshIndicator(onRefresh: _refresh, child: VouchersList(vouchers: data.vouchers)),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _segmentedTabs(BuildContext context, _WalletData data) {
    final p = context.palette;
    final active = data.vouchers.where((v) => v.isActive).length;
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 4, 16, 4),
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(color: p.surfaceAlt, borderRadius: BorderRadius.circular(AppRadius.md)),
      child: TabBar(
        dividerColor: Colors.transparent,
        indicatorSize: TabBarIndicatorSize.tab,
        indicator: BoxDecoration(
          color: p.surface,
          borderRadius: BorderRadius.circular(AppRadius.sm),
          boxShadow: [BoxShadow(color: Colors.black.withValues(alpha: 0.06), blurRadius: 6, offset: const Offset(0, 2))],
        ),
        labelColor: p.ink,
        unselectedLabelColor: p.muted,
        tabs: [
          const Tab(height: 40, text: 'حركات النقاط'),
          Tab(height: 40, text: active > 0 ? 'قسائمي ($active)' : 'قسائمي'),
        ],
      ),
    );
  }
}

class _BalanceCard extends StatelessWidget {
  final WalletSummary summary;
  const _BalanceCard({required this.summary});

  @override
  Widget build(BuildContext context) {
    const gold = Color(0xFFFBBF24);
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 4, 16, 12),
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.xl),
        gradient: const LinearGradient(
          colors: [Color(0xFF0B1220), Color(0xFF1E293B)],
          begin: Alignment.topRight,
          end: Alignment.bottomLeft,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('الرصيد المتاح', style: TextStyle(color: Colors.white.withValues(alpha: 0.65), fontSize: 13)),
          const SizedBox(height: 2),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              AnimatedCount(
                value: summary.pointsBalance,
                style: const TextStyle(color: Colors.white, fontSize: 38, fontWeight: FontWeight.w800, height: 1.1),
              ),
              const SizedBox(width: 6),
              const Padding(
                padding: EdgeInsets.only(bottom: 6),
                child: Text('نقطة', style: TextStyle(color: gold, fontWeight: FontWeight.w700)),
              ),
            ],
          ),
          const SizedBox(height: 18),
          Row(
            children: [
              _stat('مكتسبة', formatPoints(summary.totalPointsEarned), const Color(0xFF34D399), LucideIcons.trendingUp),
              _stat('مستبدلة', formatPoints(summary.totalPointsSpent), gold, LucideIcons.gift),
              _stat('قسائم نشطة', '${summary.activeVouchersCount}', Colors.white, LucideIcons.ticket),
            ],
          ),
          const SizedBox(height: 18),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton.icon(
              style: ElevatedButton.styleFrom(
                backgroundColor: gold,
                foregroundColor: const Color(0xFF3B2600),
                minimumSize: const Size(0, 48),
              ),
              onPressed: () => pushPage(context, const RewardsScreen()),
              icon: const Icon(LucideIcons.gift, size: 20),
              label: const Text('استبدل نقاطك بمكافآت'),
            ),
          ),
        ],
      ),
    );
  }

  Widget _stat(String label, String value, Color color, IconData icon) => Expanded(
        child: Row(
          children: [
            Icon(icon, size: 16, color: color.withValues(alpha: 0.85)),
            const SizedBox(width: 6),
            Flexible(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(value, style: TextStyle(color: color, fontSize: 16, fontWeight: FontWeight.w800)),
                  Text(label, style: TextStyle(color: Colors.white.withValues(alpha: 0.6), fontSize: 11)),
                ],
              ),
            ),
          ],
        ),
      );
}

class TransactionsList extends StatelessWidget {
  final List<PointTransaction> transactions;
  const TransactionsList({super.key, required this.transactions});

  IconData _icon(String type) => switch (type) {
        'EARN_RECEIPT' || 'EARN_INVOICE' => LucideIcons.receiptText,
        'REDEEM_PURCHASE' || 'REDEEM_REWARD' => LucideIcons.gift,
        'POINTS_EXPIRED' => LucideIcons.clock,
        'BONUS' => LucideIcons.sparkles,
        _ => LucideIcons.settings2,
      };

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    if (transactions.isEmpty) {
      return ListView(children: const [
        EmptyView(icon: LucideIcons.history, title: 'لا توجد حركات بعد', subtitle: 'ارفع أول فاتورة لتبدأ بجمع النقاط'),
      ]);
    }
    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
      itemCount: transactions.length,
      separatorBuilder: (_, __) => const SizedBox(height: 8),
      itemBuilder: (_, i) {
        final t = transactions[i];
        final tone = t.isCredit ? Tone.success : Tone.danger;
        final (fg, _) = tone.colors(p);
        return FadeSlideIn(
          index: i,
          child: AppCard(
            padding: const EdgeInsets.all(12),
            child: Row(
              children: [
                IconBadge(_icon(t.type), tone: tone, size: 42),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(t.description.isEmpty ? 'حركة نقاط' : t.description,
                          maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, height: 1.35)),
                      const SizedBox(height: 3),
                      Text(formatDateTime(t.createdAt), style: TextStyle(fontSize: 11, color: p.muted)),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text('${t.isCredit ? '+' : ''}${formatPoints(t.amount)}',
                        textDirection: TextDirection.ltr, style: TextStyle(color: fg, fontWeight: FontWeight.w800, fontSize: 16)),
                    Text('الرصيد ${formatPoints(t.balanceAfter)}', style: TextStyle(fontSize: 11, color: p.muted)),
                  ],
                ),
              ],
            ),
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
      return ListView(children: [
        EmptyView(
          icon: LucideIcons.ticket,
          title: 'لا توجد قسائم',
          subtitle: 'استبدل نقاطك بمكافأة لتحصل على قسيمة خصم',
          action: FilledButton.tonal(onPressed: () => pushPage(context, const RewardsScreen()), child: const Text('تصفّح المكافآت')),
        ),
      ]);
    }
    final sorted = [...vouchers]..sort((a, b) {
        if (a.isActive != b.isActive) return a.isActive ? -1 : 1;
        return b.redeemedAt.compareTo(a.redeemedAt);
      });
    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
      itemCount: sorted.length,
      separatorBuilder: (_, __) => const SizedBox(height: 12),
      itemBuilder: (_, i) => FadeSlideIn(index: i, child: VoucherCard(voucher: sorted[i])),
    );
  }
}

/// قسيمة على شكل تذكرة.
class VoucherCard extends StatelessWidget {
  final Voucher voucher;
  const VoucherCard({super.key, required this.voucher});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final v = voucher;
    final active = v.isActive;
    return Opacity(
      opacity: active ? 1 : 0.55,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: BorderRadius.circular(AppRadius.lg),
          onTap: () => showVoucherCode(context, code: v.code, title: v.rewardTitle, expiresAt: v.expiresAt),
          child: ClipPath(
            clipper: const _TicketClipper(),
            child: Container(
              decoration: BoxDecoration(color: p.surface, border: Border.all(color: p.border), borderRadius: BorderRadius.circular(AppRadius.lg)),
              child: IntrinsicHeight(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Container(
                      width: 86,
                      decoration: BoxDecoration(
                        gradient: LinearGradient(
                          colors: active ? [const Color(0xFF064E3B), const Color(0xFF047857)] : [p.surfaceAlt, p.surfaceAlt],
                          begin: Alignment.topCenter,
                          end: Alignment.bottomCenter,
                        ),
                      ),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(LucideIcons.ticket, color: active ? const Color(0xFFFBBF24) : p.muted),
                          const SizedBox(height: 6),
                          if (v.discountValue > 0)
                            Text(v.discountValue.toStringAsFixed(v.discountValue % 1 == 0 ? 0 : 1),
                                style: TextStyle(color: active ? Colors.white : p.muted, fontSize: 22, fontWeight: FontWeight.w800)),
                          if (v.discountValue > 0)
                            Text('د.ل', style: TextStyle(color: active ? Colors.white70 : p.muted, fontSize: 11)),
                        ],
                      ),
                    ),
                    Expanded(
                      child: Padding(
                        padding: const EdgeInsets.all(14),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Expanded(child: Text(v.rewardTitle, style: const TextStyle(fontWeight: FontWeight.w800, height: 1.3))),
                                const SizedBox(width: 6),
                                StatusChip.voucher(v.effectiveStatus),
                              ],
                            ),
                            if (v.merchantName != null)
                              Text(v.merchantName!, style: TextStyle(fontSize: 12, color: p.muted)),
                            const SizedBox(height: 10),
                            Text(v.code,
                                textDirection: TextDirection.ltr,
                                style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800, letterSpacing: 2.5, color: p.brand)),
                            const SizedBox(height: 4),
                            Text(
                              v.usedAt != null ? 'استُخدمت في ${formatDate(v.usedAt!)}' : 'صالحة حتى ${formatDate(v.expiresAt)}',
                              style: TextStyle(fontSize: 11, color: p.muted),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// قصّ دائري على جانبي القسيمة ليبدو كتذكرة.
class _TicketClipper extends CustomClipper<Path> {
  const _TicketClipper();

  @override
  Path getClip(Size size) {
    const r = 10.0;
    const notchX = 86.0; // عند حدّ الجزء الملوّن (من اليمين في الواجهة العربية)
    final x = size.width - notchX;
    final path = Path()..addRRect(RRect.fromRectAndRadius(Offset.zero & size, const Radius.circular(AppRadius.lg)));
    final notches = Path()
      ..addOval(Rect.fromCircle(center: Offset(x, 0), radius: r))
      ..addOval(Rect.fromCircle(center: Offset(x, size.height), radius: r));
    return Path.combine(PathOperation.difference, path, notches);
  }

  @override
  bool shouldReclip(covariant CustomClipper<Path> oldClipper) => false;
}

/// نافذة تعرض كود القسيمة بخط كبير لإظهاره للكاشير، مع زر النسخ.
Future<void> showVoucherCode(BuildContext context, {required String code, required String title, required DateTime expiresAt}) {
  return showModalBottomSheet(
    context: context,
    builder: (ctx) {
      final p = ctx.palette;
      return Padding(
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            IconBadge(LucideIcons.ticket, tone: Tone.gold, size: 60),
            const SizedBox(height: 14),
            Text(title, textAlign: TextAlign.center, style: Theme.of(ctx).textTheme.titleMedium),
            const SizedBox(height: 4),
            Text('أظهر هذا الكود للكاشير عند الدفع', style: TextStyle(color: p.muted)),
            const SizedBox(height: 18),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(vertical: 22),
              decoration: BoxDecoration(
                color: p.brandSoft,
                borderRadius: BorderRadius.circular(AppRadius.lg),
                border: Border.all(color: p.brand.withValues(alpha: 0.35), width: 1.5),
              ),
              child: Text(code,
                  textAlign: TextAlign.center,
                  textDirection: TextDirection.ltr,
                  style: TextStyle(fontSize: 30, fontWeight: FontWeight.w800, letterSpacing: 5, color: p.brand)),
            ),
            const SizedBox(height: 10),
            Text('صالحة حتى ${formatDate(expiresAt)}', style: TextStyle(fontSize: 12, color: p.muted)),
            const SizedBox(height: 18),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () {
                      Clipboard.setData(ClipboardData(text: code));
                      showMessage(context, 'تم نسخ الكود');
                    },
                    icon: const Icon(LucideIcons.copy, size: 18),
                    label: const Text('نسخ الكود'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(child: ElevatedButton(onPressed: () => Navigator.pop(ctx), child: const Text('تم'))),
              ],
            ),
          ],
        ),
      );
    },
  );
}
