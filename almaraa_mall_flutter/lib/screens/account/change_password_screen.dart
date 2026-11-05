import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../core/services/loyalty_api.dart';
import '../../widgets/common.dart';
import '../auth/auth_widgets.dart';

class ChangePasswordScreen extends StatefulWidget {
  const ChangePasswordScreen({super.key});

  @override
  State<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends State<ChangePasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _current = TextEditingController();
  final _new = TextEditingController();
  final _confirm = TextEditingController();
  bool _obscure = true;
  bool _saving = false;

  @override
  void dispose() {
    _current.dispose();
    _new.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    try {
      await LoyaltyApi.changePassword(_current.text, _new.text);
      if (!mounted) return;
      showMessage(context, 'تم تغيير كلمة المرور');
      Navigator.pop(context);
    } catch (e) {
      if (mounted) showMessage(context, e.toString(), error: true);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  InputDecoration _decoration(String label) => authInputDecoration(label, LucideIcons.lock).copyWith(
        suffixIcon: IconButton(
          icon: Icon(_obscure ? LucideIcons.eye : LucideIcons.eyeOff, size: 20),
          onPressed: () => setState(() => _obscure = !_obscure),
        ),
      );

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('تغيير كلمة المرور')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 28),
          children: [
            TextFormField(
              controller: _current,
              obscureText: _obscure,
              decoration: _decoration('كلمة المرور الحالية'),
              validator: (v) => (v == null || v.isEmpty) ? 'أدخل كلمة المرور الحالية' : null,
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _new,
              obscureText: _obscure,
              decoration: _decoration('كلمة المرور الجديدة'),
              validator: (v) {
                if (v == null || v.length < 8) return '8 أحرف على الأقل';
                if (v == _current.text) return 'اختر كلمة مرور مختلفة عن الحالية';
                return null;
              },
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _confirm,
              obscureText: _obscure,
              decoration: _decoration('تأكيد كلمة المرور الجديدة'),
              validator: (v) => v != _new.text ? 'كلمتا المرور غير متطابقتين' : null,
            ),
            const SizedBox(height: 28),
            LoadingButton(loading: _saving, label: 'حفظ', onPressed: _save),
          ],
        ),
      ),
    );
  }
}
