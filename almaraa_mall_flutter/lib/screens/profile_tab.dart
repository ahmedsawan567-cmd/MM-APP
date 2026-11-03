import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/session.dart';
import '../widgets/common.dart';
import 'invoices_screen.dart';
import 'rewards_screen.dart';

class ProfileTab extends StatelessWidget {
  const ProfileTab({super.key});

  Future<void> _logout(BuildContext context) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تسجيل الخروج'),
        content: const Text('هل تريد تسجيل الخروج من حسابك؟'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: AppConstants.dangerColor),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('خروج'),
          ),
        ],
      ),
    );
    if (ok == true) await Session.instance.logout();
  }

  @override
  Widget build(BuildContext context) {
    void push(Widget page) => Navigator.of(context).push(MaterialPageRoute(builder: (_) => page));

    return Scaffold(
      appBar: AppBar(title: const Text('حسابي')),
      body: ListenableBuilder(
        listenable: Session.instance,
        builder: (context, _) {
          final user = Session.instance.user;
          if (user == null) return const LoadingView();
          return RefreshIndicator(
            color: AppConstants.primaryGreen,
            onRefresh: () => Session.instance.refreshProfile().catchError((_) {}),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      children: [
                        CircleAvatar(
                          radius: 36,
                          backgroundColor: AppConstants.primaryGreen,
                          child: Text(
                            user.fullName.isNotEmpty ? user.fullName.characters.first : '?',
                            style: const TextStyle(fontSize: 30, color: Colors.white, fontWeight: FontWeight.bold),
                          ),
                        ),
                        const SizedBox(height: 12),
                        Text(user.fullName, style: const TextStyle(fontSize: 19, fontWeight: FontWeight.bold)),
                        const SizedBox(height: 4),
                        Text(user.phoneNumber,
                            textDirection: TextDirection.ltr, style: const TextStyle(color: AppConstants.textMuted)),
                        const SizedBox(height: 4),
                        Text('عضو منذ ${formatDate(user.createdAt)}',
                            style: const TextStyle(fontSize: 12, color: AppConstants.textMuted)),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                Card(
                  child: Column(
                    children: [
                      _item(LucideIcons.receipt, 'سجل الفواتير', () => push(const InvoicesScreen())),
                      const Divider(height: 1),
                      _item(LucideIcons.gift, 'كتالوج المكافآت', () => push(const RewardsScreen())),
                      const Divider(height: 1),
                      _item(LucideIcons.helpCircle, 'كيف أكسب النقاط؟', () => _showHowItWorks(context)),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                Card(
                  child: _item(LucideIcons.logOut, 'تسجيل الخروج', () => _logout(context), color: AppConstants.dangerColor),
                ),
                const SizedBox(height: 16),
                const Center(
                  child: Text('${AppConstants.appNameAr} • الإصدار 1.0.0',
                      style: TextStyle(fontSize: 12, color: AppConstants.textMuted)),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _item(IconData icon, String label, VoidCallback onTap, {Color? color}) => ListTile(
        leading: Icon(icon, color: color ?? AppConstants.primaryGreen),
        title: Text(label, style: TextStyle(color: color, fontWeight: FontWeight.w600)),
        trailing: color == null ? const Icon(Icons.chevron_left) : null,
        onTap: onTap,
      );

  void _showHowItWorks(BuildContext context) {
    const steps = [
      (LucideIcons.shoppingBag, 'تسوّق من أي متجر مشارك في المول'),
      (LucideIcons.camera, 'صوّر الفاتورة من تبويب "مسح فاتورة"'),
      (LucideIcons.coins, 'تُضاف النقاط لمحفظتك بعد قراءة الفاتورة ومراجعتها'),
      (LucideIcons.gift, 'استبدل النقاط بقسائم خصم من كتالوج المكافآت'),
    ];
    showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(24))),
      builder: (ctx) => Padding(
        padding: const EdgeInsets.fromLTRB(24, 24, 24, 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('كيف أكسب النقاط؟', style: TextStyle(fontSize: 19, fontWeight: FontWeight.bold)),
            const SizedBox(height: 16),
            for (var i = 0; i < steps.length; i++)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 8),
                child: Row(
                  children: [
                    CircleAvatar(
                      radius: 18,
                      backgroundColor: AppConstants.primaryGreen.withOpacity(0.1),
                      child: Icon(steps[i].$1, size: 18, color: AppConstants.primaryGreen),
                    ),
                    const SizedBox(width: 12),
                    Expanded(child: Text('${i + 1}. ${steps[i].$2}')),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}
