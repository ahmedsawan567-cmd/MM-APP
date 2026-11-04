import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import '../constants/app_constants.dart';

/// عنوان خادم المول. يُضبط من داخل التطبيق (شاشة الدخول ← إعدادات الخادم)
/// حتى تعمل نسخة APK واحدة على أي شبكة بدون إعادة بناء.
class ServerConfig {
  ServerConfig._();

  static const _key = 'server_api_base_url';
  static String? _override;

  /// العنوان الفعلي المستخدم الآن (مثال: http://192.168.1.10:5000/api).
  static String get apiBaseUrl => _override ?? AppConstants.defaultApiBaseUrl;

  static bool get isCustom => _override != null;

  static Future<void> load() async {
    try {
      final saved = (await SharedPreferences.getInstance()).getString(_key);
      if (saved != null && saved.isNotEmpty) _override = saved;
    } catch (_) {}
  }

  static Future<void> save(String apiBaseUrl) async {
    _override = apiBaseUrl;
    try {
      await (await SharedPreferences.getInstance()).setString(_key, apiBaseUrl);
    } catch (_) {}
  }

  static Future<void> reset() async {
    _override = null;
    try {
      await (await SharedPreferences.getInstance()).remove(_key);
    } catch (_) {}
  }

  /// يحوّل ما يكتبه المستخدم إلى عنوان API كامل:
  ///   192.168.1.10            → http://192.168.1.10:5000/api
  ///   192.168.1.10:8080       → http://192.168.1.10:8080/api
  ///   https://mall.example.ly → https://mall.example.ly/api
  /// يرجع null إذا كان المدخل غير صالح.
  static String? normalize(String input) {
    var text = input.trim();
    if (text.isEmpty) return null;
    if (!text.contains('://')) text = 'http://$text';
    final uri = Uri.tryParse(text);
    if (uri == null || uri.host.isEmpty || !(uri.scheme == 'http' || uri.scheme == 'https')) return null;
    final port = uri.hasPort ? uri.port : (uri.scheme == 'http' && _looksLocal(uri.host) ? 5000 : null);
    var path = uri.path.replaceAll(RegExp(r'/+$'), '');
    if (!path.endsWith('/api')) path = '$path/api';
    return Uri(scheme: uri.scheme, host: uri.host, port: port, path: path).toString();
  }

  static bool _looksLocal(String host) =>
      RegExp(r'^\d{1,3}(\.\d{1,3}){3}$').hasMatch(host) || host == 'localhost';

  /// يتحقق أن العنوان يشير فعلاً لخادم المول.
  static Future<bool> test(String apiBaseUrl) async {
    try {
      final res = await http.get(Uri.parse('$apiBaseUrl/health')).timeout(const Duration(seconds: 6));
      if (res.statusCode != 200) return false;
      final body = jsonDecode(res.body);
      return body is Map && body['status'] == 'ok';
    } catch (_) {
      return false;
    }
  }
}
