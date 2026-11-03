/// مساعدات لقراءة JSON بأمان (القيم قد تكون null أو بنوع مختلف).
String jStr(dynamic v, [String fallback = '']) => v == null ? fallback : v.toString();
String? jStrOrNull(dynamic v) => v == null || v.toString().isEmpty ? null : v.toString();
int jInt(dynamic v, [int fallback = 0]) => v is num ? v.toInt() : int.tryParse('$v') ?? fallback;
double jDouble(dynamic v, [double fallback = 0]) => v is num ? v.toDouble() : double.tryParse('$v') ?? fallback;
bool jBool(dynamic v, [bool fallback = false]) => v is bool ? v : fallback;
DateTime jDate(dynamic v) => DateTime.tryParse('$v')?.toLocal() ?? DateTime.now();
DateTime? jDateOrNull(dynamic v) => v == null ? null : DateTime.tryParse('$v')?.toLocal();
List<T> jList<T>(dynamic v, T Function(Map<String, dynamic>) f) =>
    v is List ? v.whereType<Map<String, dynamic>>().map(f).toList() : <T>[];
