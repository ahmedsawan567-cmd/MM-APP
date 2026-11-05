import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'loyalty_api.dart';
import 'notifications_service.dart';

/// إشعارات فورية عبر Firebase Cloud Messaging.
///
/// إعدادات مشروع Firebase تُمرَّر وقت البناء (من ملف google-services.json عبر سكربت البناء):
///   --dart-define=FIREBASE_API_KEY=... --dart-define=FIREBASE_APP_ID=...
///   --dart-define=FIREBASE_SENDER_ID=... --dart-define=FIREBASE_PROJECT_ID=...
/// بدونها يعمل التطبيق كما هو: يفحص الإشعارات الجديدة دورياً ويعرضها على الهاتف.
class PushService {
  PushService._();

  static const _apiKey = String.fromEnvironment('FIREBASE_API_KEY');
  static const _appId = String.fromEnvironment('FIREBASE_APP_ID');
  static const _senderId = String.fromEnvironment('FIREBASE_SENDER_ID');
  static const _projectId = String.fromEnvironment('FIREBASE_PROJECT_ID');
  static const _storageBucket = String.fromEnvironment('FIREBASE_STORAGE_BUCKET');

  /// مفتاح مشترك مع مهمة الخلفية: عند تفعيل الإشعارات الفورية لا داعي للفحص الدوري.
  static const activeKey = 'push_active';
  static const _tokenKey = 'push_token';

  static bool get configured =>
      !kIsWeb && _apiKey.isNotEmpty && _appId.isNotEmpty && _senderId.isNotEmpty && _projectId.isNotEmpty;

  static bool _initialized = false;

  /// هل الجهاز مسجّل لدى الخادم لاستقبال الإشعارات الفورية؟
  static bool active = false;

  static Future<void> init() async {
    if (!configured || _initialized) return;
    try {
      await Firebase.initializeApp(
        options: const FirebaseOptions(
          apiKey: _apiKey,
          appId: _appId,
          messagingSenderId: _senderId,
          projectId: _projectId,
          storageBucket: _storageBucket == '' ? null : _storageBucket,
        ),
      );
      _initialized = true;

      // التطبيق مفتوح: النظام لا يعرض الإشعار تلقائياً، فنعرضه نحن
      FirebaseMessaging.onMessage.listen((m) {
        final n = m.notification;
        if (n == null) return;
        AppNotifications.showOne(
          id: m.data['id'] ?? m.messageId ?? '${DateTime.now().millisecondsSinceEpoch}',
          title: n.title ?? '',
          body: n.body ?? '',
          link: m.data['link'],
        );
        AppNotifications.onRemoteMessage.value++;
      });
      // الضغط على إشعار والتطبيق في الخلفية أو مغلق
      FirebaseMessaging.onMessageOpenedApp.listen((_) => AppNotifications.openInbox.value = true);
      final initial = await FirebaseMessaging.instance.getInitialMessage();
      if (initial != null) AppNotifications.openInbox.value = true;
      // رمز التسجيل الكلاسيكي هو ما تقبله واجهة FCM HTTP v1 في الخادم
      // ignore: deprecated_member_use
      FirebaseMessaging.instance.onTokenRefresh.listen((t) => _sendToken(t));
    } catch (e) {
      debugPrint('Firebase غير متاح: $e');
      _initialized = false;
    }
  }

  /// بعد الدخول: نسجّل رمز الجهاز لدى الخادم.
  static Future<void> register() async {
    if (!_initialized) return;
    try {
      // ignore: deprecated_member_use
      final token = await FirebaseMessaging.instance.getToken();
      if (token == null) return;
      await _sendToken(token);
    } catch (e) {
      debugPrint('تعذّر تسجيل الجهاز للإشعارات الفورية: $e');
    }
  }

  static Future<void> _sendToken(String token) async {
    await LoyaltyApi.registerDeviceToken(token);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, token);
    await prefs.setBool(activeKey, true);
    active = true;
  }

  /// عند الخروج: يتوقف وصول إشعارات هذا الحساب لهذا الجهاز.
  static Future<void> unregister() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString(_tokenKey);
    active = false;
    await prefs.remove(activeKey);
    await prefs.remove(_tokenKey);
    if (token == null) return;
    try {
      await LoyaltyApi.unregisterDeviceToken(token);
    } catch (_) {}
  }
}
