import 'package:almaraa_mall/models/app_notification.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('parses a notification and marks it read', () {
    final n = AppNotification.fromJson({
      'id': '6f1c',
      'type': 'INVOICE_APPROVED',
      'title': 'تمت إضافة 25 نقطة',
      'body': 'فاتورة كافي كادي',
      'link': 'invoice:abc',
      'isRead': false,
      'createdAt': '2026-11-05T10:00:00Z',
    });
    expect(n.link, 'invoice:abc');
    expect(n.isRead, isFalse);
    expect(n.copyWith(isRead: true).isRead, isTrue);
  });
}
