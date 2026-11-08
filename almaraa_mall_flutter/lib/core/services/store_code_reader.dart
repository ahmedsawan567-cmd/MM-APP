import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:ndef_record/ndef_record.dart';
import 'package:nfc_manager/nfc_manager.dart';
import 'package:nfc_manager/nfc_manager_android.dart';
import 'package:nfc_manager/nfc_manager_ios.dart';

/// قراءة كود المتجر من بطاقة/ملصق NFC (سجل نصي أو رابط يحتوي MMALL-STORE:XXXX).
class StoreNfcReader {
  StoreNfcReader._();

  static Future<bool> isAvailable() async {
    if (kIsWeb) return false;
    try {
      return await NfcManager.instance.checkAvailability() == NfcAvailability.enabled;
    } catch (_) {
      return false;
    }
  }

  /// يبدأ القراءة وينتظر تقريب البطاقة. يرجع النص المكتوب على البطاقة أو null.
  static Future<String?> read({Duration timeout = const Duration(seconds: 30)}) async {
    final completer = Completer<String?>();
    await NfcManager.instance.startSession(
      pollingOptions: {NfcPollingOption.iso14443, NfcPollingOption.iso15693},
      alertMessageIos: 'قرّب الهاتف من بطاقة المتجر',
      onDiscovered: (tag) async {
        String? text;
        try {
          NdefMessage? message;
          if (Platform.isAndroid) {
            message = NdefAndroid.from(tag)?.cachedNdefMessage;
          } else if (Platform.isIOS) {
            message = NdefIos.from(tag)?.cachedNdefMessage;
          }
          for (final r in message?.records ?? const <NdefRecord>[]) {
            text = _decode(r);
            if (text != null && text.toUpperCase().contains('MMALL-STORE')) break;
          }
        } catch (_) {}
        await NfcManager.instance.stopSession(alertMessageIos: text == null ? null : 'تمت القراءة');
        if (!completer.isCompleted) completer.complete(text);
      },
    );
    return completer.future.timeout(timeout, onTimeout: () async {
      try {
        await NfcManager.instance.stopSession();
      } catch (_) {}
      return null;
    });
  }

  static Future<void> cancel() async {
    try {
      await NfcManager.instance.stopSession();
    } catch (_) {}
  }

  /// سجل نصي (T): أول بايت = طول رمز اللغة. سجل رابط (U): أول بايت = بادئة الرابط.
  static String? _decode(NdefRecord r) {
    final p = r.payload;
    if (p.isEmpty) return null;
    final type = utf8.decode(r.type, allowMalformed: true);
    if (r.typeNameFormat == TypeNameFormat.wellKnown && type == 'T') {
      final langLen = p[0] & 0x3F;
      if (p.length <= 1 + langLen) return null;
      return utf8.decode(p.sublist(1 + langLen), allowMalformed: true);
    }
    if (r.typeNameFormat == TypeNameFormat.wellKnown && type == 'U') {
      return utf8.decode(p.sublist(1), allowMalformed: true);
    }
    return utf8.decode(p, allowMalformed: true);
  }
}
