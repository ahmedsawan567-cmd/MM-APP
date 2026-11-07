import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
import '../models/mall.dart';
import '../widgets/common.dart';
import 'invoices_screen.dart';
import 'notifications_screen.dart';
import 'rewards_screen.dart';

String tierNameAr(String tier) => switch (tier.toUpperCase()) {
      'SILVER' => 'العضوية الفضية',
      'GOLD' => 'العضوية الذهبية',
      'PLATINUM' => 'العضوية البلاتينية',
      _ => 'العضوية البرونزية',
    };

String _greeting() {
  final h = DateTime.now().hour;
  if (h < 12) return 'صباح الخير';
  if (h < 18) return 'مساء الخير';
  return 'مساء النور';
}

class HomeTab extends StatefulWidget {
  final void Function(int) onTabChange;
  const HomeTab({super.key, required this.onTabChange});

  @override
  State<HomeTab> createState() => _HomeTabState();
}

class _HomeTabState extends State<HomeTab> {
  Future<HomeData>? _future;

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

  void _load() => setState(() => _future = LoyaltyApi.getHome());

  Future<void> _refresh() async {
    _load();
    await _future;
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final name = Session.instance.user?.fullName ?? '';
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: AsyncView<HomeData>(
          future: _future,
          onRetry: _load,
          loading: const _HomeSkeleton(),
          builder: (context, data) => RefreshIndicator(
            onRefresh: _refresh,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
              children: [
                // الترحيب
                Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(_greeting(), style: TextStyle(color: p.muted)),
                          const SizedBox(height: 2),
                          Text(name.split(' ').first,
                              style: Theme.of(context).textTheme.headlineSmall, overflow: TextOverflow.ellipsis),
                        ],
                      ),
                    ),
                    const NotificationBell(),
                    const SizedBox(width: 10),
                    Container(
                      width: 46,
                      height: 46,
                      padding: const EdgeInsets.all(7),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(15),
                        border: Border.all(color: p.border),
                      ),
                      child: Image.asset(Brand.mark, semanticLabel: 'المرأة مول'),
                    ),
                  ],
                ),
                const SizedBox(height: 18),
                if (data.maintenanceMode) ...[
                  _Notice(
                    icon: LucideIcons.wrench,
                    text: 'النظام في وضع الصيانة حالياً، قد تتأخر معالجة الفواتير.',
                  ),
                  const SizedBox(height: 14),
                ],
                FadeSlideIn(child: MembershipCard(user: data.user, fallbackName: name)),
                const SizedBox(height: 22),
                FadeSlideIn(index: 1, child: _QuickActions(onTabChange: widget.onTabChange)),
                if (data.banners.isNotEmpty) ...[
                  const SizedBox(height: 26),
                  FadeSlideIn(index: 2, child: _BannersCarousel(banners: data.banners)),
                ],
                if (data.boosters.isNotEmpty) ...[
                  const SizedBox(height: 26),
                  const SectionTitle('عروض النقاط المضاعفة'),
                  SizedBox(
                    height: 142,
                    child: ListView.separated(
                      scrollDirection: Axis.horizontal,
                      itemCount: data.boosters.length,
                      separatorBuilder: (_, __) => const SizedBox(width: 12),
                      itemBuilder: (_, i) => FadeSlideIn(index: i, child: _BoosterCard(offer: data.boosters[i])),
                    ),
                  ),
                ],
                if (data.topStores.isNotEmpty) ...[
                  const SizedBox(height: 26),
                  SectionTitle('متاجر مميزة', actionLabel: 'عرض الكل', onAction: () => widget.onTabChange(1)),
                  for (var i = 0; i < data.topStores.take(5).length; i++)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: FadeSlideIn(index: i, child: _StoreTile(store: data.topStores[i])),
                    ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// بطاقة العضوية: الهوية الأبرز في التطبيق.
class MembershipCard extends StatelessWidget {
  final HomeUser? user;
  final String fallbackName;

  const MembershipCard({super.key, required this.user, required this.fallbackName});

  @override
  Widget build(BuildContext context) {
    final u = user;
    final progress = ((u?.tierProgress ?? 0) / 100).clamp(0.0, 1.0);
    const gold = Brand.glow;

    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.xl),
        gradient: Brand.gradient,
        boxShadow: [
          BoxShadow(color: Brand.plum.withValues(alpha: 0.35), blurRadius: 28, offset: const Offset(0, 14)),
        ],
      ),
      child: Stack(
        children: [
          // زخارف خلفية
          Positioned(
            left: -50,
            top: -60,
            child: _ring(200, Colors.white.withValues(alpha: 0.06)),
          ),
          Positioned(
            left: 30,
            bottom: -90,
            child: _ring(180, gold.withValues(alpha: 0.10)),
          ),
          Padding(
            padding: const EdgeInsets.all(22),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Image.asset(Brand.markLight, width: 24, height: 24),
                    const SizedBox(width: 8),
                    const Text(AppConstants.appNameAr,
                        style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 15)),
                    const Spacer(),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                      decoration: BoxDecoration(
                        color: gold.withValues(alpha: 0.16),
                        borderRadius: BorderRadius.circular(999),
                        border: Border.all(color: gold.withValues(alpha: 0.5)),
                      ),
                      child: Text(u != null && u.tierName.isNotEmpty ? 'عضوية ${u.tierName}' : tierNameAr(u?.loyaltyTier ?? 'BRONZE'),
                          style: const TextStyle(color: gold, fontSize: 12, fontWeight: FontWeight.w800)),
                    ),
                  ],
                ),
                const SizedBox(height: 26),
                Text('رصيد النقاط', style: TextStyle(color: Colors.white.withValues(alpha: 0.7), fontSize: 13)),
                const SizedBox(height: 2),
                Row(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    AnimatedCount(
                      value: u?.pointsBalance ?? Session.instance.user?.pointsBalance ?? 0,
                      style: const TextStyle(color: Colors.white, fontSize: 40, fontWeight: FontWeight.w800, height: 1.1),
                    ),
                    const SizedBox(width: 8),
                    Padding(
                      padding: const EdgeInsets.only(bottom: 6),
                      child: Text('نقطة', style: TextStyle(color: gold.withValues(alpha: 0.9), fontWeight: FontWeight.w700)),
                    ),
                  ],
                ),
                const SizedBox(height: 20),
                if (u != null && u.pointsToNextTier > 0) ...[
                  ClipRRect(
                    borderRadius: BorderRadius.circular(8),
                    child: TweenAnimationBuilder<double>(
                      tween: Tween(begin: 0, end: progress),
                      duration: const Duration(milliseconds: 1000),
                      curve: Curves.easeOutCubic,
                      builder: (_, v, __) => LinearProgressIndicator(
                        value: v,
                        minHeight: 7,
                        backgroundColor: Colors.white.withValues(alpha: 0.15),
                        valueColor: const AlwaysStoppedAnimation(gold),
                      ),
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'باقي ${formatPoints(u.pointsToNextTier)} نقطة للوصول إلى ${u.nextTierNameAr != null ? 'عضوية ${u.nextTierNameAr}' : tierNameAr(u.nextTierName)}',
                    style: TextStyle(color: Colors.white.withValues(alpha: 0.75), fontSize: 12),
                  ),
                ] else
                  Text(
                    (u?.fullName.isNotEmpty == true ? u!.fullName : fallbackName),
                    style: TextStyle(color: Colors.white.withValues(alpha: 0.8), fontSize: 13, letterSpacing: 0.4),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static Widget _ring(double size, Color color) => Container(
        width: size,
        height: size,
        decoration: BoxDecoration(shape: BoxShape.circle, border: Border.all(color: color, width: 26)),
      );
}

class _QuickActions extends StatelessWidget {
  final void Function(int) onTabChange;
  const _QuickActions({required this.onTabChange});

  @override
  Widget build(BuildContext context) {
    final items = [
      (LucideIcons.scanLine, 'مسح فاتورة', Tone.brand, () => onTabChange(2)),
      (LucideIcons.gift, 'المكافآت', Tone.gold, () => pushPage(context, const RewardsScreen())),
      (LucideIcons.receiptText, 'فواتيري', Tone.info, () => pushPage(context, const InvoicesScreen())),
      (LucideIcons.wallet, 'المحفظة', Tone.success, () => onTabChange(3)),
    ];
    return Row(
      children: [
        for (final (icon, label, tone, onTap) in items)
          Expanded(
            child: InkWell(
              onTap: onTap,
              borderRadius: BorderRadius.circular(AppRadius.lg),
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 6),
                child: Column(
                  children: [
                    IconBadge(icon, tone: tone, size: 56),
                    const SizedBox(height: 8),
                    Text(label, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}

class _Notice extends StatelessWidget {
  final IconData icon;
  final String text;
  const _Notice({required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(color: p.warningSoft, borderRadius: BorderRadius.circular(AppRadius.md)),
      child: Row(
        children: [
          Icon(icon, color: p.warning, size: 20),
          const SizedBox(width: 10),
          Expanded(child: Text(text, style: TextStyle(color: p.warning, fontWeight: FontWeight.w600))),
        ],
      ),
    );
  }
}

class _BannersCarousel extends StatefulWidget {
  final List<HomeBanner> banners;
  const _BannersCarousel({required this.banners});

  @override
  State<_BannersCarousel> createState() => _BannersCarouselState();
}

class _BannersCarouselState extends State<_BannersCarousel> {
  final _controller = PageController(viewportFraction: 0.92);
  int _page = 0;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Column(
      children: [
        SizedBox(
          height: 164,
          child: PageView.builder(
            controller: _controller,
            itemCount: widget.banners.length,
            onPageChanged: (i) => setState(() => _page = i),
            itemBuilder: (_, i) {
              final b = widget.banners[i];
              final url = AppConstants.resolveImageUrl(b.imageUrl);
              return Padding(
                padding: const EdgeInsets.symmetric(horizontal: 4),
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(AppRadius.lg),
                  child: Stack(
                    fit: StackFit.expand,
                    children: [
                      const DecoratedBox(
                        decoration: BoxDecoration(
                          gradient: Brand.gradient,
                        ),
                      ),
                      if (url != null) Image.network(url, fit: BoxFit.cover, errorBuilder: (_, __, ___) => const SizedBox()),
                      DecoratedBox(
                        decoration: BoxDecoration(
                          gradient: LinearGradient(
                            colors: [Colors.black.withValues(alpha: 0.72), Colors.transparent],
                            begin: Alignment.bottomCenter,
                            end: Alignment.topCenter,
                          ),
                        ),
                      ),
                      Positioned(
                        right: 18,
                        left: 18,
                        bottom: 16,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(b.title,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.w800)),
                            if (b.subtitle != null) ...[
                              const SizedBox(height: 2),
                              Text(b.subtitle!,
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                  style: TextStyle(color: Colors.white.withValues(alpha: 0.8), fontSize: 12, height: 1.4)),
                            ],
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
        if (widget.banners.length > 1) ...[
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: List.generate(
              widget.banners.length,
              (i) => AnimatedContainer(
                duration: const Duration(milliseconds: 250),
                margin: const EdgeInsets.symmetric(horizontal: 3),
                width: i == _page ? 18 : 6,
                height: 6,
                decoration: BoxDecoration(color: i == _page ? p.brand : p.border, borderRadius: BorderRadius.circular(6)),
              ),
            ),
          ),
        ],
      ],
    );
  }
}

class _BoosterCard extends StatelessWidget {
  final BoosterOffer offer;
  const _BoosterCard({required this.offer});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final reward = offer.bonusPoints > 0
        ? '+${formatPoints(offer.bonusPoints)} نقطة'
        : 'نقاط ×${offer.multiplier.toStringAsFixed(offer.multiplier % 1 == 0 ? 0 : 1)}';
    return SizedBox(
      width: 230,
      child: AppCard(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                StatusChip(label: reward, tone: Tone.gold, icon: LucideIcons.zap),
                const Spacer(),
                Text('${offer.daysRemaining} يوم', style: TextStyle(fontSize: 12, color: p.muted)),
              ],
            ),
            const SizedBox(height: 10),
            Text(offer.title,
                maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w800, height: 1.35)),
            const Spacer(),
            Row(
              children: [
                Icon(LucideIcons.store, size: 13, color: p.muted),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(offer.storeName,
                      maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: p.muted)),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _StoreTile extends StatelessWidget {
  final StoreSpotlight store;
  const _StoreTile({required this.store});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return AppCard(
      padding: const EdgeInsets.all(12),
      child: Row(
        children: [
          StoreLogo(name: store.nameAr, logoUrl: store.logoUrl, size: 48),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(store.nameAr, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w800)),
                const SizedBox(height: 3),
                Text('${store.floorLabel} • ${store.unitNumber}', style: TextStyle(fontSize: 12, color: p.muted)),
              ],
            ),
          ),
          StatusChip(label: store.category, tone: Tone.brand),
        ],
      ),
    );
  }
}

class _HomeSkeleton extends StatelessWidget {
  const _HomeSkeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      physics: const NeverScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
      children: const [
        Skeleton(width: 90, height: 12),
        SizedBox(height: 8),
        Skeleton(width: 150, height: 22),
        SizedBox(height: 20),
        Skeleton(height: 200, radius: AppRadius.xl),
        SizedBox(height: 22),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceAround,
          children: [
            Skeleton(width: 56, height: 56, radius: 18),
            Skeleton(width: 56, height: 56, radius: 18),
            Skeleton(width: 56, height: 56, radius: 18),
            Skeleton(width: 56, height: 56, radius: 18),
          ],
        ),
        SizedBox(height: 26),
        Skeleton(height: 164, radius: AppRadius.lg),
      ],
    );
  }
}
