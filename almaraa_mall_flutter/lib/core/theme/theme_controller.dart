import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// يحفظ اختيار المستخدم للوضع (فاتح / داكن / حسب النظام).
class ThemeController extends ValueNotifier<ThemeMode> {
  ThemeController._() : super(ThemeMode.system);
  static final ThemeController instance = ThemeController._();

  static const _key = 'theme_mode';

  Future<void> load() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      value = ThemeMode.values.firstWhere((m) => m.name == prefs.getString(_key), orElse: () => ThemeMode.system);
    } catch (_) {}
  }

  Future<void> set(ThemeMode mode) async {
    value = mode;
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_key, mode.name);
    } catch (_) {}
  }
}
