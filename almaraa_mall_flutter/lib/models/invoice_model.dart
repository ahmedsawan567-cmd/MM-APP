class InvoiceModel {
  final String invoiceId;
  final String customerId;
  final String tenantName;
  final String invoiceNumber;
  final DateTime invoiceDate;
  final double totalAmount;
  final String processingStatus;
  final int pointsAwarded;
  final double cashbackAwarded;
  final String? rejectionReason;
  final String? receiptImageUrl;

  InvoiceModel({
    required this.invoiceId,
    required this.customerId,
    required this.tenantName,
    required this.invoiceNumber,
    required this.invoiceDate,
    required this.totalAmount,
    required this.processingStatus,
    this.pointsAwarded = 0,
    this.cashbackAwarded = 0.0,
    this.rejectionReason,
    this.receiptImageUrl,
  });

  factory InvoiceModel.fromJson(Map<String, dynamic> json) {
    return InvoiceModel(
      invoiceId: json['InvoiceId']?.toString() ?? json['invoice_id']?.toString() ?? '',
      customerId: json['CustomerId'] ?? json['customer_id'] ?? '',
      tenantName: json['ExtractedMerchant'] ?? json['TenantName'] ?? 'متجر غير معروف',
      invoiceNumber: json['InvoiceNumber'] ?? json['invoice_number'] ?? 'INV-000',
      invoiceDate: json['InvoiceDate'] != null ? DateTime.parse(json['InvoiceDate']) : DateTime.now(),
      totalAmount: (json['TotalAmount'] ?? json['total_amount'] ?? 0.0).toDouble(),
      processingStatus: json['ProcessingStatus'] ?? json['processing_status'] ?? 'PENDING',
      pointsAwarded: json['PointsAwarded'] ?? 0,
      cashbackAwarded: (json['CashbackAwarded'] ?? 0.0).toDouble(),
      rejectionReason: json['RejectionReason'],
      receiptImageUrl: json['ReceiptImageUrl'],
    );
  }
}
