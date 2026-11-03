import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'core/constants/app_constants.dart';
import 'core/services/session.dart';
import 'core/theme/app_theme.dart';
import 'screens/auth/login_screen.dart';
import 'screens/main_screen.dart';
import 'widgets/common.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting('ar');
  // استعادة جلسة الدخول المحفوظة (إن وجدت) بالخلفية.
  Session.instance.restore();
  runApp(const AlmaraaMallApp());
}

class AlmaraaMallApp extends StatelessWidget {
  const AlmaraaMallApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: AppConstants.appNameAr,
      debugShowCheckedModeBanner: false,
      theme: AppTheme.lightTheme,
      // الدعم الكامل للغة العربية واتجاه الكتابة من اليمين إلى اليسار (RTL)
      locale: const Locale('ar', 'LY'),
      supportedLocales: const [
        Locale('ar', 'LY'),
        Locale('en', 'US'),
      ],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: ListenableBuilder(
        listenable: Session.instance,
        builder: (context, _) {
          final session = Session.instance;
          if (session.isRestoring) return const Scaffold(body: LoadingView());
          // مفتاح مختلف لكل مستخدم حتى تُبنى الشاشات من جديد بعد تبديل الحساب.
          return session.isLoggedIn
              ? MainScreen(key: ValueKey(session.user!.id))
              : const LoginScreen();
        },
      ),
    );
  }
}
