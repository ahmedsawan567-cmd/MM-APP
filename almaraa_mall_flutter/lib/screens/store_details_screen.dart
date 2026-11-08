import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
import '../models/mall.dart';
import '../models/store_details.dart';
import '../widgets/common.dart';
import 'wallet_tab.dart' show showVoucherCode;

/// صفحة المتجر: رصيدي، كم باقي للمكافأة، كيف أكسب النقاط هنا، ونشاطي في المتجر.
class StoreDetailsScreen extends StatefulWidget {
  final String storeId;
  final Store? store;
  const StoreDetailsScreen({super.key, required this.storeId, this.store});

  @override
  State<StoreDetailsScreen> createState() => _StoreDetailsScreenState();
}

class _StoreDetailsScreenState extends State<StoreDetailsScreen> {
  Future<StoreDetails>? _future;
  String? _redeemingId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  void _load() => setState(() => _future = LoyaltyApi.getStoreDetails(widget.storeId));

  Future<void> _redeem(StoreReward reward) async {
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

  void _scanInvoice() => Session.instance.requestedTab.value = 2;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(widget.store?.nameAr ?? 'المتجر')),
      body: AsyncView<StoreDetails>(
        future: _future,
        onRetry: _load,
        builder: (context, d) => RefreshIndicator(
          onRefresh: () async {
            _load();
            await _future;
          },
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 110),
            children: [
              FadeSlideIn(child: _Header(d: d)),
              const SizedBox(height: 14),
              FadeSlideIn(index: 1, child: _ProgressCard(d: d, onScan: _scanInvoice)),
              if (d.boosters.isNotEmpty) ...[
                const SizedBox(height: 14),
                FadeSlideIn(index: 2, child: _Boosters(boosters: d.boosters)),
              ],
              const SizedBox(height: 22),
              _SectionTitle(icon: LucideIcons.sparkles, title: 'كيف تكسب النقاط هنا'),
              const SizedBox(height: 10),
              FadeSlideIn(index: 2, child: _HowToEarn(lines: d.howToEarn)),
              const SizedBox(height: 22),
              _SectionTitle(icon: LucideIcons.gift, title: 'المكافآت المتاحة'),
              const SizedBox(height: 10),
              if (d.rewards.isEmpty)
                const AppCard(child: Text('لا توجد مكافآت متاحة لهذا المتجر حالياً.'))
              else
                for (final r in d.rewards)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: _RewardTile(
                      reward: r,
                      loggedIn: d.pointsBalance != null,
                      busy: _redeemingId == r.id,
                      onRedeem: _redeemingId == null ? () => _redeem(r) : null,
                    ),
                  ),
              if (d.mine != null) ...[
                const SizedBox(height: 12),
                _SectionTitle(icon: LucideIcons.chartColumn, title: 'نشاطي في المتجر'),
                const SizedBox(height: 10),
                _MyActivity(m: d.mine!),
              ],
            ],
          ),
        ),
      ),
      bottomNavigationBar: SafeArea(
        minimum: const EdgeInsets.fromLTRB(16, 0, 16, 12),
        child: SizedBox(
          height: 54,
          child: ElevatedButton.icon(
            onPressed: _scanInvoice,
            icon: const Icon(LucideIcons.scanLine),
            label: const Text('صوّر فاتورة من هذا المتجر'),
          ),
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  final IconData icon;
  final String title;
  const _SectionTitle({required this.icon, required this.title});

  @override
  Widget build(BuildContext context) => Row(
        children: [
          Icon(icon, size: 18, color: Brand.purple),
          const SizedBox(width: 8),
          Text(title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
        ],
      );
}

class _Header extends StatelessWidget {
  final StoreDetails d;
  const _Header({required this.d});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final location = [d.floor, d.unitNumber].where((s) => s.isNotEmpty).join(' • ');
    final cover = AppConstants.resolveImageUrl(d.coverImageUrl);
    final info = Row(
      children: [
        StoreLogo(name: d.nameAr, logoUrl: d.logoUrl, size: 68),
        const SizedBox(width: 14),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(d.nameAr, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, height: 1.3)),
              if (d.nameEn.isNotEmpty && d.nameEn != d.nameAr)
                Text(d.nameEn, style: TextStyle(fontSize: 13, color: p.muted)),
              const SizedBox(height: 8),
              Wrap(spacing: 6, runSpacing: 6, children: [
                if (d.category.isNotEmpty) StatusChip(label: d.category, tone: Tone.brand),
                if (location.isNotEmpty) StatusChip(label: location, tone: Tone.neutral, icon: LucideIcons.mapPin),
              ]),
            ],
          ),
        ),
      ],
    );
    if (cover == null) return info;
    // صورة غلاف المتجر أعلى الصفحة
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(AppRadius.xl),
          child: AspectRatio(
            aspectRatio: 16 / 7,
            child: Image.network(cover, fit: BoxFit.cover,
                errorBuilder: (_, __, ___) => Container(decoration: const BoxDecoration(gradient: Brand.gradient))),
          ),
        ),
        const SizedBox(height: 14),
        info,
      ],
    );
  }
}

