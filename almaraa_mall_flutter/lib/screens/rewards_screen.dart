import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
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

  void _load() {
    setState(() => _future = _fetch());
  }

  Future<(List<Reward>, int)> _fetch() async {
    final results = await Future.wait([LoyaltyApi.getRewards(), LoyaltyApi.getWalletSummary()]);
    final rewards = results[0] as List<Reward>;
    rewards.sort((a, b) => a.pointsCost.compareTo(b.pointsCost));
    return (rewards, (results[1] as WalletSummary).pointsBalance);
  }

  Future<void> _redeem(Reward reward) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تأكيد الاستبدال'),
        content: Text('سيتم خصم ${formatPoints(reward.pointsCost)} نقطة مقابل "${reward.title}". هل تريد المتابعة؟'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
          ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('استبدال')),
        ],
      ),
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
      appBar: AppBar(title: const Text('كتالوج المكافآت')),
      body: AsyncView<(List<Reward>, int)>(
        future: _future,
        onRetry: _load,
        builder: (context, data) {
          final (rewards, balance) = data;
          return RefreshIndicator(
            color: AppConstants.primaryGreen,
            onRefresh: () async {
              _load();
              await _future;
            },
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: AppConstants.primaryGreen,
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Row(
                    children: [
                      const Icon(LucideIcons.coins, color: AppConstants.accentGoldLight, size: 28),
                      const SizedBox(width: 12),
                      const Expanded(child: Text('رصيدك الحالي', style: TextStyle(color: Colors.white70))),
                      Text('${formatPoints(balance)} نقطة',
                          style: const TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.bold)),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                if (rewards.isEmpty)
                  const EmptyView(icon: LucideIcons.gift, title: 'لا توجد مكافآت متاحة حالياً')
                else
                  ...rewards.map((r) => _RewardCard(
                        reward: r,
                        balance: balance,
                        busy: _redeemingId == r.id,
                        onRedeem: _redeemingId == null ? () => _redeem(r) : null,
                      )),
              ],
            ),
          );
        },
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
    final r = reward;
    final affordable = balance >= r.pointsCost;
    final missing = r.pointsCost - balance;
    final image = AppConstants.resolveImageUrl(r.imageUrl);

    return Card(
      margin: const EdgeInsets.only(bottom: 14),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (image != null)
            SizedBox(
              height: 130,
              child: Image.network(image, fit: BoxFit.cover, errorBuilder: (_, __, ___) => const SizedBox()),
            ),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Text(r.title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                    ),
                    if (r.discountValue > 0)
                      StatusChip(label: 'خصم ${formatMoney(r.discountValue)}', color: AppConstants.accentGold),
                  ],
                ),
                if (r.merchantName != null) ...[
                  const SizedBox(height: 4),
                  Row(children: [
                    const Icon(LucideIcons.store, size: 13, color: AppConstants.textMuted),
                    const SizedBox(width: 4),
                    Text(r.merchantName!, style: const TextStyle(fontSize: 12, color: AppConstants.textMuted)),
                  ]),
                ],
                if (r.description.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Text(r.description, style: const TextStyle(fontSize: 13)),
                ],
                const SizedBox(height: 8),
                Text(
                  [
                    'صالحة ${r.validityDays} يوم بعد الاستبدال',
                    if (r.stockQuantity != null) 'المتبقي: ${r.stockQuantity}',
                  ].join('  •  '),
                  style: const TextStyle(fontSize: 12, color: AppConstants.textMuted),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    const Icon(LucideIcons.coins, color: AppConstants.accentGold, size: 20),
                    const SizedBox(width: 6),
                    Text('${formatPoints(r.pointsCost)} نقطة',
                        style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: AppConstants.accentGold)),
                    const Spacer(),
                    if (r.isOutOfStock)
                      const StatusChip(label: 'نفدت الكمية', color: AppConstants.dangerColor)
                    else if (!affordable)
                      Text('ينقصك ${formatPoints(missing)} نقطة',
                          style: const TextStyle(fontSize: 12, color: AppConstants.textMuted))
                    else
                      ElevatedButton(
                        onPressed: onRedeem,
                        style: ElevatedButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10)),
                        child: busy
                            ? const SizedBox(
                                width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                            : const Text('استبدال'),
                      ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
