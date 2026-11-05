import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:workmanager/workmanager.dart';

import '../../models/app_notification.dart';
import '../constants/app_constants.dart';
import 'server_config.dart';

/// مهمة الخلفية: تُشغَّل من نظام أندرويد كل ~15 دقيقة حتى لو كان التطبيق مغلقاً.
@pragma('vm:entry-point')
void notificationsCallbackDispatcher() {
  Workmanager().executeTask((task, inputData) async {
    try {
      await ServerConfig.load();
      // الإشعارات الفورية (Firebase) مفعّلة: النظام يعرضها مباشرة ولا حاجة للفحص الدوري
      if ((await SharedPreferences.getInstance()).getBool('push_active') ?? false) return true;
      await AppNotifications.init();
      await AppNotifications.checkAndNotify();
    } catch (_) {
      // لا نُفشل المهمة: المحاولة التالية بعد المدة الدورية
    }
    return true;
  });
}

/// إشعارات الهاتف: الإذن، العرض، والفحص الدوري في الخلفية لإشعارات المنظومة الجديدة.
class AppNotifications {
  AppNotifications._();

  static const _tokenKey = 'auth_token';
  static const _lastShownKey = 'notif_last_shown';
  static const _askedKey = 'notif_permission_asked';
  static const _taskName = 'mm-notifications-check';

  static final _plugin = FlutterLocalNotificationsPlugin();
  static bool _ready = false;

  /// يُضبط عند الضغط على إشعار: يفتح التطبيق صفحة الإشعارات.
  static final ValueNotifier<bool> openInbox = ValueNotifier(false);

  /// يزيد مع كل إشعار فوري يصل والتطبيق مفتوح (لتحديث العدّاد).
  static final ValueNotifier<int> onRemoteMessage = ValueNotifier(0);

  static bool _checking = false;

  static const _channel = AndroidNotificationDetails(
    'mm_updates',
    'تحديثات المرأة مول',
    channelDescription: 'نتائج فواتيرك، النقاط، والعروض من المول',
    importance: Importance.high,
    priority: Priority.high,
    icon: 'ic_stat_notify',
    color: AppConstants.notificationColor,
  );

  static Future<void> init() async {
    if (_ready || kIsWeb) return;
    await _plugin.initialize(
      settings: const InitializationSettings(android: AndroidInitializationSettings('ic_stat_notify')),
      onDidReceiveNotificationResponse: (_) => openInbox.value = true,
    );
    final launch = await _plugin.getNotificationAppLaunchDetails();
    if (launch?.didNotificationLaunchApp ?? false) openInbox.value = true;
    _ready = true;
  }

  static AndroidFlutterLocalNotificationsPlugin? get _android =>
      _plugin.resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>();

  /// هل سبق أن سألنا المستخدم عن الإذن؟ (حتى لا نزعجه في كل مرة)
  static Future<bool> wasAsked() async => (await SharedPreferences.getInstance()).getBool(_askedKey) ?? false;

  static Future<void> markAsked() async => (await SharedPreferences.getInstance()).setBool(_askedKey, true);

  static Future<bool> isEnabled() async {
    if (kIsWeb) return false;
    return await _android?.areNotificationsEnabled() ?? false;
  }

  /// يطلب إذن الإشعارات (أندرويد 13 فأحدث يعرض نافذة النظام).
  static Future<bool> requestPermission() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_askedKey, true);
    if (kIsWeb) return false;
    return await _android?.requestNotificationsPermission() ?? false;
  }

  /// بعد الدخول: تفعيل الفحص الدوري في الخلفية، وتجاهل الإشعارات القديمة السابقة للدخول.
  static Future<void> startBackground() async {
    if (kIsWeb) return;
    final prefs = await SharedPreferences.getInstance();
    if (prefs.getString(_lastShownKey) == null) {
      await prefs.setString(_lastShownKey, DateTime.now().toUtc().toIso8601String());
    }
    try {
      await Workmanager().initialize(notificationsCallbackDispatcher);
      await Workmanager().registerPeriodicTask(
        _taskName,
        _taskName,
        frequency: const Duration(minutes: 15),
        constraints: Constraints(networkType: NetworkType.connected),
        existingWorkPolicy: ExistingPeriodicWorkPolicy.keep,
      );
    } catch (_) {
      // أجهزة لا تدعم المهام الخلفية: تبقى الإشعارات داخل التطبيق
    }
  }

  static Future<void> stopBackground() async {
    if (kIsWeb) return;
    try {
      await Workmanager().cancelByUniqueName(_taskName);
    } catch (_) {}
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_lastShownKey);
    await _plugin.cancelAll();
  }

  /// يجلب الإشعارات الجديدة غير المقروءة منذ آخر عرض ويُظهرها على الهاتف.
  static Future<int> checkAndNotify() async {
    if (_checking) return 0;
    _checking = true;
    try {
      return await _checkAndNotify();
    } finally {
      _checking = false;
    }
  }

  static Future<int> _checkAndNotify() async {
    final prefs = await SharedPreferences.getInstance();
    // قد تكون مهمة الخلفية حدّثت آخر وقت عرض
    await prefs.reload();
    final token = prefs.getString(_tokenKey);
    if (token == null) return 0;
    final since = prefs.getString(_lastShownKey);

    final uri = Uri.parse('${AppConstants.apiBaseUrl}/Notifications')
        .replace(queryParameters: {if (since != null) 'since': since, 'take': '20'});
    final res = await http.get(uri, headers: {'Authorization': 'Bearer $token', 'Accept': 'application/json'})
        .timeout(const Duration(seconds: 20));
    if (res.statusCode != 200) return 0;

    final body = jsonDecode(utf8.decode(res.bodyBytes)) as Map<String, dynamic>;
    final items = (body['items'] as List? ?? [])
        .whereType<Map<String, dynamic>>()
        .map(AppNotification.fromJson)
        .where((n) => !n.isRead)
        .toList()
      ..sort((a, b) => a.createdAt.compareTo(b.createdAt));
    if (items.isEmpty) return 0;

    await show(items);
    await prefs.setString(_lastShownKey, items.last.createdAt.toUtc().toIso8601String());
    return items.length;
  }

  static Future<void> show(List<AppNotification> items) async {
    if (kIsWeb || !(await isEnabled())) return;
    for (final n in items.take(5)) {
      await showOne(id: n.id, title: n.title, body: n.body, link: n.link, checkPermission: false);
    }
  }

  /// عرض إشعار واحد. الوسم (tag) = معرّف الإشعار في المنظومة، وهو نفس وسم إشعار Firebase،
  /// فلا يتكرر الإشعار نفسه مرتين على الهاتف.
  static Future<void> showOne({
    required String id,
    required String title,
    required String body,
    String? link,
    bool checkPermission = true,
  }) async {
    if (kIsWeb) return;
    if (checkPermission && !(await isEnabled())) return;
    await _plugin.show(
      id: 0,
      title: title,
      body: body,
      notificationDetails: NotificationDetails(android: _channel.copyWith(body: body, tag: id)),
      payload: link,
    );
  }
}

extension on AndroidNotificationDetails {
  /// نص طويل يظهر كاملاً عند سحب الإشعار
  AndroidNotificationDetails copyWith({required String body, String? tag}) => AndroidNotificationDetails(
        channelId,
        channelName,
        channelDescription: channelDescription,
        importance: importance,
        priority: priority,
        icon: icon,
        color: color,
        styleInformation: BigTextStyleInformation(body),
        tag: tag,
      );
}
