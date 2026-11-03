import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';

// ---------------- تنسيقات ----------------
final _money = NumberFormat('#,##0.##', 'en');
final _number = NumberFormat('#,##0', 'en');

String formatMoney(double v) => '${_money.format(v)} ${AppConstants.currency}';
String formatPoints(int v) => _number.format(v);
String formatDate(DateTime d) => DateFormat('yyyy/MM/dd').format(d);
String formatDateTime(DateTime d) => DateFormat('yyyy/MM/dd  HH:mm').format(d);

void showMessage(BuildContext context, String message, {bool error = false}) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(
      content: Text(message),
      backgroundColor: error ? AppConstants.dangerColor : AppConstants.primaryGreen,
      behavior: SnackBarBehavior.floating,
    ));
}

// ---------------- حالات التحميل والخطأ والفراغ ----------------
class LoadingView extends StatelessWidget {
  const LoadingView({super.key});

  @override
  Widget build(BuildContext context) =>
      const Center(child: CircularProgressIndicator(color: AppConstants.primaryGreen));
}

class ErrorView extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;

  const ErrorView({super.key, required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(LucideIcons.wifiOff, size: 48, color: AppConstants.textMuted),
            const SizedBox(height: 16),
            Text(message, textAlign: TextAlign.center, style: const TextStyle(fontSize: 15)),
            const SizedBox(height: 20),
            ElevatedButton.icon(
              onPressed: onRetry,
              icon: const Icon(LucideIcons.refreshCw, size: 18),
              label: const Text('إعادة المحاولة'),
            ),
          ],
        ),
      ),
    );
  }
}

class EmptyView extends StatelessWidget {
  final IconData icon;
  final String title;
  final String? subtitle;

  const EmptyView({super.key, required this.icon, required this.title, this.subtitle});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 48, horizontal: 32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 52, color: AppConstants.textMuted.withOpacity(0.5)),
          const SizedBox(height: 14),
          Text(title, textAlign: TextAlign.center, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
          if (subtitle != null) ...[
            const SizedBox(height: 6),
            Text(subtitle!, textAlign: TextAlign.center, style: const TextStyle(color: AppConstants.textMuted)),
          ],
        ],
      ),
    );
  }
}

/// يبني الواجهة حسب حالة Future: تحميل، خطأ مع إعادة محاولة، أو البيانات.
class AsyncView<T> extends StatelessWidget {
  final Future<T>? future;
  final VoidCallback onRetry;
  final Widget Function(BuildContext, T) builder;

  const AsyncView({super.key, required this.future, required this.onRetry, required this.builder});

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<T>(
      future: future,
      builder: (context, snap) {
        if (snap.connectionState != ConnectionState.done) return const LoadingView();
        if (snap.hasError) return ErrorView(message: snap.error.toString(), onRetry: onRetry);
        return builder(context, snap.data as T);
      },
    );
  }
}

// ---------------- شارات الحالة ----------------
class StatusChip extends StatelessWidget {
  final String label;
  final Color color;

  const StatusChip({super.key, required this.label, required this.color});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withOpacity(0.12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(label, style: TextStyle(color: color, fontSize: 12, fontWeight: FontWeight.bold)),
    );
  }

  factory StatusChip.invoice(String status) {
    switch (status) {
      case 'APPROVED':
        return const StatusChip(label: 'مقبولة', color: AppConstants.successColor);
      case 'REJECTED':
        return const StatusChip(label: 'مرفوضة', color: AppConstants.dangerColor);
      case 'RESUBMISSION_REQUESTED':
        return const StatusChip(label: 'مطلوب إعادة التصوير', color: AppConstants.warningColor);
      case 'DISPUTED':
        return const StatusChip(label: 'اعتراض قيد المراجعة', color: Colors.indigo);
      case 'VOIDED':
        return const StatusChip(label: 'ملغاة', color: AppConstants.textMuted);
      default: // PENDING, PROCESSING, REVIEW_REQUIRED
        return const StatusChip(label: 'قيد المراجعة', color: AppConstants.accentGold);
    }
  }

  factory StatusChip.voucher(String status) {
    switch (status) {
      case 'ACTIVE':
        return const StatusChip(label: 'نشطة', color: AppConstants.successColor);
      case 'USED':
        return const StatusChip(label: 'مستخدمة', color: AppConstants.textMuted);
      case 'CANCELLED':
        return const StatusChip(label: 'ملغاة', color: AppConstants.dangerColor);
      default:
        return const StatusChip(label: 'منتهية', color: AppConstants.dangerColor);
    }
  }
}

class SectionTitle extends StatelessWidget {
  final String title;
  final String? actionLabel;
  final VoidCallback? onAction;

  const SectionTitle(this.title, {super.key, this.actionLabel, this.onAction});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(title, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
        if (actionLabel != null)
          TextButton(
            onPressed: onAction,
            child: Text(actionLabel!, style: const TextStyle(color: AppConstants.primaryGreenLight)),
          ),
      ],
    );
  }
}

/// شعار متجر: صورة إن وُجدت، وإلا الحرف الأول من الاسم.
class StoreLogo extends StatelessWidget {
  final String name;
  final String? logoUrl;
  final double size;

  const StoreLogo({super.key, required this.name, this.logoUrl, this.size = 48});

  @override
  Widget build(BuildContext context) {
    final url = AppConstants.resolveImageUrl(logoUrl);
    final fallback = Center(
      child: Text(
        name.isNotEmpty ? name.characters.first : '?',
        style: TextStyle(fontSize: size * 0.42, fontWeight: FontWeight.bold, color: AppConstants.primaryGreen),
      ),
    );
    return Container(
      width: size,
      height: size,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: AppConstants.primaryGreen.withOpacity(0.08),
        borderRadius: BorderRadius.circular(size * 0.28),
      ),
      child: url == null
          ? fallback
          : Image.network(url, fit: BoxFit.cover, errorBuilder: (_, __, ___) => fallback),
    );
  }
}
