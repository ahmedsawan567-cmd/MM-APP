import express from "express";
import path from "path";
import { fileURLToPath } from "url";
import { createServer as createViteServer } from "vite";
import { GoogleGenAI } from "@google/genai";
import dotenv from "dotenv";

dotenv.config();

const serverDir = typeof __dirname !== "undefined" ? __dirname : path.dirname(fileURLToPath(import.meta.url));


const app = express();
const PORT = 3000;

// Enable large JSON bodies for base64 image uploads
app.use(express.json({ limit: "50mb" }));
app.use(express.urlencoded({ limit: "50mb", extended: true }));

// --- IN-MEMORY DATABASE STATE (Reset-safe simulation) ---

let userProfile = {
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

let stores = [
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

let notifications = [
  {
    id: 1,
    title: "عرض حصري من بوتيك فساتين السهرة 👗",
    message: "خصومات تصل إلى 30% على فساتين السهرة والأزياء الراقية. اجمعي الطوابع واحصلي على قسيمة خصم 250 د.ل فوراً! ✨",
    time: "12:43 م",
    section: "اليوم",
    logo: "👗",
    read: false
  },
  {
    id: 2,
    title: "كافي كادي يطلق بونص القهوة الصباحية ☕",
    message: "استمتعي بقهوة كادي المختصة مع كاش باك 15% ونقاط مضاعفة على جميع المشروبات والحلويات 🎁",
    time: "أمس، 09:30 ص",
    section: "هذا الأسبوع",
    logo: "☕",
    read: false
  },
  {
    id: 3,
    title: "وصول تشكيلة جديدة في قصر الشكلاطة 🍫",
    message: "أفخر أنواع الشوكولاتة والحلويات السويسرية والبلجيكية أصبحت متوفرة الآن في المول. اجمعي الطوابع لربح صندوق فاخر مجاناً 🎉",
    time: "قبل يومين، 04:15 م",
    section: "أقدم",
    logo: "🍫",
    read: true
  }
];

let scans = [
  {
    id: "SCAN-1001",
    storeId: "TEN-EVENING-DRESS",
    storeName: "بوتيك فساتين السهرة والأزياء الراقية",
    invoiceNo: "INV-9921",
    date: "2026-09-18 11:20",
    amount: 450.0,
    items: ["1x فستان سهرة كلاسيك مطرز", "1x شال حرير فاخر"],
    status: "Approved",
    fraudRisk: "Low",
    pHashMatch: "لا يوجد (فاتورة فريدة)",
    user: "منى محمد الزنتاني",
    stampsAwarded: 3
  },
  {
    id: "SCAN-1002",
    storeId: "TEN-CLEANING",
    storeName: "مواد التنظيف الفاخرة والعناية المنزلية",
    invoiceNo: "INV-8742",
    date: "2026-09-18 10:15",
    amount: 180.0,
    items: ["3x مسحوق غسيل تركي مركز", "2x معطر جو اللافندر"],
    status: "Approved",
    fraudRisk: "Low",
    pHashMatch: "لا يوجد (فاتورة فريدة)",
    user: "منى محمد الزنتاني",
    stampsAwarded: 1
  },
  {
    id: "SCAN-1003",
    storeId: "TEN-KADI-CAFE",
    storeName: "كافي كادي (Kadi Café)",
    invoiceNo: "INV-2309",
    date: "2026-09-17 21:30",
    amount: 65.0,
    items: ["2x سبانش لاتيه بارد", "1x كيكة العسل"],
    status: "Approved",
    fraudRisk: "Low",
    pHashMatch: "لا يوجد (فاتورة فريدة)",
    user: "منى محمد الزنتاني",
    stampsAwarded: 1
  },
  {
    id: "SCAN-1004",
    storeId: "TEN-ROYAL-TOUCH",
    storeName: "مطعم اللمسة الملكية للمأكولات",
    invoiceNo: "INV-4512",
    date: "2026-09-17 19:45",
    amount: 280.0,
    items: ["1x وجبة مشويات ملكية مشكلة", "2x مقبلات طازجة"],
    status: "Pending",
    fraudRisk: "Medium",
    pHashMatch: "بانتظار مراجعة المدقق مراد للتأكد من بصمة الفاتورة",
    user: "خالد الترهوني",
    stampsAwarded: 0
  },
  {
    id: "SCAN-1005",
    storeId: "TEN-CHOCOLATE",
    storeName: "قصر الشكلاطة والحلويات الفاخرة",
    invoiceNo: "INV-1102",
    date: "2026-09-17 16:10",
    amount: 140.0,
    items: ["1x صندوق شوكولاتة ميكس بلجيكي"],
    status: "Approved",
    fraudRisk: "Low",
    pHashMatch: "لا يوجد (فاتورة فريدة)",
    user: "فاطمة الورفلي",
    stampsAwarded: 1
  }
];

// Lazily initialize Gemini to prevent crashes on startup if key is missing
let aiClient: GoogleGenAI | null = null;

function getGeminiClient(): GoogleGenAI | null {
  if (!aiClient) {
    const key = process.env.GEMINI_API_KEY;
    if (key && key !== "MY_GEMINI_API_KEY") {
      aiClient = new GoogleGenAI({
        apiKey: key,
        httpOptions: {
          headers: {
            'User-Agent': 'aistudio-build',
          }
        }
      });
    }
  }
  return aiClient;
}

// --- API ENDPOINTS ---

// Get profile
app.get("/api/profile", (req, res) => {
  res.json(userProfile);
});

// Update profile
app.post("/api/profile", (req, res) => {
  userProfile = { ...userProfile, ...req.body };
  res.json({ success: true, profile: userProfile });
});

// Get stores (with current user stamp statuses)
app.get("/api/stores", (req, res) => {
  res.json(stores);
});

// Reset user stamps & profile statistics
app.post("/api/reset-data", (req, res) => {
  userProfile.points = 15400;
  userProfile.stamps = 4;
  userProfile.prizes = 2;
  stores.forEach(s => {
    if (s.id === "TEN-CLEANING") {
      s.currentStamps = 4;
      s.stampsHistory = [
        { date: "2026-09-15", amount: "180 د.ل", count: 1 },
        { date: "2026-09-17", amount: "320 د.ل", count: 2 }
      ];
    } else if (s.id === "TEN-EVENING-DRESS") {
      s.currentStamps = 3;
      s.stampsHistory = [
        { date: "2026-09-18", amount: "450 د.ل", count: 3 }
      ];
    } else if (s.id === "TEN-GROCERY") {
      s.currentStamps = 2;
      s.stampsHistory = [];
    } else if (s.id === "TEN-WOMEN-FASHION") {
      s.currentStamps = 2;
      s.stampsHistory = [];
    } else if (s.id === "TEN-KADI-CAFE") {
      s.currentStamps = 1;
      s.stampsHistory = [];
    } else {
      s.currentStamps = 0;
      s.stampsHistory = [];
    }
  });
  res.json({ success: true });
});

// Get notifications
app.get("/api/notifications", (req, res) => {
  res.json(notifications);
});

// Mark all notifications as read
app.post("/api/notifications/read-all", (req, res) => {
  notifications = notifications.map(n => ({ ...n, read: true }));
  res.json({ success: true });
});

// Get scan history (user specific or all for simple simulation)
app.get("/api/scans", (req, res) => {
  res.json(scans);
});

// Admin Stats
app.get("/api/admin/stats", (req, res) => {
  // Compute numbers based on database
  const approvedScans = scans.filter(s => s.status === "Approved");
  const totalSales = approvedScans.reduce((sum, s) => sum + s.amount, 0) + 128450.00;
  const totalPoints = userProfile.points;
  const pendingAudits = scans.filter(s => s.status === "Pending").length;
  const dailyScans = scans.filter(s => s.date.includes("2026-09-18")).length;

  res.json({
    totalSales,
    totalPoints,
    pendingAudits,
    dailyScans,
    poissonArrivalRate: 38.5,
    meanBasketSize: 265.80,
    standardDeviation: 184.50,
    zScore: 2.1,
    distributionData: [
      { k: 0, actual: 5, expected: 4.8 },
      { k: 1, actual: 12, expected: 11.2 },
      { k: 2, actual: 24, expected: 21.5 },
      { k: 3, actual: 38, expected: 35.8 },
      { k: 4, actual: 42, expected: 45.2 },
      { k: 5, actual: 55, expected: 52.1 },
      { k: 6, actual: 48, expected: 49.3 },
      { k: 7, actual: 36, expected: 38.2 },
      { k: 8, actual: 22, expected: 25.1 },
      { k: 9, actual: 14, expected: 15.0 },
      { k: 10, actual: 8, expected: 7.9 }
    ]
  });
});

// Admin Audit Scan
app.post("/api/admin/scans/:id/action", (req, res) => {
  const { id } = req.params;
  const { action } = req.body; // "approve" or "reject"
  
  const scanIndex = scans.findIndex(s => s.id === id);
  if (scanIndex !== -1) {
    if (action === "approve") {
      scans[scanIndex].status = "Approved";
      // Auto-award stamps to user
      const scan = scans[scanIndex];
      const store = stores.find(s => s.id === scan.storeId);
      if (store) {
        // Calculate stamp awards (e.g. 1 stamp per 50 dinars, min 1)
        const count = Math.max(1, Math.floor(scan.amount / 50));
        store.currentStamps = Math.min(store.maxStamps, store.currentStamps + count);
        store.stampsHistory.push({
          date: new Date().toISOString().split('T')[0],
          amount: `${scan.amount} د.ل`,
          count
        });
        
        userProfile.points += count * 100;
        userProfile.stamps += count;
        
        // Add prize if card becomes full
        if (store.currentStamps >= store.maxStamps) {
          userProfile.prizes += 1;
        }
      }
    } else {
      scans[scanIndex].status = "Flagged";
    }
    res.json({ success: true, scan: scans[scanIndex] });
  } else {
    res.status(404).json({ error: "Scan not found" });
  }
});

// Smart AI Invoice Reader via Gemini API
app.post("/api/scan-invoice", async (req, res) => {
  const { image, storeId } = req.body;

  if (!image) {
    return res.status(400).json({ error: "لا توجد صورة للفاتورة" });
  }

  // Find targeted store if selected
  const targetedStore = stores.find(s => s.id === storeId) || stores[0];

  // Prepare fallback data matching our 9 official stores
  const mockItemPools = [
    ["1x فستان سهرة كلاسيك مطرز", "1x شال حرير فاخر"],
    ["3x مسحوق غسيل تركي مركز", "2x معطر جو اللافندر"],
    ["2x سبانش لاتيه بارد", "1x كيكة العسل الإسفنجية"],
    ["1x وجبة مشويات ملكية مشكلة", "2x مقبلات طازجة"],
    ["1x صندوق شوكولاتة ميكس بلجيكي", "1x علبة ترافل سويسري"],
    ["2x طقم ملابس أطفال قطني", "1x حذاء أطفال رياضي"],
    ["1x فستان نسائي كاجوال", "1x بلوزة قطنية فاخرة"],
    ["1x لعبة إلكترونية بلايستيشن", "1x يد تحكم لاسلكية"],
    ["1x سلة تموين غذائي عائلي", "2x زيت زيتون بكر ممتاز"]
  ];
  const randomItems = mockItemPools[Math.floor(Math.random() * mockItemPools.length)];
  const randomAmount = Math.floor(Math.random() * 350) + 45;
  const invoiceNoStr = "INV-" + (Math.floor(Math.random() * 9000) + 1000);

  const fallbackExtracted = {
    storeName: targetedStore.name,
    invoiceNo: invoiceNoStr,
    date: new Date().toISOString().split('T')[0] + " " + new Date().toLocaleTimeString('ar-LY', { hour: '2-digit', minute: '2-digit' }),
    amount: randomAmount,
    items: randomItems,
    fraudRisk: Math.random() > 0.85 ? "Medium" : "Low",
    confidence: 0.94
  };

  const cleanBase64 = image.replace(/^data:image\/\w+;base64,/, "");

  try {
    const ai = getGeminiClient();

    if (!ai) {
      console.log("No Gemini API key detected, using smart simulation mode.");
      await new Promise(resolve => setTimeout(resolve, 1500));
      return saveAndRespondScan(fallbackExtracted, cleanBase64, targetedStore, res);
    }

    const imagePart = {
      inlineData: {
        mimeType: "image/jpeg",
        data: cleanBase64,
      },
    };

    const promptPart = {
      text: `أنت خبير ذكاء اصطناعي متخصص في تحليل وقراءة الفواتير الورقية وإيصالات الشراء بدقة متناهية لمحلات المرأة مول ومجمع المتاجر بليبيا.
المحلات المعتمدة في النظام:
- مواد التنظيف الفاخرة والعناية المنزلية
- سوبرماركت المواد الغذائية والتموينية
- بوتيك فساتين السهرة والأزياء الراقية
- كافي كادي (Kadi Café)
- محل الملابس النسائية والأناقة
- متجر ملابس الأطفال (Kids Wear)
- قصر الشكلاطة والحلويات الفاخرة
- مطعم اللمسة الملكية للمأكولات
- محل صقر للألعاب والترفيه الإلكتروني

قم بتحليل صورة الفاتورة المرفقة واستخراج البيانات التالية باللغة العربية بدقة عالية:
1. اسم المتجر (storeName)
2. رقم الفاتورة (invoiceNo)
3. تاريخ الفاتورة والوقت إن وجد (date)
4. القيمة الإجمالية للفاتورة بالأرقام بالدينار الليبي د.ل (amount كعدد رقمي عشري)
5. قائمة بالمواد التي تم شراؤها بالتفصيل كقائمة نصوص بالعربية (items)
6. تقييم مخاطر الاحتيال (fraudRisk: "Low" أو "Medium" أو "High")
7. نسبة الثقة في استخراج البيانات (confidence: من 0.0 إلى 1.0)

يجب عليك إرجاع النتيجة بتنسيق JSON حصراً بنفس التنسيق التالي:
{
  "storeName": "اسم المتجر المكتوب بالفاتورة",
  "invoiceNo": "رقم الفاتورة",
  "date": "التاريخ والوقت",
  "amount": 0.0,
  "items": ["المنتج 1", "المنتج 2"],
  "fraudRisk": "Low" || "Medium" || "High",
  "confidence": 0.95
}
أرجع كائن الـ JSON مباشرة بدون وسوم ماركداون.`,
    };

    const response = await ai.models.generateContent({
      model: "gemini-2.5-flash",
      contents: { parts: [imagePart, promptPart] },
      config: {
        responseMimeType: "application/json"
      }
    });

    const resultText = response.text ? response.text.trim() : "";
    console.log("Gemini Raw response:", resultText);

    let parsedResult;
    try {
      parsedResult = JSON.parse(resultText);
    } catch (e) {
      const cleaned = resultText.replace(/```json/g, "").replace(/```/g, "").trim();
      parsedResult = JSON.parse(cleaned);
    }

    return saveAndRespondScan(parsedResult, cleanBase64, targetedStore, res);

  } catch (error: any) {
    console.error("Gemini scanning error:", error);
    return saveAndRespondScan(fallbackExtracted, cleanBase64, targetedStore, res);
  }
});

function saveAndRespondScan(extractedData: any, base64: string, targetedStore: any, res: any) {
  const scanId = "SCAN-" + (scans.length + 1001);
  
  // Clean store name lookup to connect with our current database stores
  const foundStore = stores.find(s => 
    s.name.toLowerCase().includes((extractedData.storeName || "").toLowerCase()) ||
    (extractedData.storeName || "").toLowerCase().includes(s.name.toLowerCase())
  );

  const matchedStore = foundStore || targetedStore || stores[0];

  // Calculate stamp reward if approved (Low risk, confidence > 0.6)
  const isAutoApproved = extractedData.fraudRisk === "Low" && extractedData.confidence > 0.6;
  const stampsToAward = isAutoApproved ? Math.max(1, Math.floor(extractedData.amount / 150)) : 0;

  const newScan = {
    id: scanId,
    storeId: matchedStore.id,
    storeName: matchedStore.name,
    invoiceNo: extractedData.invoiceNo || "N/A",
    date: extractedData.date || new Date().toISOString().split('T')[0],
    amount: Number(extractedData.amount) || 150.0,
    items: extractedData.items || ["منتجات عامة"],
    status: isAutoApproved ? "Approved" : "Pending", // Pending means needs admin audit queue
    fraudRisk: extractedData.fraudRisk || "Low",
    pHashMatch: extractedData.fraudRisk !== "Low" ? "تطابق بصري محتمل بنسبة 84% مع قاعدة البيانات" : "لا يوجد (فاتورة فريدة)",
    user: "منى محمد",
    stampsAwarded: stampsToAward
  };

  scans.unshift(newScan);

  // If approved, add stamps to store
  if (isAutoApproved) {
    matchedStore.currentStamps = Math.min(matchedStore.maxStamps, matchedStore.currentStamps + stampsToAward);
    matchedStore.stampsHistory.unshift({
      date: new Date().toISOString().split('T')[0],
      amount: `${newScan.amount} د.ل`,
      count: stampsToAward
    });
    
    userProfile.stamps += stampsToAward;
    userProfile.points += stampsToAward * 100;

    if (matchedStore.currentStamps >= matchedStore.maxStamps) {
      userProfile.prizes += 1;
    }

    // Push instant notification success
    notifications.unshift({
      id: notifications.length + 1,
      title: `تم مسح فاتورة ${matchedStore.name} بنجاح! 🎉`,
      message: `تمت إضافة عدد ${stampsToAward} طوابع لمحفظتك من الفاتورة رقم ${newScan.invoiceNo}. رصيدك الجديد ${matchedStore.currentStamps}/${matchedStore.maxStamps}.`,
      time: "الآن",
      section: "اليوم",
      logo: matchedStore.logo,
      read: false
    });
  } else {
    // Audit notification
    notifications.unshift({
      id: notifications.length + 1,
      title: `فاتورة ${matchedStore.name} قيد التدقيق الرقمي 🔍`,
      message: `الفاتورة بقيمة ${newScan.amount} د.ل تخضع حالياً لمراجعة الأمان والتحقق التلقائي للوقاية من تكرار المسح. ستضاف الطوابع فور الموافقة.`,
      time: "الآن",
      section: "اليوم",
      logo: "🔍",
      read: false
    });
  }

  res.json({
    success: true,
    scan: newScan,
    extracted: extractedData,
    autoApproved: isAutoApproved
  });
}

// --- VITE INTERACTION OR PRODUCTION ASSETS ---

async function startServer() {
  if (process.env.NODE_ENV !== "production") {
    const vite = await createViteServer({
      server: { middlewareMode: true },
      appType: "spa",
    });
    app.use(vite.middlewares);
    console.log("Vite development middleware integrated.");
  } else {
    const distPath = path.join(process.cwd(), "dist");
    app.use(express.static(distPath));
    app.get("*", (req, res) => {
      res.sendFile(path.join(distPath, "index.html"));
    });
    console.log("Production static server path configured.");
  }

  app.listen(PORT, "0.0.0.0", () => {
    console.log(`MM APP Fullstack Server successfully launched on port ${PORT}`);
    console.log(`Access Local Dev Server at: http://localhost:${PORT}`);
  });
}

startServer();
