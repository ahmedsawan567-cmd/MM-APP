import 'package:flutter/material.dart';
import 'package:lucide_icons/lucide_icons.dart';
import '../core/constants/app_constants.dart';
import '../core/services/supabase_service.dart';
import '../models/customer_model.dart';
import '../models/store_model.dart';
import '../models/stamp_model.dart';

class HomeTab extends StatefulWidget {
  final Function(int) onTabChange;
  const HomeTab({super.key, required this.onTabChange});

  @override
  State<HomeTab> createState() => _HomeTabState();
}

class _HomeTabState extends State<HomeTab> {
  CustomerModel? _customer;
  List<StoreModel> _stores = [];
  List<StampCardModel> _stamps = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  Future<void> _loadData() async {
    final customer = await SupabaseService.instance.getCustomerProfile('CUST-SARAH');
    final stores = await SupabaseService.instance.getStores();
    final stamps = await SupabaseService.instance.getCustomerStamps('CUST-SARAH');

    if (mounted) {
      setState(() {
        _customer = customer;
        _stores = stores;
        _stamps = stamps;
        _isLoading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_isLoading) {
      return const Center(child: CircularProgressIndicator(color: AppConstants.primaryGreen));
    }

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
        actions: [
          IconButton(
            icon: const Icon(LucideIcons.bell, size: 22),
            onPressed: () {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('لا توجد إشعارات جديدة حالياً')),
              );
            },
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _loadData,
        color: AppConstants.primaryGreen,
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // VIP Membership Header Card
              _buildVipHeaderCard(),

              const SizedBox(height: 20),

              // Quick Actions
              _buildQuickActions(),

              const SizedBox(height: 24),

              // Digital Stamp Progress
              _buildStampProgressCard(),

              const SizedBox(height: 24),

              // Featured Partner Stores
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text(
                    'متاجر وعلامات تجارية مميزة',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                  TextButton(
                    onPressed: () => widget.onTabChange(1), // go to stores tab
                    child: const Text('عرض الكل', style: TextStyle(color: AppConstants.primaryGreenLight)),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              _buildStoresList(),

              const SizedBox(height: 24),

              // Exclusive Mall Promotions Banner
              _buildPromotionsBanner(),

              const SizedBox(height: 20),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildVipHeaderCard() {
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
          BoxShadow(
            color: AppConstants.primaryGreen.withOpacity(0.25),
            blurRadius: 15,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'مرحباً بكِ، ${_customer?.fullName}',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                        decoration: BoxDecoration(
                          color: AppConstants.accentGold.withOpacity(0.3),
                          borderRadius: BorderRadius.circular(6),
                          border: Border.all(color: AppConstants.accentGoldLight, width: 1),
                        ),
                        child: Row(
                          children: [
                            const Icon(LucideIcons.sparkles, color: AppConstants.accentGoldLight, size: 12),
                            const SizedBox(width: 4),
                            Text(
                              _customer?.loyaltyTier ?? 'عضو VIP',
                              style: const TextStyle(color: AppConstants.accentGoldLight, fontSize: 11, fontWeight: FontWeight.bold),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ],
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
          const Divider(color: Colors.white24, height: 1),
          const SizedBox(height: 16),

          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('رصيد النقاط', style: TextStyle(color: Colors.white70, fontSize: 12)),
                    const SizedBox(height: 4),
                    Text(
                      '${_customer?.pointsBalance ?? 0}',
                      style: const TextStyle(color: Colors.white, fontSize: 24, fontWeight: FontWeight.bold),
                    ),
                  ],
                ),
              ),
              Container(width: 1, height: 40, color: Colors.white24),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.only(right: 16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('الاسترداد النقدي المتاح', style: TextStyle(color: Colors.white70, fontSize: 12)),
                      const SizedBox(height: 4),
                      Text(
                        '${_customer?.cashbackBalance.toStringAsFixed(2)} ${AppConstants.currency}',
                        style: const TextStyle(color: AppConstants.accentGoldLight, fontSize: 20, fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildQuickActions() {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        _actionItem(LucideIcons.scanLine, 'مسح فاتورة', AppConstants.primaryGreen, () => widget.onTabChange(2)),
        _actionItem(LucideIcons.stamp, 'بطاقة الأختام', const Color(0xFFD97706), () => widget.onTabChange(3)),
        _actionItem(LucideIcons.store, 'المتاجر', const Color(0xFF2563EB), () => widget.onTabChange(1)),
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

  Widget _buildStampProgressCard() {
    final stamp = _stamps.isNotEmpty ? _stamps.first : null;
    if (stamp == null) return const SizedBox.shrink();

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.grey.withOpacity(0.15)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.03),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  const Icon(LucideIcons.award, color: AppConstants.accentGold, size: 20),
                  const SizedBox(width: 8),
                  Text(
                    stamp.tenantName,
                    style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15),
                  ),
                ],
              ),
              Text(
                '${stamp.currentStamps}/${stamp.totalStampsNeeded} أختام',
                style: const TextStyle(
                  fontWeight: FontWeight.bold,
                  color: AppConstants.primaryGreen,
                  fontSize: 14,
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: stamp.progress,
              backgroundColor: Colors.grey.shade200,
              valueColor: const AlwaysStoppedAnimation<Color>(AppConstants.primaryGreen),
              minHeight: 8,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'المكافأة: ${stamp.rewardDescription}',
            style: TextStyle(fontSize: 12, color: Colors.grey.shade700),
          ),
        ],
      ),
    );
  }

  Widget _buildStoresList() {
    return Column(
      children: _stores.take(3).map((store) {
        return Card(
          margin: const EdgeInsets.only(bottom: 12),
          child: ListTile(
            contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            leading: Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: AppConstants.primaryGreen.withOpacity(0.08),
                borderRadius: BorderRadius.circular(10),
              ),
              child: const Icon(LucideIcons.store, color: AppConstants.primaryGreen),
            ),
            title: Text(store.nameAr, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14)),
            subtitle: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SizedBox(height: 2),
                Text('${store.category} • ${store.unitNumber} (${store.floor})', style: const TextStyle(fontSize: 12)),
                if (store.discountOffer != null) ...[
                  const SizedBox(height: 4),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                    decoration: BoxDecoration(
                      color: AppConstants.accentGold.withOpacity(0.12),
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: Text(
                      store.discountOffer!,
                      style: const TextStyle(color: AppConstants.accentGold, fontSize: 11, fontWeight: FontWeight.bold),
                    ),
                  ),
                ],
              ],
            ),
            trailing: const Icon(LucideIcons.chevronLeft, size: 18, color: Colors.grey),
          ),
        );
      }).toList(),
    );
  }

  Widget _buildPromotionsBanner() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFFEF3C7),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFFDE68A)),
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: AppConstants.accentGold,
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(LucideIcons.gift, color: Colors.white, size: 24),
          ),
          const SizedBox(width: 14),
          const Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'مهرجان التسوق الأسبوعي في المرأة مول',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Color(0xFF92400E)),
                ),
                SizedBox(height: 2),
                Text(
                  'ضاعفي نقاط ولائك عند الشراء من كافة المتاجر يوم الجمعة والسبت.',
                  style: TextStyle(fontSize: 12, color: Color(0xFF78350F)),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
