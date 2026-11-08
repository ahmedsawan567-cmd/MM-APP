import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../core/constants/app_constants.dart';
import '../../core/theme/app_theme.dart';

String? validatePhone(String? v) {
  final value = (v ?? '').trim().replaceAll(' ', '');
  if (value.isEmpty) return 'أدخل رقم الهاتف';
  if (!RegExp(r'^\+?[0-9]{9,15}$').hasMatch(value)) return 'رقم الهاتف غير صالح';
  return null;
}

/// حقل إدخال بأيقونة (الألوان والحدود من سمة التطبيق).
InputDecoration authInputDecoration(String label, IconData icon, {String? hint}) =>
    InputDecoration(labelText: label, hintText: hint, prefixIcon: Icon(icon, size: 20));

/// الإطار المشترك لشاشتي الدخول والتسجيل: ترويسة بهوية المول ثم النموذج.
class AuthScaffold extends StatelessWidget {
  final String title;
  final String subtitle;
  final Widget child;
  final bool showBack;

  const AuthScaffold({super.key, required this.title, required this.subtitle, required this.child, this.showBack = false});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final top = MediaQuery.of(context).padding.top;
    return Scaffold(
      body: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            ClipRRect(
              borderRadius: const BorderRadius.vertical(bottom: Radius.circular(36)),
              child: Container(
                padding: EdgeInsets.fromLTRB(24, top + 12, 24, 36),
                decoration: const BoxDecoration(gradient: Brand.gradient),
                child: Stack(
                  clipBehavior: Clip.none,
                  children: [
                    Positioned(left: -60, top: -40, child: _glow(180, Brand.magenta, 0.35)),
                    Positioned(right: -40, bottom: -70, child: _glow(160, Brand.glow, 0.18)),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        SizedBox(
                          height: 44,
                          child: showBack
                              ? IconButton(
                                  icon: const Icon(LucideIcons.arrowRight, color: Colors.white),
                                  onPressed: () => Navigator.of(context).pop(),
                                )
                              : null,
                        ),
                        const SizedBox(height: 8),
                        Container(
                          width: 68,
                          height: 68,
                          padding: const EdgeInsets.all(10),
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: BorderRadius.circular(22),
                            boxShadow: [BoxShadow(color: Brand.night.withValues(alpha: 0.35), blurRadius: 18, offset: const Offset(0, 8))],
                          ),
                          child: Image.asset(Brand.mark, semanticLabel: 'المرأة مول'),
                        ),
                        const SizedBox(height: 18),
                        const Text(AppConstants.appNameAr,
                            style: TextStyle(color: Colors.white, fontSize: 28, fontWeight: FontWeight.w700)),
                        const SizedBox(height: 4),
                        Text(AppConstants.taglineAr, style: TextStyle(color: Colors.white.withValues(alpha: 0.75), height: 1.5)),
                      ],
                    ),
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(24, 28, 24, 32),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(title, style: Theme.of(context).textTheme.headlineSmall),
                  const SizedBox(height: 6),
                  Text(subtitle, style: TextStyle(color: p.muted, height: 1.5)),
                  const SizedBox(height: 28),
                  child,
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  static Widget _glow(double size, Color color, double alpha) => Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          gradient: RadialGradient(colors: [color.withValues(alpha: alpha), color.withValues(alpha: 0)]),
        ),
      );
}

/// زر رئيسي مع مؤشر تحميل.
class LoadingButton extends StatelessWidget {
  final bool loading;
  final String label;
  final VoidCallback onPressed;

  const LoadingButton({super.key, required this.loading, required this.label, required this.onPressed});

  @override
  Widget build(BuildContext context) {
    return ElevatedButton(
      onPressed: loading ? null : onPressed,
      child: AnimatedSwitcher(
        duration: const Duration(milliseconds: 200),
        child: loading
            ? const SizedBox(key: ValueKey('l'), height: 22, width: 22, child: CircularProgressIndicator(strokeWidth: 2.5))
            : Text(label, key: const ValueKey('t')),
      ),
    );
  }
}
