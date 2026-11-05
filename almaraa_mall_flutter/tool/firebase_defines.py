"""يحوّل ملف google-services.json (من Firebase) إلى ملف قيم للبناء:
    python3 tool/firebase_defines.py google-services.json > firebase-defines.json
    flutter build apk --release --dart-define-from-file=firebase-defines.json
"""
import json
import sys

PACKAGE = "ly.mmapp.almaraamall"

data = json.load(open(sys.argv[1], encoding="utf-8"))
project = data["project_info"]
client = next(
    (c for c in data["client"] if c["client_info"]["android_client_info"]["package_name"] == PACKAGE),
    None,
)
if client is None:
    sys.exit(f"لم يُعثر على تطبيق أندرويد باسم الحزمة {PACKAGE} داخل google-services.json")

json.dump(
    {
        "FIREBASE_API_KEY": client["api_key"][0]["current_key"],
        "FIREBASE_APP_ID": client["client_info"]["mobilesdk_app_id"],
        "FIREBASE_SENDER_ID": project["project_number"],
        "FIREBASE_PROJECT_ID": project["project_id"],
        "FIREBASE_STORAGE_BUCKET": project.get("storage_bucket", ""),
    },
    sys.stdout,
)
