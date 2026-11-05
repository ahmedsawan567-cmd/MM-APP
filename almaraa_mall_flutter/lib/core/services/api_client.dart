import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../constants/app_constants.dart';

/// خطأ قادم من الـ API برسالة عربية جاهزة للعرض للمستخدم.
class ApiException implements Exception {
  final int? statusCode;
  final String message;

  ApiException(this.message, {this.statusCode});

  bool get isUnauthorized => statusCode == 401;

  @override
  String toString() => message;
}

/// عميل HTTP موحّد للتعامل مع الـ API: يضيف توكن JWT، ويحوّل الأخطاء لرسائل مفهومة.
class ApiClient {
  ApiClient._();
  static final ApiClient instance = ApiClient._();

  static const _timeout = Duration(seconds: 20);
  static const _uploadTimeout = Duration(seconds: 60);

  final http.Client _http = http.Client();
  String? _token;

  /// يُستدعى عند انتهاء صلاحية الجلسة (401) لتسجيل الخروج تلقائياً.
  void Function()? onUnauthorized;

  void setToken(String? token) => _token = token;

  Uri _uri(String path, [Map<String, String?>? query]) {
    final q = <String, String>{};
    query?.forEach((k, v) {
      if (v != null && v.isNotEmpty) q[k] = v;
    });
    return Uri.parse('${AppConstants.apiBaseUrl}$path').replace(queryParameters: q.isEmpty ? null : q);
  }

  Map<String, String> _headers({bool json = false}) => {
        'Accept': 'application/json',
        if (json) 'Content-Type': 'application/json; charset=utf-8',
        if (_token != null) 'Authorization': 'Bearer $_token',
      };

  Future<dynamic> get(String path, {Map<String, String?>? query}) =>
      _send(() => _http.get(_uri(path, query), headers: _headers()));

  Future<dynamic> post(String path, [Object? body]) => _send(() => _http.post(
        _uri(path),
        headers: _headers(json: true),
        body: body == null ? null : jsonEncode(body),
      ));

  Future<dynamic> put(String path, [Object? body]) => _send(() => _http.put(
        _uri(path),
        headers: _headers(json: true),
        body: body == null ? null : jsonEncode(body),
      ));

  Future<dynamic> delete(String path, {Map<String, String?>? query}) =>
      _send(() => _http.delete(_uri(path, query), headers: _headers()));

  /// رفع ملف (صورة فاتورة) كـ multipart/form-data.
  Future<dynamic> uploadFile(
    String path, {
    required File file,
    String fieldName = 'File',
    Map<String, String> fields = const {},
  }) {
    return _send(() async {
      final request = http.MultipartRequest('POST', _uri(path))
        ..headers.addAll(_headers())
        ..fields.addAll(fields)
        ..files.add(await http.MultipartFile.fromPath(fieldName, file.path));
      final streamed = await request.send().timeout(_uploadTimeout);
      return http.Response.fromStream(streamed);
    }, timeout: _uploadTimeout);
  }

  Future<dynamic> _send(Future<http.Response> Function() call, {Duration timeout = _timeout}) async {
    http.Response response;
    try {
      response = await call().timeout(timeout);
    } on TimeoutException {
      throw ApiException('انتهت مهلة الاتصال بالخادم. تحقق من الإنترنت وحاول مجدداً.');
    } on SocketException {
      throw ApiException('تعذّر الوصول إلى الخادم. تأكد من الشبكة ومن عنوان الخادم في "إعدادات الخادم".');
    } on http.ClientException {
      throw ApiException('تعذّر الوصول إلى الخادم. تأكد من الشبكة ومن عنوان الخادم في "إعدادات الخادم".');
    }

    final body = _decode(response);

    if (response.statusCode >= 200 && response.statusCode < 300) {
      return body;
    }

    if (response.statusCode == 401) {
      onUnauthorized?.call();
    }

    throw ApiException(_extractMessage(body, response.statusCode), statusCode: response.statusCode);
  }

  dynamic _decode(http.Response response) {
    if (response.bodyBytes.isEmpty) return null;
    try {
      return jsonDecode(utf8.decode(response.bodyBytes));
    } catch (_) {
      return null;
    }
  }

  String _extractMessage(dynamic body, int statusCode) {
    if (body is Map<String, dynamic>) {
      final message = body['message'];
      if (message is String && message.isNotEmpty) return message;

      // أخطاء التحقق من ASP.NET (ModelState): { errors: { Field: ["..."] } }
      final errors = body['errors'];
      if (errors is Map && errors.isNotEmpty) {
        final first = errors.values.first;
        if (first is List && first.isNotEmpty) return first.first.toString();
      }
      final title = body['title'];
      if (title is String && title.isNotEmpty && statusCode != 400) return title;
    }

    switch (statusCode) {
      case 400:
        return 'البيانات المدخلة غير صحيحة.';
      case 401:
        return 'انتهت الجلسة. يرجى تسجيل الدخول مجدداً.';
      case 403:
        return 'ليس لديك صلاحية لهذا الإجراء.';
      case 404:
        return 'العنصر المطلوب غير موجود.';
      case 409:
        return 'هذه العملية مسجلة مسبقاً.';
      default:
        return 'حدث خطأ في الخادم ($statusCode). حاول لاحقاً.';
    }
  }
}
