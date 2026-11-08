import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// ألوان النظام التي تتبدّل بين الوضعين الفاتح والداكن.
/// الاستخدام: `context.palette.muted`
@immutable
class AppPalette extends ThemeExtension<AppPalette> {
  final Color canvas;
  final Color surface;
  final Color surfaceAlt;
  final Color border;
  final Color ink;
  final Color muted;
  final Color brand;
  final Color brandStrong;
  final Color brandSoft;
  final Color gold;
  final Color goldSoft;
  final Color success;
  final Color successSoft;
  final Color warning;
  final Color warningSoft;
  final Color danger;
  final Color dangerSoft;
  final Color info;
  final Color infoSoft;

  const AppPalette({
    required this.canvas,
    required this.surface,
    required this.surfaceAlt,
    required this.border,
    required this.ink,
    required this.muted,
    required this.brand,
    required this.brandStrong,
    required this.brandSoft,
    required this.gold,
    required this.goldSoft,
    required this.success,
    required this.successSoft,
    required this.warning,
    required this.warningSoft,
    required this.danger,
    required this.dangerSoft,
    required this.info,
    required this.infoSoft,
  });

  static const light = AppPalette(
    canvas: Color(0xFFF6F7F9),
    surface: Color(0xFFFFFFFF),
    surfaceAlt: Color(0xFFF1F5F9),
    border: Color(0xFFE2E8F0),
    ink: Color(0xFF0F172A),
    muted: Color(0xFF64748B),
    brand: Color(0xFFB607BC),
    brandStrong: Color(0xFF750880),
    brandSoft: Color(0xFFFCF3FD),
    gold: Color(0xFFC4008F),
    goldSoft: Color(0xFFFFF0FA),
    success: Color(0xFF047857),
    successSoft: Color(0xFFECFDF5),
    warning: Color(0xFFB45309),
    warningSoft: Color(0xFFFFFBEB),
    danger: Color(0xFFBE123C),
    dangerSoft: Color(0xFFFFF1F2),
    info: Color(0xFF4338CA),
    infoSoft: Color(0xFFEEF2FF),
  );

  static const dark = AppPalette(
    canvas: Color(0xFF0A0F1A),
    surface: Color(0xFF111827),
    surfaceAlt: Color(0xFF1C273C),
    border: Color(0xFF243047),
    ink: Color(0xFFF1F5F9),
    muted: Color(0xFF94A3B8),
    brand: Color(0xFFE080E8),
    brandStrong: Color(0xFF750880),
    brandSoft: Color(0xFF2A1430),
    gold: Color(0xFFFF6FD6),
    goldSoft: Color(0xFF331328),
    success: Color(0xFF34D399),
    successSoft: Color(0xFF112B32),
    warning: Color(0xFFFBBF24),
    warningSoft: Color(0xFF2C2824),
    danger: Color(0xFFFB7185),
    dangerSoft: Color(0xFF2C1D2E),
    info: Color(0xFFA5B4FC),
    infoSoft: Color(0xFF1B213F),
  );

  @override
  AppPalette copyWith() => this;

  @override
  AppPalette lerp(ThemeExtension<AppPalette>? other, double t) {
    if (other is! AppPalette) return this;
    Color l(Color a, Color b) => Color.lerp(a, b, t)!;
    return AppPalette(
      canvas: l(canvas, other.canvas),
      surface: l(surface, other.surface),
      surfaceAlt: l(surfaceAlt, other.surfaceAlt),
      border: l(border, other.border),
      ink: l(ink, other.ink),
      muted: l(muted, other.muted),
      brand: l(brand, other.brand),
      brandStrong: l(brandStrong, other.brandStrong),
      brandSoft: l(brandSoft, other.brandSoft),
      gold: l(gold, other.gold),
      goldSoft: l(goldSoft, other.goldSoft),
      success: l(success, other.success),
      successSoft: l(successSoft, other.successSoft),
      warning: l(warning, other.warning),
      warningSoft: l(warningSoft, other.warningSoft),
      danger: l(danger, other.danger),
      dangerSoft: l(dangerSoft, other.dangerSoft),
      info: l(info, other.info),
      infoSoft: l(infoSoft, other.infoSoft),
    );
  }
}

