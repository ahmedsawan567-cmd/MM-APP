import 'json_utils.dart';

class WalletSummary {
  final int pointsBalance;
  final int totalPointsEarned;
  final int totalPointsSpent;
  final int activeVouchersCount;

  WalletSummary({
    required this.pointsBalance,
    required this.totalPointsEarned,
    required this.totalPointsSpent,
    required this.activeVouchersCount,
  });

  factory WalletSummary.fromJson(Map<String, dynamic> j) => WalletSummary(
        pointsBalance: jInt(j['pointsBalance']),
        totalPointsEarned: jInt(j['totalPointsEarned']),
        totalPointsSpent: jInt(j['totalPointsSpent']),
        activeVouchersCount: jInt(j['activeVouchersCount']),
      );
}

class PointTransaction {
  final String id;
  final int amount;
  final int balanceAfter;
  final String type;
  final String description;
  final DateTime createdAt;

  PointTransaction({
    required this.id,
    required this.amount,
    required this.balanceAfter,
    required this.type,
    required this.description,
    required this.createdAt,
  });

  bool get isCredit => amount >= 0;

  factory PointTransaction.fromJson(Map<String, dynamic> j) => PointTransaction(
        id: jStr(j['id']),
        amount: jInt(j['amount']),
        balanceAfter: jInt(j['balanceAfter']),
        type: jStr(j['type']),
        description: jStr(j['description']),
        createdAt: jDate(j['createdAt']),
      );
}

class Voucher {
  final String id;
  final String code;
  final String rewardTitle;
  final String? merchantName;
  final double discountValue;
  final int pointsSpent;
  final String status;
  final DateTime redeemedAt;
  final DateTime? usedAt;
  final DateTime expiresAt;

  Voucher({
    required this.id,
    required this.code,
    required this.rewardTitle,
    this.merchantName,
    required this.discountValue,
    required this.pointsSpent,
    required this.status,
    required this.redeemedAt,
    this.usedAt,
    required this.expiresAt,
  });

  bool get isActive => status == 'ACTIVE' && expiresAt.isAfter(DateTime.now());

  /// الحالة الفعلية للعرض (قسيمة نشطة انتهى تاريخها تعتبر منتهية).
  String get effectiveStatus => status == 'ACTIVE' && !isActive ? 'EXPIRED' : status;

  factory Voucher.fromJson(Map<String, dynamic> j) => Voucher(
        id: jStr(j['id']),
        code: jStr(j['redemptionCode']),
        rewardTitle: jStr(j['rewardTitle']),
        merchantName: jStrOrNull(j['merchantName']),
        discountValue: jDouble(j['discountValue']),
        pointsSpent: jInt(j['pointsSpent']),
        status: jStr(j['status'], 'ACTIVE'),
        redeemedAt: jDate(j['redeemedAt']),
        usedAt: jDateOrNull(j['usedAt']),
        expiresAt: jDate(j['expiresAt']),
      );
}
