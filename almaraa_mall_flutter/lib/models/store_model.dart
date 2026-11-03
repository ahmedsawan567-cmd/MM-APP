class StoreModel {
  final String tenantId;
  final String nameAr;
  final String nameEn;
  final String unitNumber;
  final String category;
  final String? logoUrl;
  final bool isActive;
  final String rewardType;
  final double conversionRate;
  final String? floor;
  final String? discountOffer;

  StoreModel({
    required this.tenantId,
    required this.nameAr,
    required this.nameEn,
    required this.unitNumber,
    required this.category,
    this.logoUrl,
    this.isActive = true,
    this.rewardType = 'CASHBACK',
    this.conversionRate = 0.05,
    this.floor,
    this.discountOffer,
  });

  factory StoreModel.fromJson(Map<String, dynamic> json) {
    return StoreModel(
      tenantId: json['TenantId'] ?? json['tenant_id'] ?? '',
      nameAr: json['NameAr'] ?? json['name_ar'] ?? '',
      nameEn: json['NameEn'] ?? json['name_en'] ?? '',
      unitNumber: json['UnitNumber'] ?? json['unit_number'] ?? '',
      category: json['Category'] ?? json['category'] ?? 'عام',
      logoUrl: json['LogoUrl'] ?? json['logo_url'],
      isActive: json['IsActive'] ?? json['is_active'] ?? true,
      rewardType: json['RewardType'] ?? json['reward_type'] ?? 'CASHBACK',
      conversionRate: (json['ConversionRate'] ?? json['conversion_rate'] ?? 0.05).toDouble(),
      floor: json['Floor'] ?? 'الطابق الأرضي',
      discountOffer: json['DiscountOffer'],
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'TenantId': tenantId,
      'NameAr': nameAr,
      'NameEn': nameEn,
      'UnitNumber': unitNumber,
      'Category': category,
      'LogoUrl': logoUrl,
      'IsActive': isActive,
    };
  }
}
