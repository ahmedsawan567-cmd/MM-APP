import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';

/// شاشة البداية أثناء استعادة الجلسة.
class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF064E3B),
      body: Center(
        child: TweenAnimationBuilder<double>(
          tween: Tween(begin: 0.85, end: 1),
          duration: const Duration(milliseconds: 700),
          curve: Curves.easeOutBack,
          builder: (_, v, child) => Opacity(opacity: ((v - 0.85) / 0.15).clamp(0, 1), child: Transform.scale(scale: v, child: child)),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 92,
                height: 92,
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(30),
                  border: Border.all(color: Colors.white.withValues(alpha: 0.15)),
                ),
                child: const Icon(LucideIcons.crown, size: 46, color: Color(0xFFFBBF24)),
              ),
              const SizedBox(height: 20),
              const Text(AppConstants.appNameAr,
                  style: TextStyle(color: Colors.white, fontSize: 28, fontWeight: FontWeight.w800)),
              const SizedBox(height: 6),
              Text('برنامج الولاء', style: TextStyle(color: Colors.white.withValues(alpha: 0.7))),
              const SizedBox(height: 36),
              const SizedBox(
                width: 22,
                height: 22,
                child: CircularProgressIndicator(strokeWidth: 2, color: Color(0xFFFBBF24)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
