import 'dart:async';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../core/constants/app_constants.dart';
import '../core/services/loyalty_api.dart';
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

  void _load() {
    setState(() => _future = LoyaltyApi.getStores());
  }

  void _onSearchChanged(String _) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 250), () => setState(() {}));
  }

  List<Store> _filter(List<Store> stores) {
    final q = _search.text.trim().toLowerCase();
    return stores.where((s) {
      final matchesCategory = _category == null || s.category == _category;
      final matchesQuery = q.isEmpty ||
          s.nameAr.toLowerCase().contains(q) ||
          s.nameEn.toLowerCase().contains(q) ||
          s.category.toLowerCase().contains(q);
      return matchesCategory && matchesQuery;
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
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
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
                child: TextField(
                  controller: _search,
                  onChanged: _onSearchChanged,
                  decoration: InputDecoration(
                    hintText: 'ابحث عن متجر أو فئة...',
                    prefixIcon: const Icon(LucideIcons.search, size: 20),
                    filled: true,
                    fillColor: Colors.white,
                    contentPadding: const EdgeInsets.symmetric(vertical: 0),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(14),
                      borderSide: BorderSide(color: Colors.grey.withOpacity(0.2)),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(14),
                      borderSide: BorderSide(color: Colors.grey.withOpacity(0.2)),
                    ),
                  ),
                ),
              ),
              if (categories.length > 1)
                SizedBox(
                  height: 44,
                  child: ListView(
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    children: [
                      _chip('الكل', null),
                      ...categories.map((c) => _chip(c, c)),
                    ],
                  ),
                ),
              Expanded(
                child: RefreshIndicator(
                  color: AppConstants.primaryGreen,
                  onRefresh: () async {
                    _load();
                    await _future;
                  },
                  child: filtered.isEmpty
                      ? ListView(children: const [
                          EmptyView(icon: LucideIcons.store, title: 'لا توجد متاجر مطابقة'),
                        ])
                      : ListView.builder(
                          padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                          itemCount: filtered.length,
                          itemBuilder: (_, i) => _StoreCard(store: filtered[i]),
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
        selectedColor: AppConstants.primaryGreen,
        labelStyle: TextStyle(color: selected ? Colors.white : AppConstants.textPrimary),
        showCheckmark: false,
      ),
    );
  }
}

class _StoreCard extends StatelessWidget {
  final Store store;
  const _StoreCard({required this.store});

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            StoreLogo(name: store.nameAr, logoUrl: store.logoUrl, size: 56),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(store.nameAr,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                  if (store.nameEn.isNotEmpty && store.nameEn != store.nameAr)
                    Text(store.nameEn,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 12, color: AppConstants.textMuted)),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 6,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      StatusChip(label: store.category, color: AppConstants.primaryGreenLight),
                      StatusChip(label: store.rewardLabel, color: AppConstants.accentGold),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      const Icon(LucideIcons.mapPin, size: 13, color: AppConstants.textMuted),
                      const SizedBox(width: 4),
                      Flexible(
                        child: Text('${store.floorLabel} • ${store.unitNumber}',
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontSize: 12, color: AppConstants.textMuted)),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
