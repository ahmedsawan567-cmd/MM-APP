import 'json_utils.dart';

class StoreDetails {
  final String id;
  final String nameAr;
  final String nameEn;
  final String category;
  final String floor;
  final String unitNumber;
  final String? logoUrl;
  final String? coverImageUrl;
  final List<String> howToEarn;
  final List<StoreBooster> boosters;
  final int? pointsBalance;
  final StoreActivity? mine;
  final StoreReward? nextReward;
  final List<StoreReward> rewards;

  StoreDetails({
    required this.id,
    required this.nameAr,
    required this.nameEn,
    required this.category,
    required this.floor,
    required this.unitNumber,
    this.logoUrl,
    this.coverImageUrl,
    required this.howToEarn,
    required this.boosters,
    this.pointsBalance,
    this.mine,
    this.nextReward,
    required this.rewards,
  });

  factory StoreDetails.fromJson(Map<String, dynamic> j) {
    final s = (j['store'] as Map<String, dynamic>?) ?? const {};
    return StoreDetails(
      id: jStr(s['id']),
      nameAr: jStr(s['nameAr']),
      nameEn: jStr(s['nameEn']),
      category: jStr(s['category']),
      floor: jStr(s['floor']),
      unitNumber: jStr(s['unitNumber']),
      logoUrl: jStrOrNull(s['logoUrl']),
      coverImageUrl: jStrOrNull(s['coverImageUrl']),
      howToEarn: (j['howToEarn'] as List? ?? const []).map((e) => '$e').toList(),
      boosters: jList(j['boosters'], StoreBooster.fromJson),
      pointsBalance: j['pointsBalance'] == null ? null : jInt(j['pointsBalance']),
      mine: j['mine'] is Map<String, dynamic> ? StoreActivity.fromJson(j['mine']) : null,
      nextReward: j['nextReward'] is Map<String, dynamic> ? StoreReward.fromJson(j['nextReward']) : null,
      rewards: jList(j['rewards'], StoreReward.fromJson),
    );
  }
}

class StoreBooster {
  final String title;
  final int bonusPoints;
  final DateTime endsAt;
  StoreBooster({required this.title, required this.bonusPoints, required this.endsAt});
  factory StoreBooster.fromJson(Map<String, dynamic> j) =>
      StoreBooster(title: jStr(j['title']), bonusPoints: jInt(j['bonusPoints']), endsAt: jDate(j['endsAt']));
}

class StoreActivity {
  final int invoicesCount;
  final int approvedCount;
  final double totalSpent;
  final int pointsEarned;
  final DateTime? lastVisit;
  StoreActivity({required this.invoicesCount, required this.approvedCount, required this.totalSpent, required this.pointsEarned, this.lastVisit});
  factory StoreActivity.fromJson(Map<String, dynamic> j) => StoreActivity(
        invoicesCount: jInt(j['invoicesCount']),
        approvedCount: jInt(j['approvedCount']),
        totalSpent: jDouble(j['totalSpent']),
        pointsEarned: jInt(j['pointsEarned']),
        lastVisit: jDateOrNull(j['lastVisit']),
      );
}

class StoreReward {
  final String id;
  final String title;
  final String description;
  final String? imageUrl;
  final int pointsCost;
  final double discountValue;
  final bool atThisStore;
  final int remainingPoints;
  final double progress;
  final bool canRedeem;
  final double? estimatedSpend;

  StoreReward({
    required this.id,
    required this.title,
    required this.description,
    this.imageUrl,
    required this.pointsCost,
    required this.discountValue,
    required this.atThisStore,
    required this.remainingPoints,
    required this.progress,
    required this.canRedeem,
    this.estimatedSpend,
  });

  factory StoreReward.fromJson(Map<String, dynamic> j) => StoreReward(
        id: jStr(j['id']),
        title: jStr(j['title']),
        description: jStr(j['description']),
        imageUrl: jStrOrNull(j['imageUrl']),
        pointsCost: jInt(j['pointsCost']),
        discountValue: jDouble(j['discountValue']),
        atThisStore: jBool(j['atThisStore']),
        remainingPoints: jInt(j['remainingPoints']),
        progress: jDouble(j['progress']).clamp(0.0, 1.0),
        canRedeem: jBool(j['canRedeem']),
        estimatedSpend: j['estimatedSpend'] == null ? null : jDouble(j['estimatedSpend']),
      );
}
