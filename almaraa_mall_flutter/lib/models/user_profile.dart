import 'json_utils.dart';

class UserProfile {
  final String id;
  final String fullName;
  final String phoneNumber;
  /// رقم العضوية المميز (مثل MM-100001).
  final String? membershipNumber;
  final String? email;
  final DateTime? birthDate;
  /// M أو F
  final String? gender;
  final String loyaltyTier;
  final String role;
  final int pointsBalance;
  final DateTime createdAt;

  UserProfile({
    required this.id,
    required this.fullName,
    required this.phoneNumber,
    this.membershipNumber,
    this.email,
    this.birthDate,
    this.gender,
    this.loyaltyTier = 'BRONZE',
    required this.role,
    required this.pointsBalance,
    required this.createdAt,
  });

  factory UserProfile.fromJson(Map<String, dynamic> j) => UserProfile(
        id: jStr(j['id']),
        fullName: jStr(j['fullName'], 'متسوق'),
        phoneNumber: jStr(j['phoneNumber']),
        membershipNumber: jStrOrNull(j['membershipNumber']),
        email: jStrOrNull(j['email']),
        birthDate: jDateOrNull(j['birthDate']),
        gender: jStrOrNull(j['gender']),
        loyaltyTier: jStr(j['loyaltyTier'], 'BRONZE'),
        role: jStr(j['role'], 'CUSTOMER'),
        pointsBalance: jInt(j['pointsBalance']),
        createdAt: jDate(j['createdAt']),
      );

  String get tierLabel => switch (loyaltyTier.toUpperCase()) {
        'VIP' => 'عضوية VIP',
        'GOLD' => 'العضوية الذهبية',
        'SILVER' => 'العضوية الفضية',
        _ => 'العضوية البرونزية',
      };
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
