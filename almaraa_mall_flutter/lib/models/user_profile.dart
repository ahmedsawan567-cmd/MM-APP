import 'json_utils.dart';

class UserProfile {
  final String id;
  final String fullName;
  final String phoneNumber;
  final String role;
  final int pointsBalance;
  final DateTime createdAt;

  UserProfile({
    required this.id,
    required this.fullName,
    required this.phoneNumber,
    required this.role,
    required this.pointsBalance,
    required this.createdAt,
  });

  factory UserProfile.fromJson(Map<String, dynamic> j) => UserProfile(
        id: jStr(j['id']),
        fullName: jStr(j['fullName'], 'متسوق'),
        phoneNumber: jStr(j['phoneNumber']),
        role: jStr(j['role'], 'CUSTOMER'),
        pointsBalance: jInt(j['pointsBalance']),
        createdAt: jDate(j['createdAt']),
      );
}

class AuthResult {
  final String token;
  final String role;
  final DateTime expiresAt;

  AuthResult({required this.token, required this.role, required this.expiresAt});

  factory AuthResult.fromJson(Map<String, dynamic> j) => AuthResult(
        token: jStr(j['token']),
        role: jStr(j['role']),
        expiresAt: jDate(j['expiresAt']),
      );
}
