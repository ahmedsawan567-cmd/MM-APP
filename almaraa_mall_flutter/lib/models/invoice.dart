import 'json_utils.dart';

class Invoice {
  final String id;
  final String merchantName;
  final String invoiceNumber;
  final DateTime invoiceDate;
  final double totalAmount;
  final int pointsEarned;
  final String status;
  final String? rejectionReason;
  final String? imageUrl;
  final DateTime createdAt;

  Invoice({
    required this.id,
    required this.merchantName,
    required this.invoiceNumber,
    required this.invoiceDate,
    required this.totalAmount,
    required this.pointsEarned,
    required this.status,
    this.rejectionReason,
    this.imageUrl,
    required this.createdAt,
  });

  bool get canDispute => status == 'REJECTED';
  bool get canResubmit => status == 'RESUBMISSION_REQUESTED' || status == 'REJECTED';

  factory Invoice.fromJson(Map<String, dynamic> j) => Invoice(
        id: jStr(j['id']),
        merchantName: jStr(j['merchantName'], 'متجر'),
        invoiceNumber: jStr(j['invoiceNumber']),
        invoiceDate: jDate(j['invoiceDate']),
        totalAmount: jDouble(j['totalAmount']),
        pointsEarned: jInt(j['pointsEarned']),
        status: jStr(j['status'], 'PENDING'),
        rejectionReason: jStrOrNull(j['rejectionReason']),
        imageUrl: jStrOrNull(j['imageUrl']),
        createdAt: jDate(j['createdAt']),
      );
}

class InvoiceUploadResult {
  final bool success;
  final String message;
  final Invoice? invoice;
  final int pointsEarned;
  final int newBalance;

  InvoiceUploadResult({
    required this.success,
    required this.message,
    this.invoice,
    required this.pointsEarned,
    required this.newBalance,
  });

  bool get isApproved => invoice?.status == 'APPROVED';

  factory InvoiceUploadResult.fromJson(Map<String, dynamic> j) => InvoiceUploadResult(
        success: jBool(j['success'], true),
        message: jStr(j['message']),
        invoice: j['invoice'] is Map<String, dynamic> ? Invoice.fromJson(j['invoice']) : null,
        pointsEarned: jInt(j['pointsEarned']),
        newBalance: jInt(j['currentWalletPointsBalance']),
      );
}
