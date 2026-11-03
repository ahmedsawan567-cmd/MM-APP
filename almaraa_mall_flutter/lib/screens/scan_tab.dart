import 'package:flutter/material.dart';
import 'package:lucide_icons/lucide_icons.dart';
import '../core/constants/app_constants.dart';
import '../core/services/supabase_service.dart';
import '../models/invoice_model.dart';

class ScanTab extends StatefulWidget {
  final Function(int)? onNavigateToWallet;
  const ScanTab({super.key, this.onNavigateToWallet});

  @override
  State<ScanTab> createState() => _ScanTabState();
}

class _ScanTabState extends State<ScanTab> {
  bool _isProcessing = false;

  void _simulateScan(String merchant, double amount, String invNo) async {
    setState(() {
      _isProcessing = true;
    });

    // محاكاة معالجة الذكاء الاصطناعي واستخراج البيانات
    await Future.delayed(const Duration(milliseconds: 1400));

    final invoice = await SupabaseService.instance.submitInvoice(
      customerId: 'CUST-SARAH',
      merchantName: merchant,
      invoiceNumber: invNo,
      totalAmount: amount,
      imagePath: 'receipt_${DateTime.now().millisecondsSinceEpoch}.jpg',
    );

    if (mounted) {
      setState(() {
        _isProcessing = false;
      });
      _showResultSheet(invoice);
    }
  }

