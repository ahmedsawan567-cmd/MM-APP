class CustomerModel {
  final String customerId;
  final String fullName;
  final String phoneNumber;
  final String loyaltyTier;
  final int pointsBalance;
  final double cashbackBalance;
  final int completedStamps;
  final bool isFrozen;

  CustomerModel({
    required this.customerId,
    required this.fullName,
    required this.phoneNumber,
    this.loyaltyTier = 'VIP',
    this.pointsBalance = 1450,
    this.cashbackBalance = 72.50,
    this.completedStamps = 7,
    this.isFrozen = false,
  });

  factory CustomerModel.fromJson(Map<String, dynamic> json) {
    return CustomerModel(
      customerId: json['CustomerId'] ?? json['customer_id'] ?? '',
      fullName: json['FullName'] ?? json['full_name'] ?? 'متسوق المرأة مول',
      phoneNumber: json['PhoneNumber'] ?? json['phone_number'] ?? '',
      loyaltyTier: json['LoyaltyTier'] ?? json['loyalty_tier'] ?? 'VIP',
      pointsBalance: json['PointsBalance'] ?? 1450,
      cashbackBalance: (json['CashbackBalance'] ?? 72.50).toDouble(),
      completedStamps: json['CompletedStamps'] ?? 7,
      isFrozen: json['IsFrozen'] ?? false,
    );
  }
}