/// البطاقة الرئيسية: الرصيد الحالي وكم باقي لأقرب مكافأة.
class _ProgressCard extends StatelessWidget {
  final StoreDetails d;
  final VoidCallback onScan;
  const _ProgressCard({required this.d, required this.onScan});

  @override
  Widget build(BuildContext context) {
    const white70 = TextStyle(color: Colors.white70, fontSize: 13);
    final balance = d.pointsBalance;
    final next = d.nextReward;

    Widget body;
    if (balance == null) {
      body = const Text('سجّل الدخول لترى رصيدك وكم باقي لك للمكافأة.', style: TextStyle(color: Colors.white, fontSize: 15));
    } else if (next == null) {
      body = Text(d.rewards.isEmpty ? 'اجمع النقاط من فواتيرك هنا واستبدلها بمكافآت المول.' : 'مبروك! رصيدك يكفي لكل المكافآت المتاحة 🎉',
          style: const TextStyle(color: Colors.white, fontSize: 15, fontWeight: FontWeight.w700));
    } else if (next.canRedeem) {
      body = Text('رصيدك يكفي الآن لـ «${next.title}» — استبدلها من الأسفل 🎁',
          style: const TextStyle(color: Colors.white, fontSize: 15, fontWeight: FontWeight.w700));
    } else {
      body = Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text.rich(
            TextSpan(style: const TextStyle(color: Colors.white, fontSize: 15, height: 1.5), children: [
              const TextSpan(text: 'باقي لك '),
              TextSpan(text: formatPoints(next.remainingPoints), style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18, color: Brand.glow)),
              const TextSpan(text: ' نقطة للحصول على '),
              TextSpan(text: next.title, style: const TextStyle(fontWeight: FontWeight.w700)),
            ]),
          ),
          const SizedBox(height: 12),
          ClipRRect(
            borderRadius: BorderRadius.circular(99),
            child: TweenAnimationBuilder<double>(
              tween: Tween(begin: 0, end: next.progress),
              duration: const Duration(milliseconds: 900),
              curve: Curves.easeOutCubic,
              builder: (_, v, __) => LinearProgressIndicator(
                value: v,
                minHeight: 10,
                backgroundColor: Colors.white24,
                valueColor: const AlwaysStoppedAnimation(Colors.white),
              ),
            ),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Text('${formatPoints(balance)} / ${formatPoints(next.pointsCost)}', style: white70),
              const Spacer(),
              if (next.estimatedSpend != null && next.estimatedSpend! > 0)
                Text('≈ تسوّق بـ ${formatMoney(next.estimatedSpend!)}', style: white70),
            ],
          ),
        ],
      );
    }

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(gradient: Brand.gradient, borderRadius: BorderRadius.circular(AppRadius.xl)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(LucideIcons.coins, color: Colors.white, size: 20),
              const SizedBox(width: 8),
              const Text('رصيدي الحالي', style: TextStyle(color: Colors.white70, fontSize: 14)),
              const Spacer(),
              if (balance != null)
                AnimatedCount(
                  value: balance,
                  style: const TextStyle(color: Colors.white, fontSize: 28, fontWeight: FontWeight.w800),
                ),
            ],
          ),
          const SizedBox(height: 14),
          body,
        ],
      ),
    );
  }
}

class _Boosters extends StatelessWidget {
  final List<StoreBooster> boosters;
  const _Boosters({required this.boosters});

