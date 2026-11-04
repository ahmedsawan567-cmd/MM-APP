import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/services/loyalty_api.dart';
import '../core/theme/app_theme.dart';
import '../models/mall.dart';
import '../widgets/common.dart';

class StoresTab extends StatefulWidget {
  const StoresTab({super.key});

  @override
  State<StoresTab> createState() => _StoresTabState();
}

class _StoresTabState extends State<StoresTab> {
  Future<List<Store>>? _future;
  final _search = TextEditingController();
  Timer? _debounce;
  String? _category;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _load() => setState(() => _future = LoyaltyApi.getStores());

  void _onSearchChanged(String _) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 220), () => setState(() {}));
  }

  List<Store> _filter(List<Store> stores) {
    final q = _search.text.trim().toLowerCase();
    return stores.where((s) {
      final matchesCategory = _category == null || s.category == _category;
      final matchesQuery =
          q.isEmpty || s.nameAr.toLowerCase().contains(q) || s.nameEn.toLowerCase().contains(q) || s.category.toLowerCase().contains(q);
      return matchesCategory && matchesQuery;
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return Scaffold(
      appBar: AppBar(title: const Text('دليل المتاجر')),
      body: AsyncView<List<Store>>(
        future: _future,
        onRetry: _load,
        builder: (context, stores) {
          final categories = stores.map((s) => s.category).where((c) => c.isNotEmpty).toSet().toList()..sort();
          final filtered = _filter(stores);
          return Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 4, 16, 10),
                child: TextField(
                  controller: _search,
                  onChanged: _onSearchChanged,
                  textInputAction: TextInputAction.search,
                  decoration: InputDecoration(
                    hintText: 'ابحث عن متجر أو فئة',
                    prefixIcon: const Icon(LucideIcons.search, size: 20),
                    suffixIcon: _search.text.isEmpty
                        ? null
                        : IconButton(
                            icon: const Icon(LucideIcons.x, size: 18),
                            onPressed: () => setState(_search.clear),
                          ),
                    contentPadding: const EdgeInsets.symmetric(vertical: 12),
                  ),
                ),
              ),
              if (categories.length > 1)
                SizedBox(
                  height: 42,
                  child: ListView(
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    children: [
                      _chip('الكل', null),
                      ...categories.map((c) => _chip(c, c)),
                    ],
                  ),
                ),
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 10, 20, 2),
                child: Row(
                  children: [
                    Text('${filtered.length} متجر', style: TextStyle(color: p.muted, fontSize: 13, fontWeight: FontWeight.w600)),
                  ],
                ),
              ),
              Expanded(
                child: RefreshIndicator(
                  onRefresh: () async {
                    _load();
                    await _future;
                  },
                  child: filtered.isEmpty
                      ? ListView(children: const [
                          EmptyView(icon: LucideIcons.searchX, title: 'لا توجد متاجر مطابقة', subtitle: 'جرّب كلمة بحث أخرى أو فئة مختلفة'),
                        ])
                      : ListView.separated(
                          padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                          itemCount: filtered.length,
                          separatorBuilder: (_, __) => const SizedBox(height: 10),
                          itemBuilder: (_, i) => FadeSlideIn(index: i, child: _StoreCard(store: filtered[i])),
                        ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _chip(String label, String? value) {
    final selected = _category == value;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4),
      child: ChoiceChip(
        label: Text(label),
        selected: selected,
        onSelected: (_) => setState(() => _category = value),
      ),
    );
  }
}

class _StoreCard extends StatelessWidget {
  final Store store;
  const _StoreCard({required this.store});

  @override
  Widget build(BuildContext context) {
    final p = context.palette;
    return AppCard(
      padding: const EdgeInsets.all(14),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          StoreLogo(name: store.nameAr, logoUrl: store.logoUrl, size: 58),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(store.nameAr,
                    maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800, height: 1.3)),
                if (store.nameEn.isNotEmpty && store.nameEn != store.nameAr)
                  Text(store.nameEn, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: p.muted)),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: [
                    StatusChip(label: store.category, tone: Tone.brand),
                    StatusChip(label: store.rewardLabel, tone: Tone.gold, icon: LucideIcons.coins),
                  ],
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Icon(LucideIcons.mapPin, size: 14, color: p.muted),
                    const SizedBox(width: 4),
                    Flexible(
                      child: Text('${store.floorLabel} • ${store.unitNumber}',
                          overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: p.muted)),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
