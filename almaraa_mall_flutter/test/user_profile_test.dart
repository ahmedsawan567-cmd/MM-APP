import 'package:almaraa_mall/models/user_profile.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('parses membership number and optional profile fields', () {
    final u = UserProfile.fromJson({
      'id': 'CUST-1',
      'fullName': 'سارة',
      'phoneNumber': '0912345678',
      'membershipNumber': 'MM-100045',
      'email': 'sara@mail.ly',
      'birthDate': '1995-04-12T00:00:00',
      'gender': 'F',
      'loyaltyTier': 'GOLD',
      'role': 'CUSTOMER',
      'pointsBalance': 320,
      'createdAt': '2026-01-01T00:00:00Z',
    });
    expect(u.membershipNumber, 'MM-100045');
    expect(u.birthDate?.year, 1995);
    expect(u.tierLabel, 'العضوية الذهبية');

    final minimal = UserProfile.fromJson({'id': 'x', 'fullName': 'a', 'phoneNumber': '09', 'createdAt': '2026-01-01'});
    expect(minimal.membershipNumber, isNull);
    expect(minimal.email, isNull);
  });
}
