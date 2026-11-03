import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import '../constants/app_constants.dart';

class AppTheme {
  static ThemeData get lightTheme {
    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.light,
      primaryColor: AppConstants.primaryGreen,
      scaffoldBackgroundColor: AppConstants.bgLight,
      colorScheme: const ColorScheme.light(
        primary: AppConstants.primaryGreen,
        secondary: AppConstants.accentGold,
        surface: AppConstants.surfaceCard,
        error: AppConstants.dangerColor,
      ),
      fontFamily: GoogleFonts.tajawal().fontFamily,
      textTheme: TextTheme(
        displayLarge: GoogleFonts.tajawal(
          fontSize: 28,
          fontWeight: FontWeight.bold,
          color: AppConstants.textPrimary,
        ),
        titleLarge: GoogleFonts.tajawal(
          fontSize: 20,
          fontWeight: FontWeight.bold,
          color: AppConstants.textPrimary,
        ),
        titleMedium: GoogleFonts.tajawal(
          fontSize: 16,
          fontWeight: FontWeight.w600,
          color: AppConstants.textPrimary,
        ),
        bodyLarge: GoogleFonts.tajawal(
          fontSize: 15,
          color: AppConstants.textPrimary,
        ),
        bodyMedium: GoogleFonts.tajawal(
          fontSize: 13,
          color: AppConstants.textMuted,
        ),
      ),
      appBarTheme: AppBarTheme(
        backgroundColor: Colors.white,
        elevation: 0.5,
        centerTitle: true,
        iconTheme: const IconThemeData(color: AppConstants.textPrimary),
        titleTextStyle: GoogleFonts.tajawal(
          fontSize: 18,
          fontWeight: FontWeight.bold,
          color: AppConstants.textPrimary,
        ),
      ),
      cardTheme: CardThemeData(
        color: Colors.white,
        elevation: 2,
        shadowColor: Colors.black.withOpacity(0.04),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(color: Colors.grey.withOpacity(0.12), width: 1),
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppConstants.primaryGreen,
          foregroundColor: Colors.white,
          elevation: 2,
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
          textStyle: GoogleFonts.tajawal(
            fontSize: 15,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),
      tabBarTheme: TabBarThemeData(
        dividerColor: Colors.grey.withOpacity(0.15),
      ),
      dividerTheme: DividerThemeData(
        color: Colors.grey.withOpacity(0.15),
        thickness: 1,
        space: 1,
      ),
      bottomNavigationBarTheme: const BottomNavigationBarThemeData(
        backgroundColor: Colors.white,
        selectedItemColor: AppConstants.primaryGreen,
        unselectedItemColor: AppConstants.textMuted,
        showUnselectedLabels: true,
        type: BottomNavigationBarType.fixed,
        elevation: 10,
      ),
    );
  }
}