  @override
  Widget build(BuildContext context) => Column(
        children: [
          for (final b in boosters)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: AppCard(
                padding: const EdgeInsets.all(12),
                child: Row(
                  children: [
                    IconBadge(LucideIcons.zap, tone: Tone.warning, size: 40),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(b.title, style: const TextStyle(fontWeight: FontWeight.w700)),
                          Text('ينتهي ${formatDate(b.endsAt)}', style: TextStyle(fontSize: 12, color: context.palette.muted)),
                        ],
                      ),
                    ),
                    StatusChip(label: '+${formatPoints(b.bonusPoints)} نقطة', tone: Tone.success),
                  ],
                ),
              ),
            ),
        ],
      );
}

class _HowToEarn extends StatelessWidget {
  final List<String> lines;
  const _HowToEarn({required this.lines});

  @override
  Widget build(BuildContext context) {
    final items = lines.isEmpty ? const ['صوّر فاتورتك بعد الشراء واكسب النقاط بعد المراجعة.'] : lines;
    return AppCard(
      child: Column(
        children: [
          for (var i = 0; i < items.length; i++) ...[
            if (i > 0) const SizedBox(height: 10),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Padding(
                  padding: EdgeInsets.only(top: 2),
                  child: Icon(LucideIcons.circleCheck, size: 18, color: Brand.purple),
                ),
                const SizedBox(width: 10),
                Expanded(child: Text(items[i], style: const TextStyle(fontSize: 14, height: 1.5))),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _RewardTile extends StatelessWidget {
  final StoreReward reward;
  final bool loggedIn;
  final bool busy;
  final VoidCallback? onRedeem;
  const _RewardTile({required this.reward, required this.loggedIn, required this.busy, this.onRedeem});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final r = reward;
    return AppCard(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              IconBadge(LucideIcons.gift, tone: r.canRedeem ? Tone.success : Tone.gold, size: 44),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(r.title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                    if (r.description.isNotEmpty)
                      Text(r.description, maxLines: 2, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: p.muted)),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              StatusChip(label: '${formatPoints(r.pointsCost)} نقطة', tone: Tone.gold, icon: LucideIcons.coins),
            ],
          ),
          if (!r.atThisStore) ...[
            const SizedBox(height: 8),
            Text('مكافأة عامة من المول', style: TextStyle(fontSize: 12, color: p.muted)),
          ],
          if (loggedIn) ...[
            const SizedBox(height: 12),
            ClipRRect(
              borderRadius: BorderRadius.circular(99),
              child: LinearProgressIndicator(
                value: r.progress,
                minHeight: 7,
                backgroundColor: p.border,
                valueColor: AlwaysStoppedAnimation(r.canRedeem ? Colors.green : Brand.purple),
              ),
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: Text(
                    r.canRedeem ? 'رصيدك يكفي ✓' : 'باقي ${formatPoints(r.remainingPoints)} نقطة',
                    style: TextStyle(fontWeight: FontWeight.w700, color: r.canRedeem ? Colors.green : p.muted),
                  ),
                ),
                if (r.canRedeem)
                  SizedBox(
                    height: 38,
                    child: ElevatedButton(
                      onPressed: busy ? null : onRedeem,
                      child: busy
                          ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                          : const Text('استبدل الآن'),
                    ),
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _MyActivity extends StatelessWidget {
  final StoreActivity m;
  const _MyActivity({required this.m});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    Widget stat(String label, String value, IconData icon) => Expanded(
          child: AppCard(
            padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 10),
            child: Column(
              children: [
                Icon(icon, size: 20, color: Brand.purple),
                const SizedBox(height: 6),
                Text(value, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 17)),
                Text(label, style: TextStyle(fontSize: 12, color: p.muted)),
              ],
            ),
          ),
        );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(children: [
          stat('فواتير', '${m.invoicesCount}', LucideIcons.receipt),
          const SizedBox(width: 8),
          stat('مشترياتي', formatMoney(m.totalSpent), LucideIcons.wallet),
          const SizedBox(width: 8),
          stat('نقاط كسبتها', formatPoints(m.pointsEarned), LucideIcons.coins),
        ]),
        if (m.lastVisit != null) ...[
          const SizedBox(height: 8),
          Text('آخر فاتورة: ${formatDate(m.lastVisit!)}', style: TextStyle(fontSize: 12, color: p.muted)),
        ],
      ],
    );
  }
}
