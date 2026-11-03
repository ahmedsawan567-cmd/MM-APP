import { UserProfile, Store, NotificationItem, ScanRecord, AdminStats } from "./types";

export const defaultProfile: UserProfile = {
  name: "منى محمد الزنتاني",
  cardId: "CUST-001",
  points: 15400,
  stamps: 4,
  prizes: 2,
  phone: "0912345678",
  birthdate: "1998-05-12",
  gender: "أنثى",
  city: "طرابلس",
  email: "mouna@mmapp.ly",
  avatar: ""
};

export const defaultStores: Store[] = [
  {
    id: "TEN-CLEANING",
    name: "مواد التنظيف الفاخرة والعناية المنزلية",
    logo: "🧼",
    category: "مواد تنظيف وعناية",
    description: "تجميع 10 طوابع للحصول على باقة منظفات فاخرة مجاناً",
    currentStamps: 4,
    maxStamps: 10,
    rewardText: "باقة منظفات وعناية بقيمة 120 د.ل",
    moq: "5 قطع",
    supplierName: "مجمع مواد التنظيف - الوحدة G-12",
    verified: true,
    stampsHistory: [
      { date: "2026-09-15", amount: "180 د.ل", count: 1 },
      { date: "2026-09-17", amount: "320 د.ل", count: 2 }
    ]
  },
  {
    id: "TEN-GROCERY",
    name: "سوبرماركت المواد الغذائية والتموينية",
    logo: "🛒",
    category: "مواد غذائية وتموين",
    description: "تجميع 8 طوابع للحصول على قسيمة مشتريات غذائية",
    currentStamps: 2,
    maxStamps: 8,
    rewardText: "قسيمة بقيمة 100 د.ل من قسم الأغذية",
    moq: "10 عبوات",
    supplierName: "هايبرماركت المواد الغذائية - الوحدة G-02",
    verified: true,
    stampsHistory: []
  },
  {
    id: "TEN-EVENING-DRESS",
    name: "بوتيك فساتين السهرة والأزياء الراقية",
    logo: "👗",
    category: "فساتين سهرة وأزياء",
    description: "تجميع 10 طوابع للحصول على فستان سهرة مخفض بنسبة 50%",
    currentStamps: 3,
    maxStamps: 10,
    rewardText: "قسيمة خصم 250 د.ل على فساتين السهرة",
    moq: "قطعة واحدة",
    supplierName: "بوتيك فساتين السهرة - الوحدة 201-A",
    verified: true,
    stampsHistory: [
      { date: "2026-09-18", amount: "450 د.ل", count: 3 }
    ]
  },
  {
    id: "TEN-KADI-CAFE",
    name: "كافي كادي (Kadi Café)",
    logo: "☕",
    category: "مقاهي ومشروبات",
    description: "تجميع 6 طوابع للحصول على مشروب وحلوى مجاناً",
    currentStamps: 1,
    maxStamps: 6,
    rewardText: "وجبة فطور وقهوة مختصة مجاناً",
    moq: "مشروب واحد",
    supplierName: "كافي كادي - الوحدة G-08",
    verified: true,
    stampsHistory: []
  },
  {
    id: "TEN-WOMEN-FASHION",
    name: "محل الملابس النسائية والأناقة",
    logo: "👚",
    category: "ملابس نسائية",
    description: "تجميع 10 طوابع للحصول على طقم ملابس نسائي",
    currentStamps: 2,
    maxStamps: 10,
    rewardText: "قسيمة بقيمة 150 د.ل على الأزياء النسائية",
    moq: "قطعتين",
    supplierName: "الملابس النسائية - الوحدة 110-A",
    verified: true,
    stampsHistory: []
  },
  {
    id: "TEN-KIDS-WEAR",
    name: "متجر ملابس الأطفال (Kids Wear)",
    logo: "🧸",
    category: "ملابس وألعاب أطفال",
    description: "تجميع 8 طوابع للحصول على طقم أطفال كامل",
    currentStamps: 0,
    maxStamps: 8,
    rewardText: "طقم أطفال كامل بقيمة 120 د.ل",
    moq: "3 قطع",
    supplierName: "ملابس الأطفال - الوحدة 115-B",
    verified: true,
    stampsHistory: []
  },
  {
    id: "TEN-CHOCOLATE",
    name: "قصر الشكلاطة والحلويات الفاخرة",
    logo: "🍫",
    category: "شوكولاتة وحلويات",
    description: "تجميع 8 طوابع للحصول على صندوق شوكولاتة بلجيكية فاخرة",
    currentStamps: 0,
    maxStamps: 8,
    rewardText: "صندوق شوكولاتة مشكل 1 كجم",
    moq: "علبة واحدة",
    supplierName: "محل للشكلاطة - الوحدة G-15",
    verified: true,
    stampsHistory: []
  },
  {
    id: "TEN-ROYAL-TOUCH",
    name: "مطعم اللمسة الملكية للمأكولات",
    logo: "🍽️",
    category: "مطاعم ومأكولات",
    description: "تجميع 10 طوابع للحصول على عشاء عائلي مجاني",
    currentStamps: 0,
    maxStamps: 10,
    rewardText: "وجبة عشاء ملكية بقيمة 180 د.ل",
    moq: "وجبة واحدة",
    supplierName: "مطعم اللمسة الملكية - الوحدة FC-01",
    verified: true,
    stampsHistory: []
  },
  {
    id: "TEN-SAQR-GAMES",
    name: "محل صقر للألعاب والترفيه الإلكتروني",
    logo: "🎮",
    category: "ألعاب وإلكترونيات",
    description: "تجميع 12 طابع للحصول على جهاز ألعاب أو إكسسوار",
    currentStamps: 0,
    maxStamps: 12,
    rewardText: "يد تحكم لاسلكية أو لعبة مجانية",
    moq: "قطعة واحدة",
    supplierName: "محل صقر للألعاب - الوحدة 208-B",
    verified: true,
    stampsHistory: []
  }
];

