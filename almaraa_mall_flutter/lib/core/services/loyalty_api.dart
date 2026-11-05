import 'dart:io';

import '../../models/app_notification.dart';
import '../../models/invoice.dart';
import '../../models/json_utils.dart';
import '../../models/mall.dart';
import '../../models/reward.dart';
import '../../models/user_profile.dart';
import '../../models/wallet.dart';
import 'api_client.dart';

/// كل استدعاءات الـ API الخاصة بتطبيق المتسوق في مكان واحد.
class LoyaltyApi {
  static final _api = ApiClient.instance;

  // ---------- الحساب ----------
  static Future<AuthResult> login(String phone, String password) async =>
      AuthResult.fromJson(await _api.post('/Auth/login', {'phoneNumber': phone, 'password': password}));

  static Future<AuthResult> register(String fullName, String phone, String password) async =>
      AuthResult.fromJson(await _api.post('/Auth/register', {
        'fullName': fullName,
        'phoneNumber': phone,
        'password': password,
      }));

  static Future<UserProfile> getProfile() async => UserProfile.fromJson(await _api.get('/Auth/me'));

  static Future<UserProfile> updateProfile({
    required String fullName,
    String? email,
    DateTime? birthDate,
    String? gender,
  }) async {
    final res = await _api.put('/Auth/me', {
      'fullName': fullName,
      'email': email,
      'birthDate': birthDate == null
          ? null
          : '${birthDate.year.toString().padLeft(4, '0')}-${birthDate.month.toString().padLeft(2, '0')}-${birthDate.day.toString().padLeft(2, '0')}',
      'gender': gender,
    });
    return UserProfile.fromJson((res as Map<String, dynamic>)['profile'] as Map<String, dynamic>);
  }

  static Future<void> changePassword(String currentPassword, String newPassword) async =>
      _api.post('/Auth/change-password', {'currentPassword': currentPassword, 'newPassword': newPassword});

  static Future<void> deleteAccount(String password) async => _api.post('/Auth/delete-account', {'password': password});

  // ---------- الإشعارات ----------
  static Future<(List<AppNotification>, int)> getNotifications() async {
    final res = await _api.get('/Notifications', query: {'take': '50'}) as Map<String, dynamic>;
    return (jList(res['items'], AppNotification.fromJson), jInt(res['unread']));
  }

  static Future<int> getUnreadNotifications() async =>
      jInt((await _api.get('/Notifications/unread-count') as Map<String, dynamic>)['unread']);

  static Future<void> markNotificationRead(String id) async => _api.post('/Notifications/$id/read');

  static Future<void> markAllNotificationsRead() async => _api.post('/Notifications/read-all');

  // ---------- الرئيسية والمتاجر ----------
  static Future<HomeData> getHome() async => HomeData.fromJson(await _api.get('/v1/mobile/home'));

  static Future<List<Store>> getStores({String? search, String? category}) async =>
      jList(await _api.get('/v1/mobile/stores', query: {'search': search, 'category': category}), Store.fromJson);

  // ---------- المحفظة ----------
  static Future<WalletSummary> getWalletSummary() async =>
      WalletSummary.fromJson(await _api.get('/Wallet/summary'));

  static Future<List<PointTransaction>> getTransactions() async =>
      jList(await _api.get('/Wallet/transactions'), PointTransaction.fromJson);

  static Future<List<Voucher>> getVouchers() async => jList(await _api.get('/Wallet/my-vouchers'), Voucher.fromJson);

  // ---------- المكافآت ----------
  static Future<List<Reward>> getRewards() async => jList(await _api.get('/Rewards'), Reward.fromJson);

  static Future<RedemptionResult> redeemReward(String rewardId) async =>
      RedemptionResult.fromJson(await _api.post('/Rewards/$rewardId/redeem'));

  // ---------- الفواتير ----------
  static Future<List<Invoice>> getInvoices() async => jList(await _api.get('/Invoices'), Invoice.fromJson);

  static Future<Invoice> getInvoice(String id) async => Invoice.fromJson(await _api.get('/Invoices/$id'));

  static Future<InvoiceUploadResult> uploadInvoice(File image) async =>
      InvoiceUploadResult.fromJson(await _api.uploadFile('/Invoices/upload', file: image, fields: _deviceFields()));

  static Future<String> resubmitInvoice(String id, File image) async {
    final res = await _api.uploadFile('/Invoices/$id/resubmit', file: image, fields: _deviceFields());
    return jStr((res as Map<String, dynamic>?)?['message'], 'تمت إعادة رفع الفاتورة.');
  }

  static Future<String> disputeInvoice(String id, String reason) async {
    final res = await _api.post('/Invoices/$id/dispute', {'disputeReason': reason});
    return jStr((res as Map<String, dynamic>?)?['message'], 'تم تقديم الاعتراض.');
  }

  static Map<String, String> _deviceFields() => {
        'DeviceModel': '${Platform.operatingSystem} ${Platform.operatingSystemVersion}'.trim(),
      };
}
