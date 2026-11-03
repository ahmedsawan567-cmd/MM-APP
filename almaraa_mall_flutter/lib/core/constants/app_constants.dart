import 'package:flutter/material.dart';

class AppConstants {
  // ----------------------------------------------------
  // 1. SUPABASE CLOUD CONFIGURATION
  // ----------------------------------------------------
  static const String supabaseUrl = 'https://exswegtnsxvtdwkbhdqw.supabase.co';
  static const String supabaseAnonKey =
      'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImV4c3dlZ3Ruc3h2dGR3a2JoZHF3Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODk3NjQ5MDcsImV4cCI6MjEwNTM0MDkwN30.-19YvZX5PKCTpIGPy6AyfKEG19CPgltF9iexBdpzqtg';

  // ----------------------------------------------------
  // 2. BACKEND API (ASP.NET CORE 9)
  // ----------------------------------------------------
  static const String apiBaseUrl = 'http://localhost:5000/api';

  // ----------------------------------------------------
  // 3. ENTERPRISE LUXURY PALETTE (المرأة مول)
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
  // 4. MALL INFORMATION
  // ----------------------------------------------------
  static const String appNameAr = 'المرأة مول';
  static const String appNameEn = 'Almaraa Mall';
  static const String taglineAr = 'بوابتك للتسوق الفاخر وبرامج الولاء الحصرية';
  static const String currency = 'د.ل'; // Libyan Dinar
}