extension AppPaletteX on BuildContext {
  AppPalette get palette => Theme.of(this).extension<AppPalette>()!;
  bool get isDark => Theme.of(this).brightness == Brightness.dark;
}

/// زوايا ومسافات موحّدة.
class AppRadius {
  static const sm = 10.0;
  static const md = 14.0;
  static const lg = 20.0;
  static const xl = 28.0;
}

/// هوية المرأة مول (من الشعار): بنفسجي + وردي فوشي مع كحلي داكن.
/// تُستخدم للأسطح المميزة (البطاقة الرئيسية، شاشة البداية، الشارات) بنفس اللون في الوضعين.
class Brand {
  static const purple = Color(0xFFB607BC);
  static const magenta = Color(0xFFFC03BA);
  static const plum = Color(0xFF750880);
  static const night = Color(0xFF3A0242);
  static const navy = Color(0xFF06022A);

  /// لون إبراز فوق الخلفيات المتدرجة الداكنة (بدل الذهبي سابقاً).
  static const glow = Color(0xFFFFC2EC);

  /// تدرّج الهوية: من البرقوقي الداكن إلى البنفسجي ثم الفوشي.
  static const gradient = LinearGradient(
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
    colors: [night, plum, purple],
  );

  static const vividGradient = LinearGradient(
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
    colors: [plum, purple, Color(0xFFE0089F)],
  );

  static const mark = 'assets/brand/mark.png';
  static const markLight = 'assets/brand/mark_light.png';
  static const logo = 'assets/brand/logo.png';
}

class AppTheme {
  /// خط القاهرة (Cairo) مضمّن في التطبيق (assets/fonts) فيعمل بدون إنترنت.
  static const fontFamily = 'Cairo';

  static ThemeData get lightTheme => _build(Brightness.light, AppPalette.light);
  static ThemeData get darkTheme => _build(Brightness.dark, AppPalette.dark);

