import 'package:flutter/material.dart';
import 'package:lucide_icons/lucide_icons.dart';
import '../core/constants/app_constants.dart';
import '../core/services/supabase_service.dart';
import '../models/customer_model.dart';
import '../models/stamp_model.dart';

class WalletTab extends StatefulWidget {
  const WalletTab({super.key});

  @override
  State<WalletTab> createState() => _WalletTabState();
}

class _WalletTabState extends State<WalletTab> {
  CustomerModel? _customer;
  List<StampCardModel> _stamps = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadWallet();
  }

  Future<void> _loadWallet() async {
    final customer = await SupabaseService.instance.getCustomerProfile('CUST-SARAH');
    final stamps = await SupabaseService.instance.getCustomerStamps('CUST-SARAH');
    if (mounted) {
      setState(() {
        _customer = customer;
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
        title: const Text('المحفظة وبطاقات الأختام'),
      ),
      body: RefreshIndicator(
        onRefresh: _loadWallet,
        color: AppConstants.primaryGreen,
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Digital VIP Card with QR Code
              _buildDigitalMembershipCard(),

              const SizedBox(height: 24),

              // Digital Stamps Section (الأختام الرقمية)
              const Row(
                children: [
                  Icon(LucideIcons.stamp, color: AppConstants.accentGold, size: 22),
                  SizedBox(width: 8),
                  Text(
                    'بطاقات الأختام الرقمية للمتاجر',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              ..._stamps.map((stamp) => _buildStampCardItem(stamp)),

              const SizedBox(height: 24),

              // Redeemable Vouchers & Gift Cards
              const Row(
                children: [
                  Icon(LucideIcons.gift, color: AppConstants.primaryGreen, size: 22),
                  SizedBox(width: 8),
                  Text(
                    'قسائم الشراء والمكافآت المتاحة',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              _buildVoucherItem('قسيمة مشتريات 50 دينار', 'صالحة في دار الحرير للأزياء', 500, AppConstants.primaryGreen),
              _buildVoucherItem('استرداد نقدي فوري 25 دينار', 'صالحة في كافة مقاهي المول', 300, AppConstants.accentGold),
              _buildVoucherItem('عطر ملكي فاخر مجاناً', 'صالحة لدى العنبر الملكي', 1000, const Color(0xFF7C3AED)),

              const SizedBox(height: 20),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildDigitalMembershipCard() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(22),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF0F172A), Color(0xFF1E293B), Color(0xFF0F172A)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: AppConstants.accentGold.withOpacity(0.4), width: 1.5),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.3),
            blurRadius: 20,
            offset: const Offset(0, 8),
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
                  const Text(
                    AppConstants.appNameAr,
                    style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                  Text(
                    'بطاقة العضوية الذهبية VIP',
                    style: TextStyle(color: AppConstants.accentGoldLight.withOpacity(0.9), fontSize: 12),
                  ),
                ],
              ),
              const Icon(LucideIcons.crown, color: AppConstants.accentGoldLight, size: 28),
            ],
          ),

          const SizedBox(height: 24),

          Center(
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Column(
                children: [
                  // Simulated QR / Barcode Visual
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(LucideIcons.qrCode, size: 54, color: Color(0xFF0F172A)),
                      const SizedBox(width: 14),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text('امسحي الكود عند الدفع', style: TextStyle(fontSize: 11, color: Colors.grey)),
                          Text(
                            'VIP-${_customer?.phoneNumber ?? "0912345678"}',
                            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13, letterSpacing: 1),
                          ),
                        ],
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),

          const SizedBox(height: 20),

          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                _customer?.fullName ?? 'سارة المنصوري',
                style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 14),
              ),
              Text(
                'الرصيد: ${_customer?.pointsBalance ?? 0} نقطة',
                style: const TextStyle(color: AppConstants.accentGoldLight, fontWeight: FontWeight.bold, fontSize: 14),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildStampCardItem(StampCardModel stamp) {
    return Card(
      margin: const EdgeInsets.only(bottom: 16),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  stamp.tenantName,
                  style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: AppConstants.primaryGreen.withOpacity(0.1),
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Text(
                    '${stamp.currentStamps} من ${stamp.totalStampsNeeded}',
                    style: const TextStyle(
                      color: AppConstants.primaryGreen,
                      fontWeight: FontWeight.bold,
                      fontSize: 12,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),

            // Stamps Grid Visual (10 slots)
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: List.generate(stamp.totalStampsNeeded, (index) {
                final isStamped = index < stamp.currentStamps;
                return Container(
                  width: 32,
                  height: 32,
                  decoration: BoxDecoration(
                    color: isStamped ? AppConstants.accentGold : Colors.grey.shade100,
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: isStamped ? AppConstants.accentGoldLight : Colors.grey.shade300,
                      width: 1.5,
                    ),
                  ),
                  child: Center(
                    child: isStamped
                        ? const Icon(LucideIcons.stamp, size: 16, color: Colors.white)
                        : Text(
                            '${index + 1}',
                            style: TextStyle(color: Colors.grey.shade400, fontSize: 11, fontWeight: FontWeight.bold),
                          ),
                  ),
                );
              }),
            ),

            const SizedBox(height: 12),
            Text(
              'المكافأة: ${stamp.rewardDescription}',
              style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildVoucherItem(String title, String subtitle, int pointsNeeded, Color accentColor) {
    final bool canRedeem = (_customer?.pointsBalance ?? 0) >= pointsNeeded;

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: ListTile(
        leading: Container(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: accentColor.withOpacity(0.1),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Icon(LucideIcons.tag, color: accentColor, size: 22),
        ),
        title: Text(title, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14)),
        subtitle: Text(subtitle, style: const TextStyle(fontSize: 12)),
        trailing: ElevatedButton(
          style: ElevatedButton.styleFrom(
            backgroundColor: canRedeem ? AppConstants.primaryGreen : Colors.grey.shade300,
            foregroundColor: canRedeem ? Colors.white : Colors.grey.shade600,
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            elevation: 0,
          ),
          onPressed: canRedeem
              ? () {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text('تم استبدال $pointsNeeded نقطة بنجاح وتفعيل القسيمة!'),
                      backgroundColor: AppConstants.primaryGreen,
                    ),
                  );
                }
              : null,
          child: Text(
            canRedeem ? '$pointsNeeded نقطة' : 'غير كافٍ',
            style: const TextStyle(fontSize: 11, fontWeight: FontWeight.bold),
          ),
        ),
      ),
    );
  }
}
