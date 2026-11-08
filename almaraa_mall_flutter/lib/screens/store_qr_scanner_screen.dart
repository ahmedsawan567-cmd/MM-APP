import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../core/theme/app_theme.dart';

/// كاميرا لمسح باركود المتجر. ترجع محتوى الكود عند العثور على كود متجر المرأة مول.
class StoreQrScannerScreen extends StatefulWidget {
  const StoreQrScannerScreen({super.key});

  @override
  State<StoreQrScannerScreen> createState() => _StoreQrScannerScreenState();
}

class _StoreQrScannerScreenState extends State<StoreQrScannerScreen> {
  final _controller = MobileScannerController(formats: const [BarcodeFormat.qrCode], detectionSpeed: DetectionSpeed.noDuplicates);
  bool _done = false;
  String? _hint;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_done) return;
    for (final b in capture.barcodes) {
      final v = b.rawValue;
      if (v == null) continue;
      if (v.toUpperCase().contains('MMALL-STORE')) {
        _done = true;
        Navigator.of(context).pop(v);
        return;
      }
      setState(() => _hint = 'هذا الكود لا يخص متجراً في المرأة مول');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: const Text('امسح باركود المتجر'),
        actions: [
          IconButton(icon: const Icon(LucideIcons.flashlight), tooltip: 'الإضاءة', onPressed: () => _controller.toggleTorch()),
        ],
      ),
      body: Stack(
        fit: StackFit.expand,
        children: [
          MobileScanner(
            controller: _controller,
            onDetect: _onDetect,
            errorBuilder: (context, error) => Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Text('تعذّر تشغيل الكاميرا. اسمح للتطبيق باستخدام الكاميرا من إعدادات الهاتف.',
                    textAlign: TextAlign.center, style: const TextStyle(color: Colors.white)),
              ),
            ),
          ),
          Center(
            child: Container(
              width: 250,
              height: 250,
              decoration: BoxDecoration(
                border: Border.all(color: Brand.magenta, width: 3),
                borderRadius: BorderRadius.circular(AppRadius.xl),
              ),
            ),
          ),
          Positioned(
            left: 24,
            right: 24,
            bottom: 48,
            child: Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(color: Colors.black.withValues(alpha: 0.6), borderRadius: BorderRadius.circular(AppRadius.lg)),
              child: Text(
                _hint ?? 'وجّه الكاميرا إلى باركود المرأة مول الموجود عند كاشير المتجر',
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.white, height: 1.5),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
