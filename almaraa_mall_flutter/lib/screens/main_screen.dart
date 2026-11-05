import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/services/notifications_service.dart';
import '../core/services/push_service.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
import '../widgets/common.dart';
import 'notifications_screen.dart';

import 'home_tab.dart';
import 'profile_tab.dart';
import 'scan_tab.dart';
import 'stores_tab.dart';
import 'wallet_tab.dart';

class MainScreen extends StatefulWidget {
  const MainScreen({super.key});

  @override
  State<MainScreen> createState() => _MainScreenState();
}

class _MainScreenState extends State<MainScreen> with WidgetsBindingObserver {
  int _index = 0;
  Timer? _poll;

  void _go(int index) => setState(() => _index = index);

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _checkNow();
    // أثناء فتح التطبيق: فحص كل 30 ثانية، وأي إشعار جديد يظهر على الهاتف فوراً
    _poll = Timer.periodic(const Duration(seconds: 30), (_) => _checkNow());
    AppNotifications.openInbox.addListener(_openInboxIfRequested);
    AppNotifications.onRemoteMessage.addListener(Session.instance.refreshUnread);
    Session.instance.requestedTab.addListener(_onTabRequested);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _openInboxIfRequested();
      _askForPermissionOnce();
    });
  }

  @override
  void dispose() {
    _poll?.cancel();
    AppNotifications.openInbox.removeListener(_openInboxIfRequested);
    AppNotifications.onRemoteMessage.removeListener(Session.instance.refreshUnread);
    Session.instance.requestedTab.removeListener(_onTabRequested);
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _checkNow();
  }

  /// يعرض على الهاتف الإشعارات الجديدة (إن لم تكن الإشعارات الفورية مفعّلة) ثم يحدّث العدّاد.
  Future<void> _checkNow() async {
    if (!PushService.active) {
      try {
        await AppNotifications.checkAndNotify();
      } catch (_) {}
    }
    await Session.instance.refreshUnread();
  }

  void _onTabRequested() {
    final tab = Session.instance.requestedTab.value;
    if (tab == null || !mounted) return;
    Session.instance.requestedTab.value = null;
    Navigator.of(context).popUntil((r) => r.isFirst);
    _go(tab);
  }

  void _openInboxIfRequested() {
    if (!AppNotifications.openInbox.value || !mounted) return;
    AppNotifications.openInbox.value = false;
    pushPage(context, const NotificationsScreen());
  }

  /// أول مرة بعد الدخول: نشرح فائدة الإشعارات ثم نطلب إذن النظام.
  Future<void> _askForPermissionOnce() async {
    if (await AppNotifications.wasAsked() || await AppNotifications.isEnabled() || !mounted) return;
    final accept = await showModalBottomSheet<bool>(
      context: context,
      builder: (ctx) => Padding(
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const IconBadge(LucideIcons.bellRing, size: 64),
            const SizedBox(height: 16),
            Text('فعّل الإشعارات', style: Theme.of(ctx).textTheme.titleLarge),
            const SizedBox(height: 8),
            Text(
              'نخبرك فور إضافة نقاطك أو مراجعة فاتورتك، وبأحدث عروض المول.',
              textAlign: TextAlign.center,
              style: TextStyle(color: ctx.palette.muted, height: 1.6),
            ),
            const SizedBox(height: 22),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('تفعيل الإشعارات')),
            ),
            const SizedBox(height: 8),
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('ليس الآن')),
          ],
        ),
      ),
    );
    if (accept == true) {
      await AppNotifications.requestPermission();
    } else {
      // نتذكر الرفض حتى لا نكرر السؤال؛ يمكن التفعيل لاحقاً من صفحة الإشعارات أو حسابي
      await AppNotifications.markAsked();
    }
  }

  @override
  Widget build(BuildContext context) {
    final tabs = [
      HomeTab(onTabChange: _go),
      const StoresTab(),
      ScanTab(onTabChange: _go),
      const WalletTab(),
      const ProfileTab(),
    ];

    return Scaffold(
      body: IndexedStack(index: _index, children: tabs),
      bottomNavigationBar: DecoratedBox(
        decoration: BoxDecoration(border: Border(top: BorderSide(color: Theme.of(context).dividerColor))),
        child: NavigationBar(
          selectedIndex: _index,
          onDestinationSelected: _go,
          destinations: const [
            NavigationDestination(icon: Icon(LucideIcons.house), label: 'الرئيسية'),
            NavigationDestination(icon: Icon(LucideIcons.store), label: 'المتاجر'),
            NavigationDestination(icon: Icon(LucideIcons.scanLine), label: 'مسح'),
            NavigationDestination(icon: Icon(LucideIcons.wallet), label: 'المحفظة'),
            NavigationDestination(icon: Icon(LucideIcons.user), label: 'حسابي'),
          ],
        ),
      ),
    );
  }
}