  static ThemeData _build(Brightness brightness, AppPalette p) {
    final isDark = brightness == Brightness.dark;
    final base = ThemeData(useMaterial3: true, brightness: brightness, fontFamily: fontFamily);
    final text = base.textTheme.apply(fontFamily: fontFamily, bodyColor: p.ink, displayColor: p.ink);

    final scheme = ColorScheme(
      brightness: brightness,
      primary: isDark ? p.brand : p.brand,
      onPrimary: isDark ? const Color(0xFF2E0533) : Colors.white,
      primaryContainer: p.brandSoft,
      onPrimaryContainer: p.brand,
      secondary: p.gold,
      onSecondary: isDark ? const Color(0xFF3A0029) : Colors.white,
      secondaryContainer: p.goldSoft,
      onSecondaryContainer: p.gold,
      error: p.danger,
      onError: Colors.white,
      surface: p.surface,
      onSurface: p.ink,
      onSurfaceVariant: p.muted,
      surfaceContainerHighest: p.surfaceAlt,
      outline: p.border,
      outlineVariant: p.border,
    );

    OutlineInputBorder border(Color c, [double w = 1]) =>
        OutlineInputBorder(borderRadius: BorderRadius.circular(AppRadius.md), borderSide: BorderSide(color: c, width: w));

    return base.copyWith(
      colorScheme: scheme,
      scaffoldBackgroundColor: p.canvas,
      extensions: [p],
      textTheme: text.copyWith(
        headlineSmall: text.headlineSmall?.copyWith(fontWeight: FontWeight.w700, letterSpacing: -0.2),
        titleLarge: text.titleLarge?.copyWith(fontWeight: FontWeight.w700, fontSize: 20),
        titleMedium: text.titleMedium?.copyWith(fontWeight: FontWeight.w700, fontSize: 16),
        titleSmall: text.titleSmall?.copyWith(fontWeight: FontWeight.w700),
        bodyLarge: text.bodyLarge?.copyWith(fontSize: 15, height: 1.6),
        bodyMedium: text.bodyMedium?.copyWith(fontSize: 14, height: 1.55),
        bodySmall: text.bodySmall?.copyWith(fontSize: 12.5, height: 1.5, color: p.muted),
        labelLarge: text.labelLarge?.copyWith(fontWeight: FontWeight.w700),
      ),
      dividerTheme: DividerThemeData(color: p.border, thickness: 1, space: 1),
      appBarTheme: AppBarTheme(
        backgroundColor: p.canvas,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        foregroundColor: p.ink,
        systemOverlayStyle: isDark ? SystemUiOverlayStyle.light : SystemUiOverlayStyle.dark,
        titleTextStyle: TextStyle(fontFamily: fontFamily, fontSize: 20, fontWeight: FontWeight.w700, color: p.ink),
      ),
      cardTheme: CardThemeData(
        color: p.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadius.lg),
          side: BorderSide(color: p.border),
        ),
      ),
      listTileTheme: ListTileThemeData(iconColor: p.muted, textColor: p.ink),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: isDark ? p.brand : p.brand,
          foregroundColor: scheme.onPrimary,
          elevation: 0,
          minimumSize: const Size(0, 52),
          padding: const EdgeInsets.symmetric(horizontal: 22),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
          textStyle: TextStyle(fontFamily: fontFamily, fontSize: 15, fontWeight: FontWeight.w700),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: p.ink,
          minimumSize: const Size(0, 52),
          side: BorderSide(color: p.border),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
          textStyle: TextStyle(fontFamily: fontFamily, fontSize: 15, fontWeight: FontWeight.w700),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: p.brand, textStyle: TextStyle(fontFamily: fontFamily, fontWeight: FontWeight.w700)),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: p.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        hintStyle: TextStyle(color: p.muted),
        labelStyle: TextStyle(color: p.muted),
        prefixIconColor: p.muted,
        suffixIconColor: p.muted,
        border: border(p.border),
        enabledBorder: border(p.border),
        focusedBorder: border(p.brand, 1.6),
        errorBorder: border(p.danger),
        focusedErrorBorder: border(p.danger, 1.6),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: p.surface,
        selectedColor: p.brand,
        side: BorderSide(color: p.border),
        labelStyle: TextStyle(fontFamily: fontFamily, fontWeight: FontWeight.w700, color: p.ink),
        secondaryLabelStyle: TextStyle(fontFamily: fontFamily, fontWeight: FontWeight.w700, color: scheme.onPrimary),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
        showCheckmark: false,
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: p.surface,
        surfaceTintColor: Colors.transparent,
        indicatorColor: p.brandSoft,
        height: 70,
        elevation: 0,
        labelTextStyle: WidgetStateProperty.resolveWith((s) => TextStyle(fontFamily: fontFamily, 
              fontSize: 12,
              fontWeight: s.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w600,
              color: s.contains(WidgetState.selected) ? p.brand : p.muted,
            )),
        iconTheme: WidgetStateProperty.resolveWith(
            (s) => IconThemeData(size: 22, color: s.contains(WidgetState.selected) ? p.brand : p.muted)),
      ),
      tabBarTheme: TabBarThemeData(
        labelColor: p.brand,
        unselectedLabelColor: p.muted,
        indicatorColor: p.brand,
        dividerColor: p.border,
        labelStyle: TextStyle(fontFamily: fontFamily, fontWeight: FontWeight.w700),
        unselectedLabelStyle: TextStyle(fontFamily: fontFamily, fontWeight: FontWeight.w600),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
        contentTextStyle: TextStyle(fontFamily: fontFamily, fontWeight: FontWeight.w600),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: p.surface,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadius.xl))),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: p.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.xl)),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(color: p.brand),
      pageTransitionsTheme: const PageTransitionsTheme(builders: {
        TargetPlatform.android: FadeForwardsPageTransitionsBuilder(),
      }),
    );
  }
}
