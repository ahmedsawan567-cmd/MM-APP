import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/services/loyalty_api.dart';
import '../core/services/session.dart';
import '../core/theme/app_theme.dart';
import '../widgets/common.dart';

/// بعد مسح باركود المتجر أو قراءة بطاقة NFC: تُضاف نقاط المتجر فوراً وتظهر نافذة التهنئة.
Future<void> claimStorePoints(BuildContext context, {required String code, required String method}) async {
  // نافذة انتظار قصيرة أثناء الإرسال
  showDialog(
    context: context,
    barrierDismissible: false,
    builder: (_) => const Center(child: CircularProgressIndicator()),
  );
  Map<String, dynamic>? result;
  String? error;
  try {
    result = await LoyaltyApi.claimStorePoints(code: code, method: method);
    Session.instance.notifyDataChanged();
  } catch (e) {
    error = e.toString();
  }
  if (!context.mounted) return;
  Navigator.of(context, rootNavigator: true).pop();

  await showModalBottomSheet(
    context: context,
    builder: (ctx) {
      final p = ctx.palette;
      final ok = result != null;
      final points = (result?['points'] as num?)?.toInt() ?? 0;
      final balance = (result?['newBalance'] as num?)?.toInt();
      return Padding(
        padding: const EdgeInsets.fromLTRB(24, 8, 24, 28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            IconBadge(ok ? LucideIcons.partyPopper : LucideIcons.circleAlert, tone: ok ? Tone.gold : Tone.warning, size: 72),
            const SizedBox(height: 14),
            if (ok) ...[
              Text('+${formatPoints(points)} نقطة', style: const TextStyle(fontSize: 30, fontWeight: FontWeight.w800, color: Brand.purple)),
              const SizedBox(height: 4),
              Text('من ${result['storeName'] ?? 'المتجر'}', style: Theme.of(ctx).textTheme.titleMedium),
              if (balance != null) ...[
                const SizedBox(height: 8),
                Text('رصيدك الآن ${formatPoints(balance)} نقطة', style: TextStyle(color: p.muted)),
              ],
            ] else ...[
              Text('لم تُضف النقاط', style: Theme.of(ctx).textTheme.titleLarge),
              const SizedBox(height: 8),
              Text(error ?? '', textAlign: TextAlign.center, style: TextStyle(color: p.muted, height: 1.6)),
            ],
            const SizedBox(height: 20),
            SizedBox(width: double.infinity, child: ElevatedButton(onPressed: () => Navigator.pop(ctx), child: const Text('تم'))),
          ],
        ),
      );
    },
  );
}
