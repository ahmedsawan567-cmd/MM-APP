import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../models/mall.dart';
import '../widgets/common.dart';
import 'invoices_screen.dart';
import 'rewards_screen.dart';

String tierNameAr(String tier) {
  switch (tier.toUpperCase()) {
    case 'SILVER':
      return 'العضوية الفضية';
    case 'GOLD':
      return 'العضوية الذهبية';
    case 'PLATINUM':
      return 'العضوية البلاتينية';
    default:
      return 'العضوية البرونزية';
  }
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

  void _load() {
    setState(() => _future = LoyaltyApi.getHome());
  }

  Future<void> _refresh() async {
    _load();
    await _future;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(6),
              decoration: BoxDecoration(
                color: AppConstants.primaryGreen,
                borderRadius: BorderRadius.circular(8),
              ),
              child: const Icon(LucideIcons.crown, color: AppConstants.accentGoldLight, size: 18),
            ),
            const SizedBox(width: 8),
            const Text(AppConstants.appNameAr),
          ],
        ),
      ),
      body: AsyncView<HomeData>(
        future: _future,
        onRetry: _load,
        builder: (context, data) => RefreshIndicator(
          onRefresh: _refresh,
          color: AppConstants.primaryGreen,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            children: [
              if (data.maintenanceMode) _maintenanceNotice(),
              _MembershipCard(user: data.user, fallbackName: Session.instance.user?.fullName ?? ''),
              const SizedBox(height: 20),
              _quickActions(context),
              if (data.banners.isNotEmpty) ...[
                const SizedBox(height: 24),
                _BannersCarousel(banners: data.banners),
              ],
              if (data.boosters.isNotEmpty) ...[
                const SizedBox(height: 24),
                const SectionTitle('عروض النقاط المضاعفة 🔥'),
                const SizedBox(height: 10),
                SizedBox(
                  height: 132,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    itemCount: data.boosters.length,
                    separatorBuilder: (_, __) => const SizedBox(width: 12),
                    itemBuilder: (_, i) => _BoosterCard(offer: data.boosters[i]),
                  ),
                ),
              ],
              if (data.topStores.isNotEmpty) ...[
                const SizedBox(height: 24),
                SectionTitle('متاجر مميزة', actionLabel: 'عرض الكل', onAction: () => widget.onTabChange(1)),
                const SizedBox(height: 6),
                ...data.topStores.take(5).map(_storeTile),
              ],
              const SizedBox(height: 20),
            ],
          ),
        ),
      ),
    );
  }

  Widget _maintenanceNotice() => Container(
        margin: const EdgeInsets.only(bottom: 16),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: AppConstants.warningColor.withOpacity(0.12),
          borderRadius: BorderRadius.circular(12),
        ),
        child: const Row(
          children: [
            Icon(LucideIcons.wrench, color: AppConstants.warningColor),
            SizedBox(width: 10),
            Expanded(child: Text('النظام في وضع الصيانة حالياً، قد تتأخر معالجة الفواتير.')),
          ],
        ),
      );

  Widget _quickActions(BuildContext context) {
    void push(Widget page) => Navigator.of(context).push(MaterialPageRoute(builder: (_) => page));
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        _actionItem(LucideIcons.scanLine, 'مسح فاتورة', AppConstants.primaryGreen, () => widget.onTabChange(2)),
        _actionItem(LucideIcons.gift, 'المكافآت', AppConstants.accentGold, () => push(const RewardsScreen())),
        _actionItem(LucideIcons.receipt, 'فواتيري', const Color(0xFF2563EB), () => push(const InvoicesScreen())),
        _actionItem(LucideIcons.wallet, 'المحفظة', const Color(0xFF7C3AED), () => widget.onTabChange(3)),
      ],
    );
  }

  Widget _actionItem(IconData icon, String label, Color color, VoidCallback onTap) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Column(
        children: [
          Container(
            width: 60,
            height: 60,
            decoration: BoxDecoration(
              color: color.withOpacity(0.1),
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: color.withOpacity(0.2)),
            ),
            child: Icon(icon, color: color, size: 26),
          ),
          const SizedBox(height: 6),
          Text(label, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }

  Widget _storeTile(StoreSpotlight s) => Card(
        margin: const EdgeInsets.only(bottom: 10),
        child: ListTile(
          leading: StoreLogo(name: s.nameAr, logoUrl: s.logoUrl, size: 44),
          title: Text(s.nameAr, style: const TextStyle(fontWeight: FontWeight.bold)),
          subtitle: Text('${s.category} • ${s.floorLabel} • ${s.unitNumber}'),
        ),
      );
}

class _MembershipCard extends StatelessWidget {
  final HomeUser? user;
  final String fallbackName;

  const _MembershipCard({required this.user, required this.fallbackName});

