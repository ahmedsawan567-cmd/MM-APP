import 'package:flutter/material.dart';
import 'package:intl/intl.dart' show DateFormat, NumberFormat;
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/theme/app_theme.dart';

// ---------------- تنسيقات ----------------
final _money = NumberFormat('#,##0.##', 'en');
final _number = NumberFormat('#,##0', 'en');

String formatMoney(double v) => '${_money.format(v)} ${AppConstants.currency}';
String formatPoints(int v) => _number.format(v);
String formatDate(DateTime d) => DateFormat('yyyy/MM/dd').format(d);
String formatDateTime(DateTime d) => DateFormat('yyyy/MM/dd  HH:mm').format(d);

void showMessage(BuildContext context, String message, {bool error = false}) {
  final p = context.palette;
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(
      content: Row(
        children: [
          Icon(error ? LucideIcons.circleAlert : LucideIcons.circleCheck, color: Colors.white, size: 20),
          const SizedBox(width: 10),
          Expanded(child: Text(message)),
        ],
      ),
      backgroundColor: error ? p.danger : p.brandStrong,
    ));
}

/// يفتح صفحة بانتقال ناعم.
Future<T?> pushPage<T>(BuildContext context, Widget page) =>
    Navigator.of(context).push<T>(MaterialPageRoute(builder: (_) => page));

// ---------------- الحركة ----------------

/// يظهر العنصر بتلاشٍ وانزلاق خفيف، مع تأخير اختياري لعرض القوائم تدريجياً.
class FadeSlideIn extends StatefulWidget {
  final Widget child;
  final int index;
  final Duration delayStep;

  const FadeSlideIn({super.key, required this.child, this.index = 0, this.delayStep = const Duration(milliseconds: 45)});

  @override
  State<FadeSlideIn> createState() => _FadeSlideInState();
}

class _FadeSlideInState extends State<FadeSlideIn> with SingleTickerProviderStateMixin {
  late final AnimationController _c =
      AnimationController(vsync: this, duration: const Duration(milliseconds: 380));
  late final Animation<double> _a = CurvedAnimation(parent: _c, curve: Curves.easeOutCubic);

  @override
  void initState() {
    super.initState();
    final delay = widget.delayStep * widget.index.clamp(0, 10);
    Future.delayed(delay, () {
      if (mounted) _c.forward();
    });
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _a,
      builder: (_, child) => Opacity(
        opacity: _a.value,
        child: Transform.translate(offset: Offset(0, 12 * (1 - _a.value)), child: child),
      ),
      child: widget.child,
    );
  }
}

/// رقم يتحرك من قيمته السابقة إلى الجديدة.
class AnimatedCount extends StatelessWidget {
  final int value;
  final TextStyle? style;

  const AnimatedCount({super.key, required this.value, this.style});

  @override
  Widget build(BuildContext context) {
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: value.toDouble()),
      duration: const Duration(milliseconds: 900),
      curve: Curves.easeOutCubic,
      builder: (_, v, __) => Text(formatPoints(v.round()), style: style, textDirection: TextDirection.ltr),
    );
  }
}

// ---------------- هياكل التحميل ----------------

class Skeleton extends StatefulWidget {
  final double? width;
  final double height;
  final double radius;

  const Skeleton({super.key, this.width, this.height = 14, this.radius = 8});

  @override
  State<Skeleton> createState() => _SkeletonState();
}

class _SkeletonState extends State<Skeleton> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 1100))
    ..repeat(reverse: true);

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return AnimatedBuilder(
      animation: _c,
      builder: (_, __) => Container(
        width: widget.width,
        height: widget.height,
        decoration: BoxDecoration(
          color: Color.lerp(p.surfaceAlt, p.border, _c.value),
          borderRadius: BorderRadius.circular(widget.radius),
        ),
      ),
    );
  }
}

/// قائمة بطاقات وهمية أثناء التحميل.
class SkeletonList extends StatelessWidget {
  final int count;
  final double itemHeight;
  final Widget? header;

