import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/services/loyalty_api.dart';
import '../core/theme/app_theme.dart';
import '../widgets/common.dart';

Color _hex(String? v, Color fallback) {
  if (v == null || !RegExp(r'^#[0-9a-fA-F]{6}$').hasMatch(v)) return fallback;
  return Color(int.parse('FF${v.substring(1)}', radix: 16));
}

/// عضويتي: مستويات العضوية ومزايا كل مستوى، وتقدّم العميل نحو المستوى التالي.
class MembershipTiersScreen extends StatefulWidget {
  const MembershipTiersScreen({super.key});

  @override
  State<MembershipTiersScreen> createState() => _MembershipTiersScreenState();
}

class _MembershipTiersScreenState extends State<MembershipTiersScreen> {
  Future<Map<String, dynamic>>? _future;

  @override
  void initState() {
    super.initState();
    _load();
  }

  void _load() => setState(() => _future = LoyaltyApi.getTiers());

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('عضويتي')),
      body: AsyncView<Map<String, dynamic>>(
        future: _future,
        onRetry: _load,
        builder: (context, data) {
          final tiers = (data['tiers'] as List? ?? const []).cast<Map<String, dynamic>>();
          final current = data['current'] as String?;
          final progress = data['progress'] as Map<String, dynamic>?;
          final locked = data['locked'] == true;
          final currentTier = tiers.where((t) => t['code'] == current).firstOrNull;
          return RefreshIndicator(
            onRefresh: () async {
              _load();
              await _future;
            },
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
              children: [
                if (currentTier != null) _CurrentCard(tier: currentTier, progress: progress, locked: locked),
                const SizedBox(height: 20),
                const Text('مستويات العضوية', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
                const SizedBox(height: 10),
                for (var i = 0; i < tiers.length; i++)
                  FadeSlideIn(
                    index: i,
                    child: Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: _TierTile(tier: tiers[i], isCurrent: tiers[i]['code'] == current),
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _CurrentCard extends StatelessWidget {
  final Map<String, dynamic> tier;
  final Map<String, dynamic>? progress;
  final bool locked;
  const _CurrentCard({required this.tier, required this.progress, required this.locked});

  @override
  Widget build(BuildContext context) {
    final color = _hex(tier['color'] as String?, Brand.purple);
    final next = progress?['nextName'] as String?;
    final percent = ((progress?['percent'] as num?) ?? 0).toDouble();
    final pointsTo = ((progress?['pointsToNext'] as num?) ?? 0).toInt();
    final spendTo = ((progress?['spendToNext'] as num?) ?? 0).toDouble();
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.xl),
        gradient: LinearGradient(colors: [color, Color.lerp(color, Colors.black, 0.35)!], begin: Alignment.topRight, end: Alignment.bottomLeft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(children: [
            const Icon(LucideIcons.crown, color: Colors.white, size: 22),
            const SizedBox(width: 8),
            Text('عضوية ${tier['name']}', style: const TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.w800)),
          ]),
          const SizedBox(height: 14),
          if (locked)
            const Text('عضويتك مميزة ومثبّتة من إدارة المول.', style: TextStyle(color: Colors.white))
          else if (next == null)
            const Text('أنت في أعلى مستوى عضوية 🎉', style: TextStyle(color: Colors.white, fontWeight: FontWeight.w700))
          else ...[
            Text(
              [
                if (pointsTo > 0) 'باقي ${formatPoints(pointsTo)} نقطة',
                if (spendTo > 0) '${pointsTo > 0 ? 'و' : 'باقي '}${formatMoney(spendTo)} مشتريات',
              ].join(' ') + (pointsTo > 0 || spendTo > 0 ? ' للوصول إلى عضوية $next' : 'ستُرقّى إلى $next مع فاتورتك القادمة'),
              style: const TextStyle(color: Colors.white, height: 1.5),
            ),
            const SizedBox(height: 10),
            ClipRRect(
              borderRadius: BorderRadius.circular(99),
              child: LinearProgressIndicator(value: percent / 100, minHeight: 9, backgroundColor: Colors.white24,
                  valueColor: const AlwaysStoppedAnimation(Colors.white)),
            ),
          ],
        ],
      ),
    );
  }
}

class _TierTile extends StatelessWidget {
  final Map<String, dynamic> tier;
  final bool isCurrent;
  const _TierTile({required this.tier, required this.isCurrent});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    final color = _hex(tier['color'] as String?, Brand.purple);
    final multiplier = ((tier['multiplier'] as num?) ?? 1).toDouble();
    final benefits = (tier['benefits'] as List? ?? const []).map((e) => '$e').toList();
    return AppCard(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 40, height: 40,
                decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(12)),
                child: Icon(LucideIcons.crown, color: color, size: 20),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('${tier['name']}', style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
                    Text('${tier['condition']}', style: TextStyle(fontSize: 12, color: p.muted)),
                  ],
                ),
              ),
              if (isCurrent) const StatusChip(label: 'عضويتك', tone: Tone.success),
            ],
          ),
          if (multiplier > 1) ...[
            const SizedBox(height: 10),
            StatusChip(label: 'نقاط إضافية ${((multiplier - 1) * 100).round()}% على كل فاتورة', tone: Tone.gold, icon: LucideIcons.sparkles),
          ],
          if (benefits.isNotEmpty) ...[
            const SizedBox(height: 10),
            for (final b in benefits)
              Padding(
                padding: const EdgeInsets.only(bottom: 4),
                child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Padding(padding: const EdgeInsets.only(top: 2), child: Icon(LucideIcons.check, size: 16, color: color)),
                  const SizedBox(width: 8),
                  Expanded(child: Text(b, style: const TextStyle(height: 1.4))),
                ]),
              ),
          ],
        ],
      ),
    );
  }
}
