import 'package:flutter/material.dart';
import 'package:intl/intl.dart' show DateFormat;
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../core/services/session.dart';
import '../../core/theme/app_theme.dart';
import '../../widgets/common.dart';
import '../auth/auth_widgets.dart';

/// تعديل العميل لبياناته الشخصية. رقم الهاتف هو هوية الحساب ولا يُعدَّل إلا من خدمة العملاء.
class EditProfileScreen extends StatefulWidget {
  const EditProfileScreen({super.key});

  @override
  State<EditProfileScreen> createState() => _EditProfileScreenState();
}

class _EditProfileScreenState extends State<EditProfileScreen> {
  final _formKey = GlobalKey<FormState>();
  late final _user = Session.instance.user!;
  late final _name = TextEditingController(text: _user.fullName);
  late final _email = TextEditingController(text: _user.email ?? '');
  late DateTime? _birthDate = _user.birthDate;
  late String? _gender = _user.gender;
  bool _saving = false;

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    super.dispose();
  }

  Future<void> _pickBirthDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _birthDate ?? DateTime(now.year - 25),
      firstDate: DateTime(1920),
      lastDate: DateTime(now.year - 5, now.month, now.day),
      helpText: 'تاريخ الميلاد',
    );
    if (picked != null) setState(() => _birthDate = picked);
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    try {
      await Session.instance.updateProfile(
        fullName: _name.text.trim(),
        email: _email.text.trim().isEmpty ? null : _email.text.trim(),
        birthDate: _birthDate,
        gender: _gender,
      );
      if (!mounted) return;
      showMessage(context, 'تم حفظ بياناتك');
      Navigator.pop(context);
    } catch (e) {
      if (mounted) showMessage(context, e.toString(), error: true);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Scaffold(
      appBar: AppBar(title: const Text('بياناتي')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 28),
          children: [
            TextFormField(
              controller: _name,
              textInputAction: TextInputAction.next,
              decoration: authInputDecoration('الاسم الكامل', LucideIcons.user),
              validator: (v) => (v == null || v.trim().length < 3) ? 'أدخل اسمك الكامل' : null,
            ),
            const SizedBox(height: 14),
            TextFormField(
              initialValue: _user.phoneNumber,
              enabled: false,
              textDirection: TextDirection.ltr,
              decoration: authInputDecoration('رقم الهاتف', LucideIcons.phone).copyWith(
                helperText: 'لتغيير رقم الهاتف راجع خدمة العملاء في المول',
              ),
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              textDirection: TextDirection.ltr,
              decoration: authInputDecoration('البريد الإلكتروني (اختياري)', LucideIcons.mail),
              validator: (v) {
                final t = (v ?? '').trim();
                if (t.isEmpty) return null;
                return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(t) ? null : 'البريد الإلكتروني غير صالح';
              },
            ),
            const SizedBox(height: 14),
            InkWell(
              borderRadius: BorderRadius.circular(AppRadius.md),
              onTap: _pickBirthDate,
              child: InputDecorator(
                decoration: authInputDecoration('تاريخ الميلاد (اختياري)', LucideIcons.cake).copyWith(
                  suffixIcon: _birthDate == null
                      ? null
                      : IconButton(icon: const Icon(LucideIcons.x, size: 18), onPressed: () => setState(() => _birthDate = null)),
                ),
                child: Text(
                  _birthDate == null ? 'اختر التاريخ' : DateFormat('d MMMM yyyy', 'ar').format(_birthDate!),
                  style: TextStyle(color: _birthDate == null ? p.muted : p.ink),
                ),
              ),
            ),
            const SizedBox(height: 18),
            Text('الجنس', style: TextStyle(color: p.muted, fontWeight: FontWeight.w700, fontSize: 13)),
            const SizedBox(height: 8),
            SegmentedButton<String?>(
              showSelectedIcon: false,
              style: SegmentedButton.styleFrom(minimumSize: const Size(0, 46)),
              segments: const [
                ButtonSegment(value: 'F', label: Text('أنثى')),
                ButtonSegment(value: 'M', label: Text('ذكر')),
                ButtonSegment(value: null, label: Text('عدم التحديد')),
              ],
              selected: {_gender},
              onSelectionChanged: (s) => setState(() => _gender = s.first),
            ),
            const SizedBox(height: 28),
            LoadingButton(loading: _saving, label: 'حفظ', onPressed: _save),
          ],
        ),
      ),
    );
  }
}
