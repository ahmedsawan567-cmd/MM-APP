import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'core/constants/app_constants.dart';
import 'core/services/server_config.dart';
import 'core/services/session.dart';
import 'core/theme/app_theme.dart';
import 'core/theme/theme_controller.dart';
import 'screens/auth/login_screen.dart';
import 'screens/main_screen.dart';
import 'screens/onboarding_screen.dart';
import 'screens/splash_screen.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // رخصة خط تجوال تظهر ضمن تراخيص التطبيق
  LicenseRegistry.addLicense(() async* {
    yield LicenseEntryWithLineBreaks(['Tajawal'], await rootBundle.loadString('assets/fonts/OFL.txt'));
  });
  await initializeDateFormatting('ar');
  await ThemeController.instance.load();
  await ServerConfig.load();
  var onboardingSeen = true;
  try {
    onboardingSeen = (await SharedPreferences.getInstance()).getBool(OnboardingScreen.seenKey) ?? false;
  } catch (_) {}
  // استعادة جلسة الدخول المحفوظة (إن وجدت) بالخلفية بينما تظهر شاشة البداية.
  Session.instance.restore();
  runApp(AlmaraaMallApp(onboardingSeen: onboardingSeen));
}

class AlmaraaMallApp extends StatefulWidget {
  final bool onboardingSeen;
  const AlmaraaMallApp({super.key, required this.onboardingSeen});

  @override
  State<AlmaraaMallApp> createState() => _AlmaraaMallAppState();
}

class _AlmaraaMallAppState extends State<AlmaraaMallApp> {
  late bool _onboardingSeen = widget.onboardingSeen;

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<ThemeMode>(
      valueListenable: ThemeController.instance,
      builder: (context, mode, _) => MaterialApp(
        title: AppConstants.appNameAr,
        debugShowCheckedModeBanner: false,
        theme: AppTheme.lightTheme,
        darkTheme: AppTheme.darkTheme,
        themeMode: mode,
        // الدعم الكامل للغة العربية واتجاه الكتابة من اليمين إلى اليسار (RTL)
        locale: const Locale('ar', 'LY'),
        supportedLocales: const [Locale('ar', 'LY'), Locale('en', 'US')],
        localizationsDelegates: const [
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: ListenableBuilder(
          listenable: Session.instance,
          builder: (context, _) {
            final session = Session.instance;
            final Widget page;
            if (session.isRestoring) {
              page = const SplashScreen();
            } else if (session.isLoggedIn) {
              // مفتاح مختلف لكل مستخدم حتى تُبنى الشاشات من جديد بعد تبديل الحساب.
              page = MainScreen(key: ValueKey(session.user!.id));
            } else if (!_onboardingSeen) {
              page = OnboardingScreen(onDone: () => setState(() => _onboardingSeen = true));
            } else {
              page = const LoginScreen();
            }
            return AnimatedSwitcher(duration: const Duration(milliseconds: 350), child: page);
          },
        ),
      ),
    );
  }
}