  @override
  Widget build(BuildContext context) {
    final u = user;
    final progress = ((u?.tierProgress ?? 0) / 100).clamp(0.0, 1.0);

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF064E3B), Color(0xFF047857), Color(0xFF065F46)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(20),
        boxShadow: [
          BoxShadow(color: AppConstants.primaryGreen.withOpacity(0.25), blurRadius: 15, offset: const Offset(0, 6)),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'مرحباً، ${u?.fullName.isNotEmpty == true ? u!.fullName : fallbackName}',
                      style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold),
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 6),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                      decoration: BoxDecoration(
                        color: AppConstants.accentGold.withOpacity(0.3),
                        borderRadius: BorderRadius.circular(6),
                        border: Border.all(color: AppConstants.accentGoldLight),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(LucideIcons.sparkles, color: AppConstants.accentGoldLight, size: 12),
                          const SizedBox(width: 4),
                          Text(
                            tierNameAr(u?.loyaltyTier ?? 'BRONZE'),
                            style: const TextStyle(
                                color: AppConstants.accentGoldLight, fontSize: 11, fontWeight: FontWeight.bold),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: Colors.white.withOpacity(0.15),
                  border: Border.all(color: AppConstants.accentGoldLight, width: 1.5),
                ),
                child: const Icon(LucideIcons.user, color: Colors.white, size: 24),
              ),
            ],
          ),
          const SizedBox(height: 20),
          const Text('رصيد النقاط', style: TextStyle(color: Colors.white70, fontSize: 12)),
          const SizedBox(height: 4),
          Text(
            formatPoints(u?.pointsBalance ?? Session.instance.user?.pointsBalance ?? 0),
            style: const TextStyle(color: Colors.white, fontSize: 30, fontWeight: FontWeight.bold),
          ),
          if (u != null && u.pointsToNextTier > 0) ...[
            const SizedBox(height: 14),
            ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: LinearProgressIndicator(
                value: progress,
                minHeight: 7,
                backgroundColor: Colors.white24,
                valueColor: const AlwaysStoppedAnimation(AppConstants.accentGoldLight),
              ),
            ),
            const SizedBox(height: 6),
            Text(
              'باقي ${formatPoints(u.pointsToNextTier)} نقطة للوصول إلى ${tierNameAr(u.nextTierName)}',
              style: const TextStyle(color: Colors.white70, fontSize: 12),
            ),
          ],
        ],
      ),
    );
  }
}

class _BannersCarousel extends StatelessWidget {
  final List<HomeBanner> banners;
  const _BannersCarousel({required this.banners});

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 150,
      child: PageView.builder(
        controller: PageController(viewportFraction: 0.92),
        itemCount: banners.length,
        itemBuilder: (_, i) {
          final b = banners[i];
          final url = AppConstants.resolveImageUrl(b.imageUrl);
          return Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(18),
              child: Stack(
                fit: StackFit.expand,
                children: [
                  Container(
                    decoration: const BoxDecoration(
                      gradient: LinearGradient(colors: [AppConstants.bgDark, Color(0xFF1E293B)]),
                    ),
                  ),
                  if (url != null)
                    Image.network(url, fit: BoxFit.cover, errorBuilder: (_, __, ___) => const SizedBox()),
                  Container(
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        colors: [Colors.black.withOpacity(0.65), Colors.transparent],
                        begin: Alignment.bottomCenter,
                        end: Alignment.topCenter,
                      ),
                    ),
                  ),
                  Positioned(
                    right: 16,
                    left: 16,
                    bottom: 14,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(b.title,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(color: Colors.white, fontSize: 17, fontWeight: FontWeight.bold)),
                        if (b.subtitle != null)
                          Text(b.subtitle!,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(color: Colors.white70, fontSize: 12)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

class _BoosterCard extends StatelessWidget {
  final BoosterOffer offer;
  const _BoosterCard({required this.offer});

  @override
  Widget build(BuildContext context) {
    final reward = offer.bonusPoints > 0
        ? '+${formatPoints(offer.bonusPoints)} نقطة'
        : 'نقاط ×${offer.multiplier.toStringAsFixed(offer.multiplier % 1 == 0 ? 0 : 1)}';
    return Container(
      width: 220,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppConstants.accentGold.withOpacity(0.35)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(LucideIcons.zap, color: AppConstants.accentGold, size: 18),
              const SizedBox(width: 6),
              Text(reward, style: const TextStyle(color: AppConstants.accentGold, fontWeight: FontWeight.bold)),
            ],
          ),
          const SizedBox(height: 8),
          Text(offer.title, maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.bold)),
          const Spacer(),
          Text('${offer.storeName} • باقي ${offer.daysRemaining} يوم',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 12, color: AppConstants.textMuted)),
        ],
      ),
    );
  }
}
