import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
import '../models/reward.dart';
import '../models/wallet.dart';
import '../widgets/common.dart';
import 'wallet_tab.dart';

class RewardsScreen extends StatefulWidget {
  const RewardsScreen({super.key});

  @override
  State<RewardsScreen> createState() => _RewardsScreenState();
}

class _RewardsScreenState extends State<RewardsScreen> {
  Future<(List<Reward>, int)>? _future;
  String? _redeemingId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  void _load() => setState(() => _future = _fetch());

  Future<(List<Reward>, int)> _fetch() async {
    final r = await Future.wait([LoyaltyApi.getRewards(), LoyaltyApi.getWalletSummary()]);
    final rewards = r[0] as List<Reward>..sort((a, b) => a.pointsCost.compareTo(b.pointsCost));
    return (rewards, (r[1] as WalletSummary).pointsBalance);
  }

  Future<void> _redeem(Reward reward) async {
    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      builder: (ctx) {
        final p = ctx.palette;
        return Padding(
          padding: const EdgeInsets.fromLTRB(24, 0, 24, 28),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              IconBadge(LucideIcons.gift, tone: Tone.gold, size: 60),
              const SizedBox(height: 14),
              Text('تأكيد الاستبدال', style: Theme.of(ctx).textTheme.titleLarge),
              const SizedBox(height: 8),
              Text('سيتم خصم ${formatPoints(reward.pointsCost)} نقطة مقابل:',
                  textAlign: TextAlign.center, style: TextStyle(color: p.muted)),
              const SizedBox(height: 4),
              Text(reward.title, textAlign: TextAlign.center, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
              const SizedBox(height: 22),
              Row(
                children: [
                  Expanded(child: OutlinedButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء'))),
                  const SizedBox(width: 12),
                  Expanded(child: ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('استبدال'))),
                ],
              ),
            ],
          ),
        );
      },
    );
    if (confirmed != true || !mounted) return;

    setState(() => _redeemingId = reward.id);
    try {
      final result = await LoyaltyApi.redeemReward(reward.id);
      Session.instance.notifyDataChanged();
      if (!mounted) return;
      _load();
      await showVoucherCode(context, code: result.code, title: result.rewardTitle, expiresAt: result.expiresAt);
    } catch (e) {
      if (mounted) showMessage(context, e.toString(), error: true);
    } finally {
      if (mounted) setState(() => _redeemingId = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('المكافآت')),
      body: AsyncView<(List<Reward>, int)>(
        future: _future,
        onRetry: _load,
        loading: const SkeletonList(header: Skeleton(height: 84, radius: AppRadius.lg), itemHeight: 150, count: 3),
        builder: (context, data) {
          final (rewards, balance) = data;
          return RefreshIndicator(
            onRefresh: () async {
              _load();
              await _future;
            },
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
              children: [
                _BalanceStrip(balance: balance),
                const SizedBox(height: 16),
                if (rewards.isEmpty)
                  const EmptyView(icon: LucideIcons.gift, title: 'لا توجد مكافآت متاحة حالياً', subtitle: 'تابعنا، ستضاف مكافآت جديدة قريباً')
                else
                  for (var i = 0; i < rewards.length; i++)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 14),
                      child: FadeSlideIn(
                        index: i,
                        child: _RewardCard(
                          reward: rewards[i],
                          balance: balance,
                          busy: _redeemingId == rewards[i].id,
                          onRedeem: _redeemingId == null ? () => _redeem(rewards[i]) : null,
                        ),
                      ),
                    ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _BalanceStrip extends StatelessWidget {
  final int balance;
  const _BalanceStrip({required this.balance});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        gradient: Brand.gradient,
      ),
      child: Row(
        children: [
          Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(color: Colors.white.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(14)),
            child: const Icon(LucideIcons.coins, color: Brand.glow),
          ),
          const SizedBox(width: 14),
          Expanded(child: Text('رصيدك الحالي', style: TextStyle(color: Colors.white.withValues(alpha: 0.8)))),
          AnimatedCount(value: balance, style: const TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.w700)),
          const SizedBox(width: 6),
          const Text('نقطة', style: TextStyle(color: Brand.glow, fontWeight: FontWeight.w700)),
        ],
      ),
    );
  }
}

class _RewardCard extends StatelessWidget {
  final Reward reward;
  final int balance;
  final bool busy;
  final VoidCallback? onRedeem;

  const _RewardCard({required this.reward, required this.balance, required this.busy, this.onRedeem});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final r = reward;
    final affordable = balance >= r.pointsCost;
    final image = AppConstants.resolveImageUrl(r.imageUrl);
    final progress = r.pointsCost == 0 ? 1.0 : (balance / r.pointsCost).clamp(0.0, 1.0);

    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: p.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: p.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (image != null)
            SizedBox(height: 140, child: Image.network(image, fit: BoxFit.cover, errorBuilder: (_, __, ___) => const SizedBox())),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(child: Text(r.title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, height: 1.35))),
                    if (r.discountValue > 0) ...[
                      const SizedBox(width: 8),
                      StatusChip(label: 'خصم ${formatMoney(r.discountValue)}', tone: Tone.gold),
                    ],
                  ],
                ),
                if (r.merchantName != null) ...[
                  const SizedBox(height: 6),
                  Row(children: [
                    Icon(LucideIcons.store, size: 14, color: p.muted),
                    const SizedBox(width: 4),
                    Text(r.merchantName!, style: TextStyle(fontSize: 12, color: p.muted)),
                  ]),
                ],
                if (r.description.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Text(r.description, style: TextStyle(fontSize: 13, color: p.muted, height: 1.5)),
                ],
                const SizedBox(height: 14),
                Row(
                  children: [
                    Icon(LucideIcons.coins, color: p.gold, size: 20),
                    const SizedBox(width: 6),
                    Text('${formatPoints(r.pointsCost)} نقطة',
                        style: TextStyle(fontSize: 17, fontWeight: FontWeight.w700, color: p.gold)),
                    const Spacer(),
                    if (r.isOutOfStock)
                      const StatusChip(label: 'نفدت الكمية', tone: Tone.danger)
                    else if (affordable)
                      ElevatedButton(
                        onPressed: onRedeem,
                        style: ElevatedButton.styleFrom(minimumSize: const Size(110, 42)),
                        child: busy
                            ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                            : const Text('استبدال'),
                      ),
                  ],
                ),
                if (!affordable && !r.isOutOfStock) ...[
                  const SizedBox(height: 12),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(6),
                    child: LinearProgressIndicator(value: progress, minHeight: 6, backgroundColor: p.surfaceAlt, color: p.gold),
                  ),
                  const SizedBox(height: 6),
                  Text('ينقصك ${formatPoints(r.pointsCost - balance)} نقطة', style: TextStyle(fontSize: 12, color: p.muted)),
                ],
                const SizedBox(height: 10),
                Text(
                  [
                    'صالحة ${r.validityDays} يوم بعد الاستبدال',
                    if (r.stockQuantity != null && !r.isOutOfStock) 'المتبقي ${r.stockQuantity}',
                  ].join('  •  '),
                  style: TextStyle(fontSize: 11, color: p.muted),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
