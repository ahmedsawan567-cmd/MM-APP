import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/services/loyalty_api.dart';
import '../core/services/notifications_service.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
import '../models/app_notification.dart';
import '../widgets/common.dart';
import 'invoices_screen.dart';
import 'rewards_screen.dart';

/// صندوق الإشعارات: نتائج الفواتير، تعديلات النقاط، وإعلانات المول.
class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  Future<(List<AppNotification>, int)>? _future;
  List<AppNotification> _items = [];
  bool _permissionOff = false;

  @override
  void initState() {
    super.initState();
    _load();
    AppNotifications.isEnabled().then((on) {
      if (mounted) setState(() => _permissionOff = !on);
    });
  }

  void _load() => setState(() => _future = LoyaltyApi.getNotifications().then((r) {
        _items = r.$1;
        Session.instance.unreadNotifications.value = r.$2;
        return r;
      }));

  Future<void> _markAll() async {
    try {
      await LoyaltyApi.markAllNotificationsRead();
      setState(() => _items = [for (final n in _items) n.copyWith(isRead: true)]);
      Session.instance.unreadNotifications.value = 0;
    } catch (e) {
      if (mounted) showMessage(context, e.toString(), error: true);
    }
  }

  Future<void> _open(AppNotification n) async {
    if (!n.isRead) {
      LoyaltyApi.markNotificationRead(n.id).ignore();
      setState(() => _items = [for (final x in _items) x.id == n.id ? x.copyWith(isRead: true) : x]);
      final u = Session.instance.unreadNotifications;
      if (u.value > 0) u.value--;
    }
    final link = n.link ?? '';
    if (link.startsWith('invoice:')) {
      pushPage(context, const InvoicesScreen());
    } else if (n.type == 'POINTS_ADJUSTED' || n.type == 'INVOICE_APPROVED') {
      pushPage(context, const RewardsScreen());
    }
  }

  Future<void> _enable() async {
    await AppNotifications.requestPermission();
    final on = await AppNotifications.isEnabled();
    if (mounted) setState(() => _permissionOff = !on);
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Scaffold(
      appBar: AppBar(
        title: const Text('الإشعارات'),
        actions: [
          if (_items.any((n) => !n.isRead))
            TextButton(onPressed: _markAll, child: const Text('قراءة الكل')),
        ],
      ),
      body: AsyncView<(List<AppNotification>, int)>(
        future: _future,
        onRetry: _load,
        loading: const SkeletonList(itemHeight: 84, count: 6),
        builder: (context, _) => RefreshIndicator(
          onRefresh: () async {
            _load();
            await _future;
          },
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
            children: [
              if (_permissionOff)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: AppCard(
                    padding: const EdgeInsets.all(14),
                    child: Row(
                      children: [
                        const IconBadge(LucideIcons.bellOff, tone: Tone.gold, size: 42),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Text('إشعارات الهاتف متوقفة. فعّلها لتعرف فوراً متى تُضاف نقاطك.',
                              style: TextStyle(color: p.ink, height: 1.5)),
                        ),
                        TextButton(onPressed: _enable, child: const Text('تفعيل')),
                      ],
                    ),
                  ),
                ),
              if (_items.isEmpty)
                const Padding(
                  padding: EdgeInsets.only(top: 60),
                  child: EmptyView(
                    icon: LucideIcons.bell,
                    title: 'لا توجد إشعارات بعد',
                    subtitle: 'ستصلك هنا نتائج فواتيرك ونقاطك وعروض المول',
                  ),
                )
              else
                for (var i = 0; i < _items.length; i++)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: FadeSlideIn(index: i, child: _NotificationTile(item: _items[i], onTap: () => _open(_items[i]))),
                  ),
            ],
          ),
        ),
      ),
    );
  }
}

class _NotificationTile extends StatelessWidget {
  final AppNotification item;
  final VoidCallback onTap;

  const _NotificationTile({required this.item, required this.onTap});

  (IconData, Tone) get _look => switch (item.type) {
        'INVOICE_APPROVED' => (LucideIcons.circleCheck, Tone.success),
        'INVOICE_REJECTED' || 'INVOICE_VOIDED' => (LucideIcons.circleX, Tone.danger),
        'INVOICE_IN_REVIEW' => (LucideIcons.hourglass, Tone.info),
        'INVOICE_RESUBMIT_REQUESTED' => (LucideIcons.camera, Tone.gold),
        'POINTS_ADJUSTED' => (LucideIcons.coins, Tone.gold),
        'BROADCAST' => (LucideIcons.megaphone, Tone.brand),
        _ => (LucideIcons.bell, Tone.brand),
      };

  static String _ago(DateTime t) {
    final d = DateTime.now().difference(t);
    if (d.inMinutes < 1) return 'الآن';
    if (d.inMinutes < 60) return 'منذ ${d.inMinutes} دقيقة';
    if (d.inHours < 24) return 'منذ ${d.inHours} ساعة';
    if (d.inDays < 7) return 'منذ ${d.inDays} يوم';
    return formatDate(t);
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final (icon, tone) = _look;
    return Material(
      color: item.isRead ? p.surface : p.brandSoft,
      borderRadius: BorderRadius.circular(AppRadius.lg),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadius.lg),
            border: Border.all(color: item.isRead ? p.border : p.brand.withValues(alpha: 0.25)),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              IconBadge(icon, tone: tone, size: 42),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(item.title,
                              style: TextStyle(fontWeight: item.isRead ? FontWeight.w600 : FontWeight.w800, fontSize: 15)),
                        ),
                        if (!item.isRead)
                          Container(width: 8, height: 8, decoration: BoxDecoration(color: p.brand, shape: BoxShape.circle)),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(item.body, style: TextStyle(color: p.muted, height: 1.5, fontSize: 13)),
                    const SizedBox(height: 6),
                    Text(_ago(item.createdAt), style: TextStyle(color: p.muted, fontSize: 11)),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// زر الجرس مع عدد غير المقروء (يُستخدم في الرئيسية).
class NotificationBell extends StatelessWidget {
  const NotificationBell({super.key});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return ValueListenableBuilder<int>(
      valueListenable: Session.instance.unreadNotifications,
      builder: (context, count, _) => Stack(
        clipBehavior: Clip.none,
        children: [
          Material(
            color: p.surface,
            borderRadius: BorderRadius.circular(15),
            child: InkWell(
              borderRadius: BorderRadius.circular(15),
              onTap: () => pushPage(context, const NotificationsScreen()),
              child: Container(
                width: 46,
                height: 46,
                decoration: BoxDecoration(borderRadius: BorderRadius.circular(15), border: Border.all(color: p.border)),
                child: Icon(LucideIcons.bell, color: p.ink, size: 22, semanticLabel: 'الإشعارات'),
              ),
            ),
          ),
          if (count > 0)
            PositionedDirectional(
              top: -4,
              end: -4,
              child: Container(
                constraints: const BoxConstraints(minWidth: 20),
                height: 20,
                padding: const EdgeInsets.symmetric(horizontal: 5),
                decoration: BoxDecoration(color: p.danger, borderRadius: BorderRadius.circular(10), border: Border.all(color: p.canvas, width: 2)),
                alignment: Alignment.center,
                child: Text(count > 99 ? '99+' : '$count',
                    style: const TextStyle(color: Colors.white, fontSize: 10, fontWeight: FontWeight.w800)),
              ),
            ),
        ],
      ),
    );
  }
}
