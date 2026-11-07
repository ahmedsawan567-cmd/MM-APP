import 'membership_tiers_screen.dart';
import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
import '../core/theme/theme_controller.dart';
import '../widgets/common.dart';
import '../widgets/server_settings_sheet.dart';
import 'account/change_password_screen.dart';
import 'account/edit_profile_screen.dart';
import 'account/membership_card_screen.dart';
import 'invoices_screen.dart';
import 'notifications_screen.dart';
import 'rewards_screen.dart';

class ProfileTab extends StatelessWidget {
  const ProfileTab({super.key});

  Future<void> _logout(BuildContext context) async {
    final ok = await showModalBottomSheet<bool>(
      context: context,
      builder: (ctx) => Padding(
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const IconBadge(LucideIcons.logOut, tone: Tone.danger, size: 60),
            const SizedBox(height: 14),
            Text('تسجيل الخروج؟', style: Theme.of(ctx).textTheme.titleLarge),
            const SizedBox(height: 6),
            Text('يمكنك العودة في أي وقت برقم هاتفك وكلمة المرور.',
                textAlign: TextAlign.center,
                style: TextStyle(color: ctx.palette.muted)),
            const SizedBox(height: 22),
            Row(
              children: [
                Expanded(
                    child: OutlinedButton(
                        onPressed: () => Navigator.pop(ctx, false),
                        child: const Text('إلغاء'))),
                const SizedBox(width: 12),
                Expanded(
                  child: ElevatedButton(
                    style: ElevatedButton.styleFrom(
                        backgroundColor: ctx.palette.danger,
                        foregroundColor: Colors.white),
                    onPressed: () => Navigator.pop(ctx, true),
                    child: const Text('خروج'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
    if (ok == true) await Session.instance.logout();
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Scaffold(
      appBar: AppBar(title: const Text('حسابي')),
      body: ListenableBuilder(
        listenable: Session.instance,
        builder: (context, _) {
          final user = Session.instance.user;
          if (user == null) return const LoadingView();
          return RefreshIndicator(
            onRefresh: () =>
                Session.instance.refreshProfile().catchError((_) {}),
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 28),
              children: [
                FadeSlideIn(
                  child: AppCard(
                    padding: const EdgeInsets.all(18),
                    child: Row(
                      children: [
                        Container(
                          width: 64,
                          height: 64,
                          decoration: const BoxDecoration(
                              shape: BoxShape.circle,
                              gradient: Brand.vividGradient),
                          alignment: Alignment.center,
                          child: Text(
                              user.fullName.isNotEmpty
                                  ? user.fullName.characters.first
                                  : '?',
                              style: const TextStyle(
                                  fontSize: 26,
                                  color: Colors.white,
                                  fontWeight: FontWeight.w800)),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(user.fullName,
                                  style:
                                      Theme.of(context).textTheme.titleMedium),
                              const SizedBox(height: 2),
                              Text(user.phoneNumber,
                                  textDirection: TextDirection.ltr,
                                  style: TextStyle(color: p.muted)),
                              const SizedBox(height: 6),
                              if (user.membershipNumber != null)
                                Text(user.membershipNumber!,
                                    textDirection: TextDirection.ltr,
                                    style: TextStyle(
                                        fontSize: 12,
                                        color: p.brand,
                                        fontWeight: FontWeight.w800,
                                        letterSpacing: 0.5))
                              else
                                Text('عضو منذ ${formatDate(user.createdAt)}',
                                    style: TextStyle(
                                        fontSize: 12, color: p.muted)),
                            ],
                          ),
                        ),
                        Column(
                          children: [
                            Text(formatPoints(user.pointsBalance),
                                style: TextStyle(
                                    fontWeight: FontWeight.w800,
                                    fontSize: 18,
                                    color: p.gold)),
                            Text('نقطة',
                                style: TextStyle(fontSize: 11, color: p.muted)),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 22),
                _label(context, 'حسابي'),
                _Group(children: [
                  _Item(
                      icon: LucideIcons.idCard,
                      tone: Tone.brand,
                      label: 'بطاقة العضوية',
                      onTap: () =>
                          pushPage(context, const MembershipCardScreen())),
                  _Item(
                      icon: LucideIcons.crown,
                      tone: Tone.gold,
                      label: 'عضويتي ومزاياها',
                      onTap: () =>
                          pushPage(context, const MembershipTiersScreen())),
                  _Item(
                      icon: LucideIcons.userPen,
                      tone: Tone.info,
                      label: 'تعديل بياناتي',
                      onTap: () =>
                          pushPage(context, const EditProfileScreen())),
                  _Item(
                      icon: LucideIcons.keyRound,
                      tone: Tone.gold,
                      label: 'تغيير كلمة المرور',
                      onTap: () =>
                          pushPage(context, const ChangePasswordScreen())),
                ]),
                const SizedBox(height: 22),
                _label(context, 'نشاطي'),
                _Group(children: [
                  _Item(
                      icon: LucideIcons.bell,
                      tone: Tone.brand,
                      label: 'الإشعارات',
                      onTap: () =>
                          pushPage(context, const NotificationsScreen())),
                  _Item(
                      icon: LucideIcons.receiptText,
                      tone: Tone.info,
                      label: 'فواتيري',
                      onTap: () => pushPage(context, const InvoicesScreen())),
                  _Item(
                      icon: LucideIcons.gift,
                      tone: Tone.gold,
                      label: 'المكافآت',
                      onTap: () => pushPage(context, const RewardsScreen())),
                  _Item(
                      icon: LucideIcons.circleHelp,
                      tone: Tone.brand,
                      label: 'كيف أكسب النقاط؟',
                      onTap: () => _howItWorks(context)),
                ]),
                const SizedBox(height: 22),
                _label(context, 'الاتصال'),
                _Group(children: [
                  _Item(
                      icon: LucideIcons.server,
                      tone: Tone.info,
                      label: 'إعدادات الخادم',
                      onTap: () => showServerSettings(context)),
                ]),
                const SizedBox(height: 22),
                _label(context, 'المظهر'),
                const _ThemeSelector(),
                const SizedBox(height: 22),
                _Group(children: [
                  _Item(
                      icon: LucideIcons.logOut,
                      tone: Tone.danger,
                      label: 'تسجيل الخروج',
                      danger: true,
                      onTap: () => _logout(context)),
                ]),
                const SizedBox(height: 10),
                Center(
                  child: TextButton(
                    onPressed: () => _deleteAccount(context),
                    child: Text('حذف الحساب نهائياً',
                        style: TextStyle(color: p.danger, fontSize: 13)),
                  ),
                ),
                const SizedBox(height: 22),
                Center(
                  child: Text('${AppConstants.appNameAr} • الإصدار 1.0.0',
                      style: TextStyle(fontSize: 12, color: p.muted)),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Future<void> _deleteAccount(BuildContext context) async {
    final password = TextEditingController();
    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => Padding(
        padding: EdgeInsets.fromLTRB(
            24, 0, 24, 28 + MediaQuery.of(ctx).viewInsets.bottom),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const IconBadge(LucideIcons.userX, tone: Tone.danger, size: 60),
            const SizedBox(height: 14),
            Text('حذف الحساب نهائياً؟',
                style: Theme.of(ctx).textTheme.titleLarge),
            const SizedBox(height: 8),
            Text(
              'ستفقد نقاطك وقسائمك ولا يمكن التراجع. تُمسح بياناتك الشخصية وتبقى سجلات الفواتير بدون هويتك.',
              textAlign: TextAlign.center,
              style: TextStyle(color: ctx.palette.muted, height: 1.5),
            ),
            const SizedBox(height: 18),
            TextField(
              controller: password,
              obscureText: true,
              decoration: const InputDecoration(
                  labelText: 'كلمة المرور للتأكيد',
                  prefixIcon: Icon(LucideIcons.lock, size: 20)),
            ),
            const SizedBox(height: 20),
            Row(
              children: [
                Expanded(
                    child: OutlinedButton(
                        onPressed: () => Navigator.pop(ctx, false),
                        child: const Text('إلغاء'))),
                const SizedBox(width: 12),
                Expanded(
                  child: ElevatedButton(
                    style: ElevatedButton.styleFrom(
                        backgroundColor: ctx.palette.danger,
                        foregroundColor: Colors.white),
                    onPressed: () => Navigator.pop(ctx, true),
                    child: const Text('حذف الحساب'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
    if (confirmed != true || !context.mounted) return;
    if (password.text.isEmpty) {
      showMessage(context, 'أدخل كلمة المرور لتأكيد الحذف', error: true);
      return;
    }
    try {
      await Session.instance.deleteAccount(password.text);
    } catch (e) {
      if (context.mounted) showMessage(context, e.toString(), error: true);
    }
  }

  Widget _label(BuildContext context, String text) => Padding(
        padding: const EdgeInsets.fromLTRB(6, 0, 6, 8),
        child: Text(text,
            style: TextStyle(
                color: context.palette.muted,
                fontWeight: FontWeight.w700,
                fontSize: 13)),
      );

  void _howItWorks(BuildContext context) {
    const steps = [
      (LucideIcons.shoppingBag, 'تسوّق من أي متجر مشارك في المول'),
      (LucideIcons.camera, 'صوّر الفاتورة من تبويب "مسح"'),
      (LucideIcons.coins, 'تُضاف النقاط لمحفظتك بعد قراءة الفاتورة'),
      (LucideIcons.gift, 'استبدل النقاط بقسائم خصم من المكافآت'),
    ];
    showModalBottomSheet(
      context: context,
      builder: (ctx) => Padding(
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('كيف أكسب النقاط؟', style: Theme.of(ctx).textTheme.titleLarge),
            const SizedBox(height: 18),
            for (var i = 0; i < steps.length; i++)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 8),
                child: Row(
                  children: [
                    IconBadge(steps[i].$1, size: 42),
                    const SizedBox(width: 14),
                    Expanded(
                        child: Text(steps[i].$2,
                            style:
                                const TextStyle(fontWeight: FontWeight.w600))),
                    Text('${i + 1}',
                        style: TextStyle(
                            color: ctx.palette.muted,
                            fontWeight: FontWeight.w800)),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _ThemeSelector extends StatelessWidget {
  const _ThemeSelector();

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<ThemeMode>(
      valueListenable: ThemeController.instance,
      builder: (context, mode, _) => SizedBox(
        width: double.infinity,
        child: SegmentedButton<ThemeMode>(
          showSelectedIcon: false,
          style: SegmentedButton.styleFrom(
            minimumSize: const Size(0, 48),
            shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.md)),
          ),
          segments: const [
            ButtonSegment(
                value: ThemeMode.light,
                icon: Icon(LucideIcons.sun, size: 18),
                label: Text('فاتح')),
            ButtonSegment(
                value: ThemeMode.dark,
                icon: Icon(LucideIcons.moon, size: 18),
                label: Text('داكن')),
            ButtonSegment(
                value: ThemeMode.system,
                icon: Icon(LucideIcons.smartphone, size: 18),
                label: Text('تلقائي')),
          ],
          selected: {mode},
          onSelectionChanged: (s) => ThemeController.instance.set(s.first),
        ),
      ),
    );
  }
}

class _Group extends StatelessWidget {
  final List<Widget> children;
  const _Group({required this.children});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: p.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: p.border),
      ),
      child: Column(
        children: [
          for (var i = 0; i < children.length; i++) ...[
            if (i > 0) const Divider(indent: 70),
            children[i],
          ],
        ],
      ),
    );
  }
}

class _Item extends StatelessWidget {
  final IconData icon;
  final Tone tone;
  final String label;
  final VoidCallback onTap;
  final bool danger;

  const _Item(
      {required this.icon,
      required this.tone,
      required this.label,
      required this.onTap,
      this.danger = false});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
      leading: IconBadge(icon, tone: tone, size: 40),
      title: Text(label,
          style: TextStyle(
              fontWeight: FontWeight.w700, color: danger ? p.danger : p.ink)),
      trailing: danger
          ? null
          : Icon(LucideIcons.chevronLeft, color: p.muted, size: 20),
      onTap: onTap,
    );
  }
}
