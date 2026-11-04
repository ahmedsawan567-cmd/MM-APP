import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import 'home_tab.dart';
import 'profile_tab.dart';
import 'scan_tab.dart';
import 'stores_tab.dart';
import 'wallet_tab.dart';

class MainScreen extends StatefulWidget {
  const MainScreen({super.key});

  @override
  State<MainScreen> createState() => _MainScreenState();
}

class _MainScreenState extends State<MainScreen> {
  int _index = 0;

  void _go(int index) => setState(() => _index = index);

  @override
  Widget build(BuildContext context) {
    final tabs = [
      HomeTab(onTabChange: _go),
      const StoresTab(),
      ScanTab(onTabChange: _go),
      const WalletTab(),
      const ProfileTab(),
    ];

    return Scaffold(
      body: IndexedStack(index: _index, children: tabs),
      bottomNavigationBar: DecoratedBox(
        decoration: BoxDecoration(border: Border(top: BorderSide(color: Theme.of(context).dividerColor))),
        child: NavigationBar(
          selectedIndex: _index,
          onDestinationSelected: _go,
          destinations: const [
            NavigationDestination(icon: Icon(LucideIcons.house), label: 'الرئيسية'),
            NavigationDestination(icon: Icon(LucideIcons.store), label: 'المتاجر'),
            NavigationDestination(icon: Icon(LucideIcons.scanLine), label: 'مسح'),
            NavigationDestination(icon: Icon(LucideIcons.wallet), label: 'المحفظة'),
            NavigationDestination(icon: Icon(LucideIcons.user), label: 'حسابي'),
          ],
        ),
      ),
    );
  }
}
