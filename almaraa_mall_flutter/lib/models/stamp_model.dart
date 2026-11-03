class StampCardModel {
  final String cardId;
  final String tenantName;
  final int totalStampsNeeded;
  final int currentStamps;
  final String rewardDescription;
  final DateTime expiryDate;

  StampCardModel({
    required this.cardId,
    required this.tenantName,
    this.totalStampsNeeded = 10,
    required this.currentStamps,
    required this.rewardDescription,
    required this.expiryDate,
  });

  bool get isCompleted => currentStamps >= totalStampsNeeded;
  double get progress => (currentStamps / totalStampsNeeded).clamp(0.0, 1.0);
}
