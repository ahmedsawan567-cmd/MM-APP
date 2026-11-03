import 'json_utils.dart';

class Reward {
  final String id;
  final String title;
  final String description;
  final String? merchantName;
  final int pointsCost;
  final double discountValue;
  final String? imageUrl;
  final int? stockQuantity;
  final int validityDays;
  final DateTime? validUntil;

  Reward({
    required this.id,
    required this.title,
    required this.description,
    this.merchantName,
    required this.pointsCost,
    required this.discountValue,
    this.imageUrl,
    this.stockQuantity,
    required this.validityDays,
    this.validUntil,
  });

  bool get isOutOfStock => stockQuantity != null && stockQuantity! <= 0;

  factory Reward.fromJson(Map<String, dynamic> j) => Reward(
        id: jStr(j['id']),
        title: jStr(j['title']),
        description: jStr(j['description']),
        merchantName: jStrOrNull(j['merchantName']),
        pointsCost: jInt(j['pointsCost']),
        discountValue: jDouble(j['discountValue']),
        imageUrl: jStrOrNull(j['imageUrl']),
        stockQuantity: j['stockQuantity'] == null ? null : jInt(j['stockQuantity']),
        validityDays: jInt(j['validityDays'], 30),
        validUntil: jDateOrNull(j['validUntil']),
      );
}

class RedemptionResult {
  final String message;
  final String code;
  final String rewardTitle;
  final double discountValue;
  final int pointsSpent;
  final int newBalance;
  final DateTime expiresAt;

  RedemptionResult({
    required this.message,
    required this.code,
    required this.rewardTitle,
    required this.discountValue,
    required this.pointsSpent,
    required this.newBalance,
    required this.expiresAt,
  });

  factory RedemptionResult.fromJson(Map<String, dynamic> j) => RedemptionResult(
        message: jStr(j['message']),
        code: jStr(j['redemptionCode']),
        rewardTitle: jStr(j['rewardTitle']),
        discountValue: jDouble(j['discountValue']),
        pointsSpent: jInt(j['pointsSpent']),
        newBalance: jInt(j['currentWalletPointsBalance']),
        expiresAt: jDate(j['expiresAt']),
      );
}