  void _showResultSheet(InvoiceModel invoice) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) {
        return Container(
          padding: const EdgeInsets.all(24),
          decoration: const BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: Colors.grey.shade300,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
              const SizedBox(height: 20),
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: AppConstants.successColor.withOpacity(0.12),
                  shape: BoxShape.circle,
                ),
                child: const Icon(LucideIcons.checkCircle2, color: AppConstants.successColor, size: 40),
              ),
              const SizedBox(height: 16),
              const Text(
                'تم التحقق من الفاتورة بنجاح!',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 6),
              Text(
                'تمت المطابقة والتحقق بالذكاء الاصطناعي وقيد النقاط في رصيدك',
                textAlign: TextAlign.center,
                style: TextStyle(color: Colors.grey.shade600, fontSize: 13),
              ),
              const SizedBox(height: 20),

              // Breakdown Card
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: AppConstants.bgLight,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: Colors.grey.shade200),
                ),
                child: Column(
                  children: [
                    _sheetRow('المتجر:', invoice.tenantName),
                    const Divider(height: 16),
                    _sheetRow('رقم الفاتورة:', invoice.invoiceNumber),
                    const Divider(height: 16),
                    _sheetRow('إجمالي المبلغ:', '${invoice.totalAmount.toStringAsFixed(2)} ${AppConstants.currency}'),
                    const Divider(height: 16),
                    _sheetRow('النقاط المكتسبة:', '+${invoice.pointsAwarded} نقطة', highlightColor: AppConstants.primaryGreen),
                    const Divider(height: 16),
                    _sheetRow('الاسترداد النقدي:', '+${invoice.cashbackAwarded.toStringAsFixed(2)} ${AppConstants.currency}', highlightColor: AppConstants.accentGold),
                  ],
                ),
              ),

              const SizedBox(height: 24),

              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: () {
                    Navigator.pop(context);
                    if (widget.onNavigateToWallet != null) {
                      widget.onNavigateToWallet!(3);
                    }
                  },
                  child: const Text('عرض المحفظة والنقاط'),
                ),
              ),
              const SizedBox(height: 10),
            ],
          ),
        );
      },
    );
  }

  Widget _sheetRow(String label, String value, {Color? highlightColor}) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: const TextStyle(color: Colors.grey, fontSize: 13)),
        Text(
          value,
          style: TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 14,
            color: highlightColor ?? AppConstants.textPrimary,
          ),
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.black,
        elevation: 0,
        iconTheme: const IconThemeData(color: Colors.white),
        title: const Text(
          'مسح الفاتورة ضوئياً (AI Scanner)',
          style: TextStyle(color: Colors.white, fontSize: 16, fontWeight: FontWeight.bold),
        ),
      ),
      body: Stack(
        children: [
          // Simulated Camera Viewport
          Center(
            child: Container(
              margin: const EdgeInsets.symmetric(horizontal: 24),
              height: 440,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(24),
                border: Border.all(color: AppConstants.accentGoldLight.withOpacity(0.8), width: 2),
              ),
              child: Stack(
                alignment: Alignment.center,
                children: [
                  Container(
                    decoration: BoxDecoration(
                      color: Colors.white.withOpacity(0.04),
                      borderRadius: BorderRadius.circular(22),
                    ),
                  ),

                  // Framing Corner Accents
                  _cornerMarker(Alignment.topLeft),
                  _cornerMarker(Alignment.topRight),
                  _cornerMarker(Alignment.bottomLeft),
                  _cornerMarker(Alignment.bottomRight),

                  if (_isProcessing) ...[
                    Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const CircularProgressIndicator(color: AppConstants.accentGoldLight),
                        const SizedBox(height: 16),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                          decoration: BoxDecoration(
                            color: Colors.black87,
                            borderRadius: BorderRadius.circular(20),
                          ),
                          child: const Text(
                            'جاري فحص الفاتورة والتحقق بالذكاء الاصطناعي...',
                            style: TextStyle(color: Colors.white, fontSize: 13),
                          ),
                        ),
                      ],
                    ),
                  ] else ...[
                    Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(LucideIcons.scanLine, color: Colors.white.withOpacity(0.6), size: 54),
                        const SizedBox(height: 12),
                        const Text(
                          'ضعي الفاتورة داخل الإطار للمسح التلقائي',
                          style: TextStyle(color: Colors.white70, fontSize: 13),
                        ),
                      ],
                    ),
                  ],
                ],
              ),
            ),
          ),

          // Action Bottom Controls
          Positioned(
            bottom: 30,
            left: 20,
            right: 20,
            child: Column(
              children: [
                // Quick Test Triggers (للاختبار الفوري)
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                  children: [
                    _testInvoiceBtn('كافي كادي (45 د.ل)', () => _simulateScan('كافي كادي (Kadi Café)', 45.0, 'KD-2041')),
                    _testInvoiceBtn('فساتين السهرة (1200 د.ل)', () => _simulateScan('بوتيك فساتين السهرة', 1200.0, 'EV-84920')),
                  ],
                ),
                const SizedBox(height: 16),

                // Main Trigger Button
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                  children: [
                    IconButton(
                      icon: const Icon(LucideIcons.image, color: Colors.white, size: 28),
                      onPressed: () => _simulateScan('سنيور كافيه', 45.0, 'SC-1002'),
                      tooltip: 'اختيار من الاستوديو',
                    ),
                    GestureDetector(
                      onTap: _isProcessing
                          ? null
                          : () => _simulateScan('دار الحرير للأزياء والعبايات', 290.0, 'DH-9481'),
                      child: Container(
                        width: 72,
                        height: 72,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          border: Border.all(color: Colors.white, width: 4),
                          color: AppConstants.accentGold,
                        ),
                        child: const Icon(LucideIcons.camera, color: Colors.white, size: 32),
                      ),
                    ),
                    IconButton(
                      icon: const Icon(LucideIcons.zap, color: Colors.white, size: 28),
                      onPressed: () {},
                      tooltip: 'الفلاش',
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _testInvoiceBtn(String label, VoidCallback onTap) {
    return InkWell(
      onTap: _isProcessing ? null : onTap,
      borderRadius: BorderRadius.circular(10),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: Colors.white12,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: Colors.white24),
        ),
        child: Row(
          children: [
            const Icon(LucideIcons.fileText, color: AppConstants.accentGoldLight, size: 14),
            const SizedBox(width: 6),
            Text(label, style: const TextStyle(color: Colors.white, fontSize: 11)),
          ],
        ),
      ),
    );
  }

  Widget _cornerMarker(Alignment alignment) {
    return Align(
      alignment: alignment,
      child: Container(
        width: 24,
        height: 24,
        margin: const EdgeInsets.all(8),
        decoration: BoxDecoration(
          border: Border(
            top: alignment.y < 0 ? const BorderSide(color: AppConstants.accentGoldLight, width: 4) : BorderSide.none,
            bottom: alignment.y > 0 ? const BorderSide(color: AppConstants.accentGoldLight, width: 4) : BorderSide.none,
            left: alignment.x < 0 ? const BorderSide(color: AppConstants.accentGoldLight, width: 4) : BorderSide.none,
            right: alignment.x > 0 ? const BorderSide(color: AppConstants.accentGoldLight, width: 4) : BorderSide.none,
          ),
        ),
      ),
    );
  }
}
