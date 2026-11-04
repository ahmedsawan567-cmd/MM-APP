import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../core/theme/app_theme.dart';
import '../../core/services/session.dart';
import '../../widgets/common.dart';
import '../../widgets/server_settings_sheet.dart';
import 'auth_widgets.dart';
import 'register_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _phone = TextEditingController();
  final _password = TextEditingController();
  bool _loading = false;
  bool _obscure = true;

  @override
  void dispose() {
    _phone.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _loading = true);
    try {
      await Session.instance.login(_phone.text.trim(), _password.text);
    } catch (e) {
      if (mounted) showMessage(context, e.toString(), error: true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return AuthScaffold(
      title: 'مرحباً بعودتك',
      subtitle: 'سجّل دخولك لمتابعة نقاطك ومكافآتك',
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextFormField(
              controller: _phone,
              keyboardType: TextInputType.phone,
              textDirection: TextDirection.ltr,
              textInputAction: TextInputAction.next,
              decoration: authInputDecoration('رقم الهاتف', LucideIcons.phone, hint: '09XXXXXXXX'),
              validator: validatePhone,
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _password,
              obscureText: _obscure,
              textInputAction: TextInputAction.done,
              onFieldSubmitted: (_) => _submit(),
              decoration: authInputDecoration('كلمة المرور', LucideIcons.lock).copyWith(
                suffixIcon: IconButton(
                  icon: Icon(_obscure ? LucideIcons.eye : LucideIcons.eyeOff, size: 20),
                  onPressed: () => setState(() => _obscure = !_obscure),
                ),
              ),
              validator: (v) => (v == null || v.isEmpty) ? 'أدخل كلمة المرور' : null,
            ),
            const SizedBox(height: 24),
            LoadingButton(loading: _loading, label: 'تسجيل الدخول', onPressed: _submit),
            const SizedBox(height: 16),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text('ليس لديك حساب؟', style: TextStyle(color: context.palette.muted)),
                TextButton(
                  onPressed: _loading
                      ? null
                      : () => pushPage(context, const RegisterScreen()),
                  child: Text('أنشئ حساباً جديداً',
                      style: TextStyle(color: context.palette.brand, fontWeight: FontWeight.w800)),
                ),
              ],
            ),
            const SizedBox(height: 4),
            Center(
              child: TextButton.icon(
                onPressed: _loading ? null : () => showServerSettings(context),
                icon: Icon(LucideIcons.server, size: 16, color: context.palette.muted),
                label: Text('إعدادات الخادم', style: TextStyle(color: context.palette.muted, fontSize: 13)),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
