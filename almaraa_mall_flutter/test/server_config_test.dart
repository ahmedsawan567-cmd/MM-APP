import 'package:almaraa_mall/core/services/server_config.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('normalizes what users type into a full API base URL', () {
    expect(ServerConfig.normalize('192.168.1.10'), 'http://192.168.1.10:5000/api');
    expect(ServerConfig.normalize(' 192.168.1.10:8080 '), 'http://192.168.1.10:8080/api');
    expect(ServerConfig.normalize('http://192.168.1.10:5000/api/'), 'http://192.168.1.10:5000/api');
    expect(ServerConfig.normalize('https://mall.example.ly'), 'https://mall.example.ly/api');
    expect(ServerConfig.normalize('http://192.168.1.106:5000/api/health'), 'http://192.168.1.106:5000/api');
    expect(ServerConfig.normalize('192.168.1.106:5000/api/health/'), 'http://192.168.1.106:5000/api');
    expect(ServerConfig.normalize('http://192.168.1.106:5000/AdminDashboard/Login'), 'http://192.168.1.106:5000/api');
    expect(ServerConfig.normalize(''), isNull);
    expect(ServerConfig.normalize('ftp://x'), isNull);
  });
}
