import 'package:flutter/material.dart';

class AppConstants {
  // ----------------------------------------------------
  // 1. BACKEND API (ASP.NET CORE 9)
  // ----------------------------------------------------
  // يمكن تغيير العنوان عند البناء بدون تعديل الكود:
  //   flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5000/api
  // القيمة الافتراضية 10.0.2.2 تشير إلى جهاز الكمبيوتر من داخل محاكي أندرويد.
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5000/api',
  );

  /// عنوان السيرفر بدون /api (لعرض الصور المرفوعة مثل صور الفواتير).
  static String get serverBaseUrl =>
      apiBaseUrl.endsWith('/api') ? apiBaseUrl.substring(0, apiBaseUrl.length - 4) : apiBaseUrl;

  /// يحوّل مسار صورة نسبي من السيرفر إلى رابط كامل.
  static String? resolveImageUrl(String? url) {
    if (url == null || url.isEmpty) return null;
    if (url.startsWith('http')) return url;
    return '$serverBaseUrl${url.startsWith('/') ? '' : '/'}$url';
  }

  // ----------------------------------------------------
  // 2. ENTERPRISE LUXURY PALETTE (المرأة مول)
  // ----------------------------------------------------
  static const Color primaryGreen = Color(0xFF064E3B);     // زمردي ملكي داكن
  static const Color primaryGreenLight = Color(0xFF047857);
  static const Color accentGold = Color(0xFFD97706);       // ذهبي فاخر
  static const Color accentGoldLight = Color(0xFFFBBF24);
  static const Color bgDark = Color(0xFF0F172A);           // كحلي داكن فاخر
  static const Color bgLight = Color(0xFFF8FAFC);          // أبيض عاجي ناعم
  static const Color surfaceCard = Color(0xFFFFFFFF);
  static const Color textPrimary = Color(0xFF0F172A);
  static const Color textMuted = Color(0xFF64748B);
  static const Color successColor = Color(0xFF10B981);
  static const Color warningColor = Color(0xFFF59E0B);
  static const Color dangerColor = Color(0xFFEF4444);

  // ----------------------------------------------------
  // 3. MALL INFORMATION
  // ----------------------------------------------------
  static const String appNameAr = 'المرأة مول';
  static const String appNameEn = 'Almaraa Mall';
  static const String taglineAr = 'بوابتك للتسوق الفاخر وبرامج الولاء الحصرية';
  static const String currency = 'د.ل'; // Libyan Dinar
}