export const defaultNotifications: NotificationItem[] = [
  {
    id: 1,
    title: "مرحباً بكِ في المرأة مول 🦋",
    message: "تم تفعيل بطاقة العضوية الخاصة بك بنجاح مع 15,400 نقطة ولاء افتتاحية.",
    time: "منذ 10 دقائق",
    section: "عام",
    logo: "🦋",
    read: false
  },
  {
    id: 2,
    title: "طوابع مضافة - مواد التنظيف الفاخرة 🧼",
    message: "تم مسح فاتورة بقيمة 320 د.ل واعتماد 2 طوابع في رصيد بطاقتك بنجاح.",
    time: "منذ ساعتين",
    section: "المتاجر",
    logo: "🧼",
    read: false
  },
  {
    id: 3,
    title: "عرض كافي كادي الحصري ☕",
    message: "احصلي على قهوة وقطعة حلوى مجانية عند جمع 6 طوابع فقط في كافي كادي.",
    time: "منذ يوم",
    section: "العروض",
    logo: "☕",
    read: true
  }
];

export const defaultScans: ScanRecord[] = [
  {
    id: "SCN-101",
    storeId: "TEN-CLEANING",
    storeName: "مواد التنظيف الفاخرة والعناية المنزلية",
    invoiceNo: "INV-9921",
    date: "2026-09-18 11:20 م",
    amount: 180.0,
    items: ["5x مسحوق غسيل فاخر (100 د.ل)", "4x مطهر أرضيات مركز (80 د.ل)"],
    status: "Approved",
    fraudRisk: "Low",
    pHashMatch: "phash_clean_9921_safe",
    user: "منى محمد الزنتاني",
    stampsAwarded: 1
  },
  {
    id: "SCN-102",
    storeId: "TEN-EVENING-DRESS",
    storeName: "بوتيك فساتين السهرة والأزياء الراقية",
    invoiceNo: "INV-8742",
    date: "2026-09-18 10:15 م",
    amount: 450.0,
    items: ["1x فستان سهرة مخمل (300 د.ل)", "1x عباءة حرير راقية (150 د.ل)"],
    status: "Approved",
    fraudRisk: "Low",
    pHashMatch: "phash_dress_8742_safe",
    user: "منى محمد الزنتاني",
    stampsAwarded: 3
  },
  {
    id: "SCN-103",
    storeId: "TEN-ROYAL-TOUCH",
    storeName: "مطعم اللمسة الملكية للمأكولات",
    invoiceNo: "INV-1102",
    date: "2026-09-17 16:10 م",
    amount: 600.0,
    items: ["12x وجبة مشاوي ملكية مشكلة (600 د.ل)"],
    status: "Flagged",
    fraudRisk: "High",
    pHashMatch: "phash_dup_fraud_alert",
    user: "حساب تجريبي مشبوه",
    stampsAwarded: 0
  }
];

export const defaultStats: AdminStats = {
  totalSales: 48520,
  totalPoints: 128400,
  pendingAudits: 1,
  dailyScans: 42,
  poissonArrivalRate: 38.5,
  meanBasketSize: 154.2,
  standardDeviation: 42.1,
  zScore: 1.15,
  distributionData: [
    { k: 0, actual: 2, expected: 1.2 },
    { k: 1, actual: 8, expected: 7.5 },
    { k: 2, actual: 18, expected: 17.2 },
    { k: 3, actual: 24, expected: 23.8 },
    { k: 4, actual: 16, expected: 15.4 },
    { k: 5, actual: 9, expected: 8.9 },
    { k: 6, actual: 4, expected: 3.8 }
  ]
};