  const SkeletonList({super.key, this.count = 6, this.itemHeight = 76, this.header});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return ListView(
      physics: const NeverScrollableScrollPhysics(),
      padding: const EdgeInsets.all(16),
      children: [
        if (header != null) ...[header!, const SizedBox(height: 16)],
        for (var i = 0; i < count; i++)
          Container(
            height: itemHeight,
            margin: const EdgeInsets.only(bottom: 12),
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: p.surface,
              borderRadius: BorderRadius.circular(AppRadius.lg),
              border: Border.all(color: p.border),
            ),
            child: const Row(
              children: [
                Skeleton(width: 44, height: 44, radius: 12),
                SizedBox(width: 12),
                Expanded(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [Skeleton(width: 160), SizedBox(height: 8), Skeleton(width: 100, height: 10)],
                  ),
                ),
                Skeleton(width: 48, height: 18),
              ],
            ),
          ),
      ],
    );
  }
}

// ---------------- حالات الخطأ والفراغ ----------------

class LoadingView extends StatelessWidget {
  const LoadingView({super.key});

  @override
  Widget build(BuildContext context) => const Center(child: CircularProgressIndicator(strokeWidth: 2.5));
}

class ErrorView extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;

  const ErrorView({super.key, required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 72,
              height: 72,
              decoration: BoxDecoration(color: p.dangerSoft, shape: BoxShape.circle),
              child: Icon(LucideIcons.wifiOff, size: 32, color: p.danger),
            ),
            const SizedBox(height: 18),
            Text('تعذّر التحميل', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 6),
            Text(message, textAlign: TextAlign.center, style: TextStyle(color: p.muted)),
            const SizedBox(height: 22),
            FilledButton.tonalIcon(
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
  final Widget? action;

  const EmptyView({super.key, required this.icon, required this.title, this.subtitle, this.action});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 48, horizontal: 32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 76,
            height: 76,
            decoration: BoxDecoration(color: p.surfaceAlt, borderRadius: BorderRadius.circular(24)),
            child: Icon(icon, size: 34, color: p.muted),
          ),
          const SizedBox(height: 16),
          Text(title, textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium),
          if (subtitle != null) ...[
            const SizedBox(height: 6),
            Text(subtitle!, textAlign: TextAlign.center, style: TextStyle(color: p.muted, height: 1.5)),
          ],
          if (action != null) ...[const SizedBox(height: 18), action!],
        ],
      ),
    );
  }
}

/// يبني الواجهة حسب حالة Future: هيكل تحميل، خطأ مع إعادة محاولة، أو البيانات.
class AsyncView<T> extends StatelessWidget {
  final Future<T>? future;
  final VoidCallback onRetry;
  final Widget Function(BuildContext, T) builder;
  final Widget? loading;

  const AsyncView({super.key, required this.future, required this.onRetry, required this.builder, this.loading});

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<T>(
      future: future,
      builder: (context, snap) {
        Widget child;
        if (snap.connectionState != ConnectionState.done) {
          child = KeyedSubtree(key: const ValueKey('loading'), child: loading ?? const SkeletonList());
        } else if (snap.hasError) {
          child = KeyedSubtree(key: const ValueKey('error'), child: ErrorView(message: snap.error.toString(), onRetry: onRetry));
        } else {
          child = KeyedSubtree(key: const ValueKey('data'), child: builder(context, snap.data as T));
        }
        return AnimatedSwitcher(duration: const Duration(milliseconds: 250), child: child);
      },
    );
  }
}

// ---------------- عناصر عامة ----------------

enum Tone { brand, gold, success, warning, danger, info, neutral }

extension ToneColors on Tone {
  (Color fg, Color bg) colors(AppPalette p) => switch (this) {
        Tone.brand => (p.brand, p.brandSoft),
        Tone.gold => (p.gold, p.goldSoft),
        Tone.success => (p.success, p.successSoft),
        Tone.warning => (p.warning, p.warningSoft),
        Tone.danger => (p.danger, p.dangerSoft),
        Tone.info => (p.info, p.infoSoft),
        Tone.neutral => (p.muted, p.surfaceAlt),
      };
}

class StatusChip extends StatelessWidget {
  final String label;
  final Tone tone;
  final IconData? icon;

  const StatusChip({super.key, required this.label, this.tone = Tone.neutral, this.icon});

