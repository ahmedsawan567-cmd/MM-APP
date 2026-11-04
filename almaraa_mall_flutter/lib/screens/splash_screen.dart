import 'package:flutter/material.dart';

import '../core/theme/app_theme.dart';

/// شاشة البداية أثناء استعادة الجلسة (مطابقة لشاشة الإقلاع الأصلية: خلفية بيضاء وشعار المول).
class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      body: Center(
        child: TweenAnimationBuilder<double>(
          tween: Tween(begin: 0.9, end: 1),
          duration: const Duration(milliseconds: 700),
          curve: Curves.easeOutBack,
          builder: (_, v, child) => Opacity(opacity: ((v - 0.9) / 0.1).clamp(0, 1), child: Transform.scale(scale: v, child: child)),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Image.asset(Brand.logo, width: 190, semanticLabel: 'المرأة مول'),
              const SizedBox(height: 40),
              const SizedBox(
                width: 22,
                height: 22,
                child: CircularProgressIndicator(strokeWidth: 2, color: Brand.purple),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
