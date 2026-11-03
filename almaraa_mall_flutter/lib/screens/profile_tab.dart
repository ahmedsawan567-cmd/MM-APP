import 'package:flutter/material.dart';
import 'package:lucide_icons/lucide_icons.dart';
import '../core/constants/app_constants.dart';
import '../core/services/supabase_service.dart';
import '../models/customer_model.dart';
import '../models/invoice_model.dart';

class ProfileTab extends StatefulWidget {
  const ProfileTab({super.key});

  @override
  State<ProfileTab> createState() => _ProfileTabState();
}

class _ProfileTabState extends State<ProfileTab> {
  CustomerModel? _customer;
  List<InvoiceModel> _invoices = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadProfile();
  }

  Future<void> _loadProfile() async {
    final customer = await SupabaseService.instance.getCustomerProfile('CUST-SARAH');
    final invoices = await SupabaseService.instance.getInvoices('CUST-SARAH');
    if (mounted) {
      setState(() {
        _customer = customer;
        _invoices = invoices;
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
        title: const Text('الملف الشخصي والأرشيف'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            // User Header
            Center(
              child: Column(
                children: [
                  Container(
                    width: 76,
                    height: 76,
                    decoration: BoxDecoration(
                      color: AppConstants.primaryGreen.withOpacity(0.1),
                      shape: BoxShape.circle,
                      border: Border.all(color: AppConstants.accentGold, width: 2),
                    ),
                    child: const Icon(LucideIcons.user, size: 36, color: AppConstants.primaryGreen),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    _customer?.fullName ?? 'سارة المنصوري',
                    style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    _customer?.phoneNumber ?? '0912345678',
                    style: TextStyle(color: Colors.grey.shade600, fontSize: 13),
                  ),
                  const SizedBox(height: 6),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppConstants.accentGold.withOpacity(0.15),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: Text(
                      _customer?.loyaltyTier ?? 'عضوية VIP',
                      style: const TextStyle(color: AppConstants.accentGold, fontWeight: FontWeight.bold, fontSize: 12),
                    ),
                  ),
                ],
              ),
            ),

            const SizedBox(height: 24),

            // Invoices Archive
            Align(
              alignment: Alignment.centerRight,
              child: Row(
                children: [
                  const Icon(LucideIcons.receipt, color: AppConstants.primaryGreen, size: 20),
                  const SizedBox(width: 8),
                  const Text('أرشيف الفواتير الممسوحة', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                  const Spacer(),
                  Text('${_invoices.length} فواتير', style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
                ],
              ),
            ),
            const SizedBox(height: 12),

            ..._invoices.map((inv) => _buildInvoiceItem(inv)),

            const SizedBox(height: 20),

            // Settings & Support Options
            Card(
              child: Column(
                children: [
                  ListTile(
                    leading: const Icon(LucideIcons.globe, color: AppConstants.primaryGreen),
                    title: const Text('لغة التطبيق (Language)'),
                    trailing: const Text('العربية', style: TextStyle(fontWeight: FontWeight.bold, color: AppConstants.primaryGreen)),
                    onTap: () {},
                  ),
                  const Divider(height: 1),
                  ListTile(
                    leading: const Icon(LucideIcons.shieldCheck, color: AppConstants.primaryGreen),
                    title: const Text('سياسة الخصوصية وحماية البيانات'),
                    trailing: const Icon(LucideIcons.chevronLeft, size: 18),
                    onTap: () {},
                  ),
                  const Divider(height: 1),
                  ListTile(
                    leading: const Icon(LucideIcons.headphones, color: AppConstants.primaryGreen),
                    title: const Text('الدعم الفني وخدمة العملاء'),
                    trailing: const Icon(LucideIcons.chevronLeft, size: 18),
                    onTap: () {},
                  ),
                ],
              ),
            ),

            const SizedBox(height: 20),
          ],
        ),
      ),
    );
  }

  Widget _buildInvoiceItem(InvoiceModel inv) {
    final bool isApproved = inv.processingStatus == 'APPROVED';
    final Color statusColor = isApproved ? AppConstants.successColor : AppConstants.warningColor;
    final String statusText = isApproved ? 'معتمدة ومقيدة' : 'قيد المراجعة';

    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
        leading: Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: statusColor.withOpacity(0.1),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Icon(isApproved ? LucideIcons.checkCheck : LucideIcons.clock, color: statusColor, size: 20),
        ),
        title: Text(inv.tenantName, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14)),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('${inv.invoiceNumber} • ${inv.totalAmount.toStringAsFixed(2)} ${AppConstants.currency}', style: const TextStyle(fontSize: 12)),
            Text(statusText, style: TextStyle(color: statusColor, fontSize: 11, fontWeight: FontWeight.bold)),
          ],
        ),
        trailing: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Text(
              '+${inv.pointsAwarded} نقطة',
              style: const TextStyle(fontWeight: FontWeight.bold, color: AppConstants.primaryGreen, fontSize: 13),
            ),
            Text(
              '+${inv.cashbackAwarded.toStringAsFixed(2)} ${AppConstants.currency}',
              style: const TextStyle(color: AppConstants.accentGold, fontSize: 11),
            ),
          ],
        ),
      ),
    );
  }
}