  @override
  Widget build(BuildContext context) {
    final (fg, bg) = tone.colors(context.palette);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(999)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[Icon(icon, size: 12, color: fg), const SizedBox(width: 4)],
          Text(label, style: TextStyle(color: fg, fontSize: 12, fontWeight: FontWeight.w800)),
        ],
      ),
    );
  }

  factory StatusChip.invoice(String status) => switch (status) {
        'APPROVED' => const StatusChip(label: 'مقبولة', tone: Tone.success, icon: LucideIcons.check),
        'REJECTED' => const StatusChip(label: 'مرفوضة', tone: Tone.danger, icon: LucideIcons.x),
        'RESUBMISSION_REQUESTED' => const StatusChip(label: 'صوّرها من جديد', tone: Tone.warning, icon: LucideIcons.camera),
        'DISPUTED' => const StatusChip(label: 'اعتراض قيد المراجعة', tone: Tone.info),
        'VOIDED' => const StatusChip(label: 'ملغاة', tone: Tone.neutral),
        _ => const StatusChip(label: 'قيد المراجعة', tone: Tone.gold, icon: LucideIcons.clock),
      };

  factory StatusChip.voucher(String status) => switch (status) {
        'ACTIVE' => const StatusChip(label: 'نشطة', tone: Tone.success),
        'USED' => const StatusChip(label: 'مستخدمة', tone: Tone.neutral),
        'CANCELLED' => const StatusChip(label: 'ملغاة', tone: Tone.danger),
        _ => const StatusChip(label: 'منتهية', tone: Tone.danger),
      };
}

class SectionTitle extends StatelessWidget {
  final String title;
  final String? actionLabel;
  final VoidCallback? onAction;

  const SectionTitle(this.title, {super.key, this.actionLabel, this.onAction});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        children: [
          Expanded(child: Text(title, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontSize: 17))),
          if (actionLabel != null)
            TextButton(
              onPressed: onAction,
              style: TextButton.styleFrom(visualDensity: VisualDensity.compact),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [Text(actionLabel!), const SizedBox(width: 2), const Icon(LucideIcons.chevronLeft, size: 16)],
              ),
            ),
        ],
      ),
    );
  }
}

/// بطاقة بحدود ناعمة وزوايا موحّدة، قابلة للضغط.
class AppCard extends StatelessWidget {
  final Widget child;
  final EdgeInsetsGeometry padding;
  final VoidCallback? onTap;
  final Color? color;

  const AppCard({super.key, required this.child, this.padding = const EdgeInsets.all(16), this.onTap, this.color});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Material(
      color: color ?? p.surface,
      borderRadius: BorderRadius.circular(AppRadius.lg),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        child: Container(
          padding: padding,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadius.lg),
            border: Border.all(color: p.border),
          ),
          child: child,
        ),
      ),
    );
  }
}

/// مربع أيقونة ملوّن.
class IconBadge extends StatelessWidget {
  final IconData icon;
  final Tone tone;
  final double size;

  const IconBadge(this.icon, {super.key, this.tone = Tone.brand, this.size = 44});

  @override
  Widget build(BuildContext context) {
    final (fg, bg) = tone.colors(context.palette);
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(size * 0.32)),
      child: Icon(icon, color: fg, size: size * 0.48),
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
    final p = context.palette;
    final url = AppConstants.resolveImageUrl(logoUrl);
    final fallback = Center(
      child: Text(
        name.isNotEmpty ? name.characters.first.toUpperCase() : '?',
        style: TextStyle(fontSize: size * 0.4, fontWeight: FontWeight.w800, color: p.brand),
      ),
    );
    return Container(
      width: size,
      height: size,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: p.brandSoft,
        borderRadius: BorderRadius.circular(size * 0.3),
        border: Border.all(color: p.border),
      ),
      child: url == null
          ? fallback
          : Image.network(url, fit: BoxFit.cover, errorBuilder: (_, __, ___) => fallback),
    );
  }
}

/// سطر "عنوان: قيمة" داخل البطاقات.
class InfoRow extends StatelessWidget {
  final String label;
  final String value;
  final bool ltr;

  const InfoRow(this.label, this.value, {super.key, this.ltr = false});

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 7),
        child: Row(
          children: [
            Text(label, style: TextStyle(color: context.palette.muted)),
            const SizedBox(width: 12),
            Expanded(
              child: Text(value,
                  // القيمة دائماً في الطرف الأيسر من السطر (عكس العنوان في الواجهة العربية)
                  textAlign: TextAlign.left,
                  textDirection: ltr ? TextDirection.ltr : null,
                  style: const TextStyle(fontWeight: FontWeight.w700)),
            ),
          ],
        ),
      );
}
