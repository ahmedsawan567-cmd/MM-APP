import 'dart:ui' show Color;

import '../services/server_config.dart';

class AppConstants {
  /// إصدار التطبيق (يُرسل للخادم ليظهر في ملف العميل). حدّثه مع version في pubspec.yaml.
  static const String appVersion = '1.6.0';

  // ----------------------------------------------------
  // 1. BACKEND API (ASP.NET CORE 9)
  // ----------------------------------------------------
  // يمكن تغيير العنوان عند البناء بدون تعديل الكود:
  //   flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5000/api
  // القيمة الافتراضية 10.0.2.2 تشير إلى جهاز الكمبيوتر من داخل محاكي أندرويد.
  // ويمكن تغييره من داخل التطبيق: شاشة الدخول ← "إعدادات الخادم" (انظر ServerConfig).
  static const String defaultApiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5000/api',
  );

  /// العنوان المستخدم فعلياً (المحفوظ في التطبيق أو الافتراضي).
  static String get apiBaseUrl => ServerConfig.apiBaseUrl;

  /// عنوان السيرفر بدون /api (لعرض الصور المرفوعة مثل صور الفواتير).
  static String get serverBaseUrl =>
      apiBaseUrl.endsWith('/api') ? apiBaseUrl.substring(0, apiBaseUrl.length - 4) : apiBaseUrl;

  /// يحوّل مسار صورة نسبي من السيرفر إلى رابط كامل.
  static String? resolveImageUrl(String? url) {
    if (url == null || url.isEmpty) return null;
    if (url.startsWith('http')) return url;
    return '$serverBaseUrl${url.startsWith('/') ? '' : '/'}$url';
  }

  // ألوان الهوية في core/theme/app_theme.dart (Brand و AppPalette).

  // ----------------------------------------------------
  // 3. MALL INFORMATION
  // ----------------------------------------------------
  static const String appNameAr = 'المرأة مول';
  /// لون أيقونة الإشعار (بنفسجي الشعار)
  static const Color notificationColor = Color(0xFFB607BC);
  static const String appNameEn = 'Almaraa Mall';
  static const String taglineAr = 'بوابتك للتسوق الفاخر وبرامج الولاء الحصرية';
  static const String currency = 'د.ل'; // Libyan Dinar
}
