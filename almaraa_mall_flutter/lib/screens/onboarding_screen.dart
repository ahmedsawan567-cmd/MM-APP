import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../core/theme/app_theme.dart';

/// تعريف سريع بالتطبيق عند أول تشغيل.
class OnboardingScreen extends StatefulWidget {
  static const seenKey = 'onboarding_seen';
  final VoidCallback onDone;

  const OnboardingScreen({super.key, required this.onDone});

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  final _controller = PageController();
  int _page = 0;

  static const _slides = [
    (LucideIcons.shoppingBag, 'تسوّق من متاجر المول', 'كل فاتورة شراء من متاجر المرأة مول تقرّبك من مكافأة جديدة.'),
    (LucideIcons.scanLine, 'صوّر الفاتورة', 'التقط صورة الفاتورة من التطبيق، ويقرأها النظام تلقائياً ويضيف نقاطك.'),
    (LucideIcons.gift, 'استبدل نقاطك', 'حوّل نقاطك إلى قسائم خصم وهدايا من متاجرك المفضلة.'),
  ];

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _finish() async {
    try {
      (await SharedPreferences.getInstance()).setBool(OnboardingScreen.seenKey, true);
    } catch (_) {}
    widget.onDone();
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final last = _page == _slides.length - 1;
    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            Align(
              alignment: AlignmentDirectional.centerEnd,
              child: TextButton(onPressed: _finish, child: Text('تخطي', style: TextStyle(color: p.muted))),
            ),
            Expanded(
              child: PageView.builder(
                controller: _controller,
                itemCount: _slides.length,
                onPageChanged: (i) => setState(() => _page = i),
                itemBuilder: (_, i) {
                  final (icon, title, body) = _slides[i];
                  return Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 32),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Container(
                          width: 180,
                          height: 180,
                          decoration: BoxDecoration(
                            gradient: const LinearGradient(
                              colors: [Color(0xFF064E3B), Color(0xFF047857)],
                              begin: Alignment.topRight,
                              end: Alignment.bottomLeft,
                            ),
                            borderRadius: BorderRadius.circular(56),
                            boxShadow: [
                              BoxShadow(color: const Color(0xFF064E3B).withValues(alpha: 0.3), blurRadius: 40, offset: const Offset(0, 16)),
                            ],
                          ),
                          child: Icon(icon, size: 76, color: const Color(0xFFFBBF24)),
                        ),
                        const SizedBox(height: 48),
                        Text(title, textAlign: TextAlign.center, style: Theme.of(context).textTheme.headlineSmall),
                        const SizedBox(height: 12),
                        Text(body, textAlign: TextAlign.center, style: TextStyle(color: p.muted, fontSize: 16, height: 1.6)),
                      ],
                    ),
                  );
                },
              ),
            ),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(
                _slides.length,
                (i) => AnimatedContainer(
                  duration: const Duration(milliseconds: 250),
                  margin: const EdgeInsets.symmetric(horizontal: 4),
                  width: i == _page ? 24 : 8,
                  height: 8,
                  decoration: BoxDecoration(
                    color: i == _page ? p.brand : p.border,
                    borderRadius: BorderRadius.circular(8),
                  ),
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(24, 28, 24, 24),
              child: SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: last
                      ? _finish
                      : () => _controller.nextPage(duration: const Duration(milliseconds: 350), curve: Curves.easeOutCubic),
                  child: Text(last ? 'ابدأ الآن' : 'التالي'),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
