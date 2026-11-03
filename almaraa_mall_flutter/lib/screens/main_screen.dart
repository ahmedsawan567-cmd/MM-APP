import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import '../core/constants/app_constants.dart';
import 'home_tab.dart';
import 'stores_tab.dart';
import 'scan_tab.dart';
import 'wallet_tab.dart';
import 'profile_tab.dart';

class MainScreen extends StatefulWidget {
  const MainScreen({super.key});

  @override
  State<MainScreen> createState() => _MainScreenState();
}

class _MainScreenState extends State<MainScreen> {
  int _currentIndex = 0;

  void _onTabTapped(int index) {
    setState(() {
      _currentIndex = index;
    });
  }

  @override
  Widget build(BuildContext context) {
    final List<Widget> tabs = [
      HomeTab(onTabChange: _onTabTapped),
      const StoresTab(),
      ScanTab(onTabChange: _onTabTapped),
      const WalletTab(),
      const ProfileTab(),
    ];

    return Scaffold(
      body: IndexedStack(
        index: _currentIndex,
        children: tabs,
      ),
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: _currentIndex,
        onTap: _onTabTapped,
        type: BottomNavigationBarType.fixed,
        selectedItemColor: AppConstants.primaryGreen,
        unselectedItemColor: AppConstants.textMuted,
        selectedFontSize: 12,
        unselectedFontSize: 11,
        items: const [
          BottomNavigationBarItem(
            icon: Icon(LucideIcons.home),
            activeIcon: Icon(LucideIcons.home, color: AppConstants.primaryGreen),
            label: 'الرئيسية',
          ),
          BottomNavigationBarItem(
            icon: Icon(LucideIcons.store),
            activeIcon: Icon(LucideIcons.store, color: AppConstants.primaryGreen),
            label: 'المتاجر',
          ),
          BottomNavigationBarItem(
            icon: Icon(LucideIcons.scanLine),
            activeIcon: Icon(LucideIcons.scanLine, color: AppConstants.primaryGreen),
            label: 'مسح فاتورة',
          ),
          BottomNavigationBarItem(
            icon: Icon(LucideIcons.wallet),
            activeIcon: Icon(LucideIcons.wallet, color: AppConstants.primaryGreen),
            label: 'المحفظة',
          ),
          BottomNavigationBarItem(
            icon: Icon(LucideIcons.user),
            activeIcon: Icon(LucideIcons.user, color: AppConstants.primaryGreen),
            label: 'حسابي',
          ),
        ],
      ),
    );
  }
}
