import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../models/user_profile.dart';
import 'api_client.dart';
import 'loyalty_api.dart';

/// يدير جلسة العميل: تسجيل الدخول، إنشاء الحساب، الاستعادة عند فتح التطبيق، وتسجيل الخروج.
class Session extends ChangeNotifier {
  Session._();
  static final Session instance = Session._();

  static const _tokenKey = 'auth_token';
  static const _expiryKey = 'auth_expires_at';

  UserProfile? _user;
  bool _restoring = true;

  UserProfile? get user => _user;
  bool get isLoggedIn => _user != null;
  bool get isRestoring => _restoring;

  /// يزداد بعد أي عملية تغيّر البيانات (رفع فاتورة، استبدال مكافأة...) لتحدّث الشاشات نفسها.
  final ValueNotifier<int> dataVersion = ValueNotifier<int>(0);

  void notifyDataChanged() => dataVersion.value++;

  Future<void> restore() async {
    ApiClient.instance.onUnauthorized = () {
      if (_user != null) logout();
    };

    try {
      final prefs = await SharedPreferences.getInstance();
      final token = prefs.getString(_tokenKey);
      final expiry = DateTime.tryParse(prefs.getString(_expiryKey) ?? '');

      if (token != null && (expiry == null || expiry.isAfter(DateTime.now()))) {
        ApiClient.instance.setToken(token);
        _user = await LoyaltyApi.getProfile();
      }
    } on ApiException catch (e) {
      // عند انقطاع الإنترنت نُبقي التوكن، وعند رفضه نحذفه.
      if (e.isUnauthorized) await _clear();
    } catch (_) {
      await _clear();
    } finally {
      _restoring = false;
      notifyListeners();
    }
  }

  Future<void> login(String phone, String password) async {
    final auth = await LoyaltyApi.login(phone, password);
    await _start(auth);
  }

  Future<void> register(String fullName, String phone, String password) async {
    final auth = await LoyaltyApi.register(fullName, phone, password);
    await _start(auth);
  }

  Future<void> refreshProfile() async {
    if (!isLoggedIn) return;
    _user = await LoyaltyApi.getProfile();
    notifyListeners();
  }

  Future<void> logout() async {
    await _clear();
    _user = null;
    notifyListeners();
  }

  Future<void> _start(AuthResult auth) async {
    if (auth.role != 'CUSTOMER') {
      throw ApiException('هذا التطبيق مخصص للمتسوقين. يرجى استخدام لوحة التحكم لحسابات طاقم العمل.');
    }
    ApiClient.instance.setToken(auth.token);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, auth.token);
    await prefs.setString(_expiryKey, auth.expiresAt.toIso8601String());
    _user = await LoyaltyApi.getProfile();
    notifyListeners();
  }

  Future<void> _clear() async {
    ApiClient.instance.setToken(null);
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_tokenKey);
    await prefs.remove(_expiryKey);
  }
}
