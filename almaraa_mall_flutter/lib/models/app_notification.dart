import 'json_utils.dart';

class AppNotification {
  final String id;
  final String type;
  final String title;
  final String body;
  final String? link;
  final bool isRead;
  final DateTime createdAt;

  AppNotification({
    required this.id,
    required this.type,
    required this.title,
    required this.body,
    this.link,
    required this.isRead,
    required this.createdAt,
  });

  factory AppNotification.fromJson(Map<String, dynamic> j) => AppNotification(
        id: jStr(j['id']),
        type: jStr(j['type']),
        title: jStr(j['title']),
        body: jStr(j['body']),
        link: jStrOrNull(j['link']),
        isRead: jBool(j['isRead']),
        createdAt: jDate(j['createdAt']),
      );

  AppNotification copyWith({bool? isRead}) =>
      AppNotification(id: id, type: type, title: title, body: body, link: link, isRead: isRead ?? this.isRead, createdAt: createdAt);
}
