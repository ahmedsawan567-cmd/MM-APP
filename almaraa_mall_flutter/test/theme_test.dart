import 'package:almaraa_mall/core/theme/app_theme.dart';
import 'package:almaraa_mall/widgets/common.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget _host(ThemeData theme, Widget child) => MaterialApp(
      theme: theme,
      home: Directionality(textDirection: TextDirection.rtl, child: Scaffold(body: child)),
    );

void main() {
  test('both themes expose the app palette', () {
    expect(AppTheme.lightTheme.extension<AppPalette>(), AppPalette.light);
    expect(AppTheme.darkTheme.extension<AppPalette>(), AppPalette.dark);
    expect(AppTheme.darkTheme.brightness, Brightness.dark);
  });

  for (final (name, theme) in [('light', AppTheme.lightTheme), ('dark', AppTheme.darkTheme)]) {
    testWidgets('shared widgets render in $name theme', (tester) async {
      await tester.pumpWidget(_host(
        theme,
        ListView(children: [
          StatusChip.invoice('APPROVED'),
          StatusChip.voucher('ACTIVE'),
          const EmptyView(icon: Icons.inbox, title: 'لا شيء'),
          const AppCard(child: InfoRow('المبلغ', '10 د.ل')),
          const StoreLogo(name: 'كادي'),
        ]),
      ));
      await tester.pump(const Duration(milliseconds: 500));
      expect(find.text('مقبولة'), findsOneWidget);
      expect(find.text('نشطة'), findsOneWidget);
      expect(find.text('لا شيء'), findsOneWidget);
      expect(find.text('ك'), findsOneWidget);
    });
  }

  testWidgets('AnimatedCount ends at the target value', (tester) async {
    await tester.pumpWidget(_host(AppTheme.lightTheme, const AnimatedCount(value: 1250)));
    await tester.pumpAndSettle();
    expect(find.text('1,250'), findsOneWidget);
  });
}
