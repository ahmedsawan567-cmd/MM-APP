import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../core/services/session.dart';
import '../../core/theme/app_theme.dart';
import '../../widgets/common.dart';

/// بطاقة العضوية: رقم العضوية المميز ورمز QR يعرضه العميل لموظف خدمة العملاء.
class MembershipCardScreen extends StatelessWidget {
  const MembershipCardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final user = Session.instance.user!;
    final number = user.membershipNumber ?? '—';
    final p = context.palette;
    return Scaffold(
      appBar: AppBar(title: const Text('بطاقة العضوية')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 28),
        children: [
          Container(
            padding: const EdgeInsets.all(22),
            decoration: BoxDecoration(
              gradient: Brand.gradient,
              borderRadius: BorderRadius.circular(AppRadius.xl),
              boxShadow: [BoxShadow(color: Brand.plum.withValues(alpha: 0.35), blurRadius: 24, offset: const Offset(0, 12))],
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Image.asset(Brand.markLight, width: 28, height: 28),
                    const SizedBox(width: 8),
                    const Text('المرأة مول', style: TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 16)),
                    const Spacer(),
                    Text(user.tierLabel, style: const TextStyle(color: Brand.glow, fontWeight: FontWeight.w700, fontSize: 12)),
                  ],
                ),
                const SizedBox(height: 22),
                Center(
                  child: Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(AppRadius.lg)),
                    child: QrImageView(
                      data: number,
                      size: 180,
                      eyeStyle: const QrEyeStyle(eyeShape: QrEyeShape.square, color: Brand.night),
                      dataModuleStyle: const QrDataModuleStyle(dataModuleShape: QrDataModuleShape.square, color: Brand.night),
                    ),
                  ),
                ),
                const SizedBox(height: 18),
                Text(user.fullName, style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.w700)),
                const SizedBox(height: 4),
                Text('رقم العضوية', style: TextStyle(color: Colors.white.withValues(alpha: 0.7), fontSize: 12)),
                Text(number,
                    textDirection: TextDirection.ltr,
                    style: const TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.w700, letterSpacing: 2)),
              ],
            ),
          ),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: () {
              Clipboard.setData(ClipboardData(text: number));
              showMessage(context, 'تم نسخ رقم العضوية');
            },
            icon: const Icon(LucideIcons.copy, size: 18),
            label: const Text('نسخ رقم العضوية'),
          ),
          const SizedBox(height: 16),
          Text(
            'اعرض هذه البطاقة أو اذكر رقم عضويتك لموظف خدمة العملاء للاستفسار عن نقاطك أو فواتيرك.',
            textAlign: TextAlign.center,
            style: TextStyle(color: p.muted, height: 1.6),
          ),
        ],
      ),
    );
  }
}
