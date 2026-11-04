import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/services/server_config.dart';
import '../core/theme/app_theme.dart';
import 'common.dart';

/// نافذة ضبط عنوان خادم المول مع فحص الاتصال قبل الحفظ.
Future<void> showServerSettings(BuildContext context) => showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (_) => const _ServerSettingsSheet(),
    );

class _ServerSettingsSheet extends StatefulWidget {
  const _ServerSettingsSheet();

  @override
  State<_ServerSettingsSheet> createState() => _ServerSettingsSheetState();
}

enum _Status { idle, testing, ok, failed }

class _ServerSettingsSheetState extends State<_ServerSettingsSheet> {
  late final _controller = TextEditingController(text: _display(ServerConfig.apiBaseUrl));
  _Status _status = _Status.idle;
  String? _error;

  static String _display(String api) => api.replaceFirst(RegExp(r'/api$'), '');

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final url = ServerConfig.normalize(_controller.text);
    if (url == null) {
      setState(() => _error = 'اكتب عنواناً صحيحاً، مثل 192.168.1.10');
      return;
    }
    setState(() {
      _error = null;
      _status = _Status.testing;
    });
    final ok = await ServerConfig.test(url);
    if (!mounted) return;
    if (!ok) {
      setState(() => _status = _Status.failed);
      return;
    }
    await ServerConfig.save(url);
    if (!mounted) return;
    setState(() => _status = _Status.ok);
    Navigator.pop(context);
    showMessage(context, 'تم الاتصال بالخادم وحفظ العنوان');
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final busy = _status == _Status.testing;
    return Padding(
      padding: EdgeInsets.fromLTRB(24, 0, 24, 24 + MediaQuery.of(context).viewInsets.bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Center(child: IconBadge(LucideIcons.server, size: 56)),
          const SizedBox(height: 14),
          Text('إعدادات الخادم', textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 6),
          Text('اكتب عنوان جهاز الخادم في المول (رقم IP أو رابط الموقع).',
              textAlign: TextAlign.center, style: TextStyle(color: p.muted)),
          const SizedBox(height: 18),
          TextField(
            controller: _controller,
            keyboardType: TextInputType.url,
            textDirection: TextDirection.ltr,
            autocorrect: false,
            onSubmitted: (_) => busy ? null : _save(),
            onChanged: (_) {
              if (_status != _Status.idle || _error != null) {
                setState(() {
                  _status = _Status.idle;
                  _error = null;
                });
              }
            },
            decoration: InputDecoration(
              labelText: 'عنوان الخادم',
              hintText: '192.168.1.10',
              prefixIcon: const Icon(LucideIcons.globe, size: 20),
              errorText: _error,
            ),
          ),
          if (_status == _Status.failed) ...[
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: p.danger.withValues(alpha: 0.08),
                borderRadius: BorderRadius.circular(AppRadius.md),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(LucideIcons.wifiOff, color: p.danger, size: 18),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'لم يستجب الخادم على هذا العنوان. تأكد أن الهاتف على نفس الشبكة، وأن الخادم يعمل، وأن المنفذ 5000 مسموح في جدار الحماية.',
                      style: TextStyle(color: p.danger, fontSize: 13, height: 1.5),
                    ),
                  ),
                ],
              ),
            ),
          ],
          const SizedBox(height: 20),
          ElevatedButton.icon(
            onPressed: busy ? null : _save,
            icon: busy
                ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Icon(LucideIcons.plugZap, size: 18),
            label: Text(busy ? 'جارٍ فحص الاتصال...' : 'فحص وحفظ'),
          ),
          if (ServerConfig.isCustom) ...[
            const SizedBox(height: 8),
            TextButton(
              onPressed: busy
                  ? null
                  : () async {
                      await ServerConfig.reset();
                      if (mounted) _controller.text = _display(ServerConfig.apiBaseUrl);
                    },
              child: const Text('استعادة العنوان الافتراضي'),
            ),
          ],
        ],
      ),
    );
  }
}
