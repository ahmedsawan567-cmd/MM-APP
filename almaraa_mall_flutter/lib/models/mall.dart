import 'json_utils.dart';

class Store {
  final String tenantId;
  final String nameAr;
  final String nameEn;
  final String unitNumber;
  final String category;
  final String? logoUrl;
  final String? coverImageUrl;
  final String floorLabel;
  final String rewardType;
  final double conversionRate;

  Store({
    required this.tenantId,
    required this.nameAr,
    required this.nameEn,
    required this.unitNumber,
    required this.category,
    this.logoUrl,
    this.coverImageUrl,
    required this.floorLabel,
    required this.rewardType,
    required this.conversionRate,
  });

  /// وصف مختصر لنظام المكافأة في المتجر.
  String get rewardLabel {
    if (rewardType == 'CASHBACK') {
      return 'كاش باك ${(conversionRate * 100).toStringAsFixed(0)}%';
    }
    if (conversionRate <= 0) return 'نقاط المول';
    final perPoint = (1 / conversionRate);
    return 'نقطة لكل ${perPoint.toStringAsFixed(perPoint == perPoint.roundToDouble() ? 0 : 1)} د.ل';
  }

  factory Store.fromJson(Map<String, dynamic> j) => Store(
        tenantId: jStr(j['tenantId']),
        nameAr: jStr(j['nameAr']),
        nameEn: jStr(j['nameEn']),
        unitNumber: jStr(j['unitNumber']),
        category: jStr(j['category'], 'عام'),
        logoUrl: jStrOrNull(j['logoUrl']),
        coverImageUrl: jStrOrNull(j['coverImageUrl']),
        floorLabel: jStr(j['floorLabel']),
        rewardType: jStr(j['rewardType'], 'POINTS'),
        conversionRate: jDouble(j['conversionRate'], 0.1),
      );
}

class HomeData {
  final HomeUser? user;
  final List<HomeBanner> banners;
  final List<HomeCategory> categories;
  final List<BoosterOffer> boosters;
  final List<StoreSpotlight> topStores;
  final bool maintenanceMode;

  HomeData({
    this.user,
    required this.banners,
    required this.categories,
    required this.boosters,
    required this.topStores,
    required this.maintenanceMode,
  });

  factory HomeData.fromJson(Map<String, dynamic> j) => HomeData(
        user: j['user'] is Map<String, dynamic> ? HomeUser.fromJson(j['user']) : null,
        banners: jList(j['heroBanners'], HomeBanner.fromJson),
        categories: jList(j['quickCategories'], HomeCategory.fromJson),
        boosters: jList(j['hotBoosters'], BoosterOffer.fromJson),
        topStores: jList(j['topStores'], StoreSpotlight.fromJson),
        maintenanceMode: jBool((j['layoutMeta'] as Map<String, dynamic>?)?['maintenanceMode']),
      );
}

class HomeUser {
  final String fullName;
  final String loyaltyTier;
  final int pointsBalance;
  final double tierProgress;
  final int pointsToNextTier;
  final String nextTierName;
  final double tierMultiplier;
  /// أسماء العضويات بالعربية ومزاياها كما يضبطها المول من المنظومة.
  final String tierName;
  final String? nextTierNameAr;
  final List<String> tierBenefits;

  HomeUser({
    required this.fullName,
    required this.loyaltyTier,
    required this.pointsBalance,
    required this.tierProgress,
    required this.pointsToNextTier,
    required this.nextTierName,
    required this.tierMultiplier,
    this.tierName = '',
    this.nextTierNameAr,
    this.tierBenefits = const [],
  });

  factory HomeUser.fromJson(Map<String, dynamic> j) => HomeUser(
        fullName: jStr(j['fullName']),
        loyaltyTier: jStr(j['loyaltyTier'], 'BRONZE'),
        pointsBalance: jInt(j['pointsBalance']),
        tierProgress: jDouble(j['tierProgressPercentage']),
        pointsToNextTier: jInt(j['pointsToNextTier']),
        nextTierName: jStr(j['nextTierName']),
        tierMultiplier: jDouble(j['tierMultiplier'], 1),
        tierName: jStr(j['loyaltyTierName']),
        nextTierNameAr: jStrOrNull(j['nextTierNameAr']),
        tierBenefits: (j['tierBenefits'] as List? ?? const []).map((e) => '$e').toList(),
      );
}

class HomeBanner {
  final String title;
  final String? subtitle;
  final String imageUrl;
  final String actionType;
  final String? targetUrl;

  HomeBanner({required this.title, this.subtitle, required this.imageUrl, required this.actionType, this.targetUrl});

  factory HomeBanner.fromJson(Map<String, dynamic> j) => HomeBanner(
        title: jStr(j['title']),
        subtitle: jStrOrNull(j['subtitle']),
        imageUrl: jStr(j['imageUrl']),
        actionType: jStr(j['actionType']),
        targetUrl: jStrOrNull(j['targetUrl']),
      );
}

class HomeCategory {
  final String key;
  final String nameAr;
  final String colorHex;
  final int storesCount;

  HomeCategory({required this.key, required this.nameAr, required this.colorHex, required this.storesCount});

  factory HomeCategory.fromJson(Map<String, dynamic> j) => HomeCategory(
        key: jStr(j['key']),
        nameAr: jStr(j['nameAr']),
        colorHex: jStr(j['colorHex']),
        storesCount: jInt(j['storesCount']),
      );
}

class BoosterOffer {
  final String title;
  final String storeName;
  final int bonusPoints;
  final double multiplier;
  final int daysRemaining;

  BoosterOffer({
    required this.title,
    required this.storeName,
    required this.bonusPoints,
    required this.multiplier,
    required this.daysRemaining,
  });

  factory BoosterOffer.fromJson(Map<String, dynamic> j) => BoosterOffer(
        title: jStr(j['title']),
        storeName: jStr(j['storeName']),
        bonusPoints: jInt(j['bonusPoints']),
        multiplier: jDouble(j['pointsMultiplier'], 1),
        daysRemaining: jInt(j['daysRemaining']),
      );
}

class StoreSpotlight {
  final String tenantId;
  final String nameAr;
  final String unitNumber;
  final String category;
  final String? logoUrl;
  final String floorLabel;

  StoreSpotlight({
    required this.tenantId,
    required this.nameAr,
    required this.unitNumber,
    required this.category,
    this.logoUrl,
    required this.floorLabel,
  });

  factory StoreSpotlight.fromJson(Map<String, dynamic> j) => StoreSpotlight(
        tenantId: jStr(j['tenantId']),
        nameAr: jStr(j['nameAr']),
        unitNumber: jStr(j['unitNumber']),
        category: jStr(j['category']),
        logoUrl: jStrOrNull(j['logoUrl']),
        floorLabel: jStr(j['floorLabel']),
      );
}
