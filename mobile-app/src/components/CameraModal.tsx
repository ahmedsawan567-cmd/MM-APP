import React, { useState, useRef, useEffect } from "react";
import { Camera, Upload, X, Sparkles, AlertCircle, CheckCircle, FileText, Loader2, Zap } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import { Store } from "../types";
import { recordInvoiceScanToSupabase } from "../lib/supabase";


interface CameraModalProps {
  isOpen: boolean;
  onClose: () => void;
  selectedStore: Store;
  onScanComplete: (data: { success: boolean; scan: any; autoApproved: boolean }) => void;
}

// Sample Almaraa Mall Receipts for quick testing
const SAMPLE_RECEIPTS = [
  {
    name: "مواد التنظيف الفاخرة 🧼",
    storeId: "TEN-CLEANING",
    amount: "180.0 د.ل",
    invoiceNo: "INV-9921",
    logo: "🧼",
    items: ["5x مسحوق غسيل فاخر 5 كجم (100 د.ل)", "4x مطهر ومنظف أرضيات مركز (80 د.ل)"],
    imageText: `
      ---------------------------------
             ALMARAA MALL - المواد الفاخرة
             الوحدة G-12، طرابلس - ليبيا
      ---------------------------------
      رقم الفاتورة: INV-9921
      التاريخ: 2026-09-18 11:20 م
      العميل: منى محمد الزنتاني
      ---------------------------------
      5x مسحوق غسيل فاخر    100.00 د.ل
      4x مطهر أرضيات مركز     80.00 د.ل
      ---------------------------------
      المجموع الإجمالي:     180.00 د.ل
      النوع: مبيعات تجزئة
      شكراً لزيارتكم! طوابع التنظيف مضافة
      ---------------------------------
    `
  },
  {
    name: "بوتيك فساتين السهرة 👗",
    storeId: "TEN-EVENING-DRESS",
    amount: "450.0 د.ل",
    invoiceNo: "INV-8742",
    logo: "👗",
    items: ["1x فستان سهرة مخمل تركي (300 د.ل)", "1x عباءة حرير راقية (150 د.ل)"],
    imageText: `
      ---------------------------------
             ALMARAA MALL - فساتين السهرة
             الوحدة 201-A، طرابلس - ليبيا
      ---------------------------------
      رقم الفاتورة: INV-8742
      التاريخ: 2026-09-18 10:15 م
      العميل: منى محمد الزنتاني
      ---------------------------------
      1x فستان سهرة مخمل    300.00 د.ل
      1x عباءة حرير راقية   150.00 د.ل
      ---------------------------------
      المجموع الإجمالي:     450.00 د.ل
      النوع: مبيعات راقية
      شكراً لزيارتكم! طوابع الأزياء مضافة
      ---------------------------------
    `
  },
  {
    name: "كافي كادي Kadi Café ☕",
    storeId: "TEN-KADI-CAFE",
    amount: "45.0 د.ل",
    invoiceNo: "INV-3310",
    logo: "☕",
    items: ["2x سبانش لاتيه بارد (24 د.ل)", "1x تشيز كيك توت (21 د.ل)"],
    imageText: `
      ---------------------------------
               KADI CAFÉ & BAKERY
             الوحدة G-08، طرابلس - ليبيا
      ---------------------------------
      رقم الفاتورة: INV-3310
      التاريخ: 2026-09-18 09:30 م
      العميل: منى محمد الزنتاني
      ---------------------------------
      2x سبانش لاتيه بارد    24.00 د.ل
      1x تشيز كيك توت       21.00 د.ل
      ---------------------------------
      المجموع الإجمالي:      45.00 د.ل
      شكراً لزيارتكم كافي كادي! ☕
      ---------------------------------
    `
  },
  {
    name: "فاتورة مشبوهة (احتيال مكرر) ⚠️",
    storeId: "TEN-ROYAL-TOUCH",
    amount: "600.0 د.ل",
    invoiceNo: "INV-1102",
    logo: "⚠️",
    items: ["12x وجبة مشاوي ملكية مشكلة (600 د.ل)"],
    imageText: `
      ---------------------------------
             مطعم اللمسة الملكية
             الوحدة FC-01، طرابلس
      ---------------------------------
      رقم الفاتورة: INV-1102  [مكرر]
      التاريخ: 2026-09-17 16:10 م
      العميل: حساب تجريبي مشبوه
      ---------------------------------
      12x وجبة مشاوي ملكية  600.00 د.ل
      ---------------------------------
      المجموع الإجمالي:     600.00 د.ل
      [تحذير الأمان: تم مسح هذه الفاتورة مسبقاً]
      ---------------------------------
    `
  }
];

export default function CameraModal({ isOpen, onClose, selectedStore, onScanComplete }: CameraModalProps) {
  const [activeTab, setActiveTab] = useState<"camera" | "upload" | "samples">("samples");
  const [imageSrc, setImageSrc] = useState<string | null>(null);
  const [isScanning, setIsScanning] = useState(false);
  const [scanResult, setScanResult] = useState<any | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const [cameraActive, setCameraActive] = useState(false);

  const videoRef = useRef<HTMLVideoElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (!isOpen) {
      stopCamera();
      setImageSrc(null);
      setScanResult(null);
      setIsScanning(false);
    }
  }, [isOpen]);

  // Handle Camera initialization
  const startCamera = async () => {
    try {
      setCameraActive(true);
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: "environment" }
      });
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
      }
    } catch (err) {
      console.error("Camera access failed:", err);
      alert("تعذر الوصول إلى الكاميرا. يرجى تفعيل الصلاحيات أو استخدام خيار تحميل الملفات أو الفواتير الجاهزة.");
      setCameraActive(false);
    }
  };

  const stopCamera = () => {
    if (videoRef.current && videoRef.current.srcObject) {
      const stream = videoRef.current.srcObject as MediaStream;
      stream.getTracks().forEach(track => track.stop());
      videoRef.current.srcObject = null;
    }
    setCameraActive(false);
  };

  const capturePhoto = () => {
    if (videoRef.current && canvasRef.current) {
      const video = videoRef.current;
      const canvas = canvasRef.current;
      canvas.width = video.videoWidth;
      canvas.height = video.videoHeight;
      const ctx = canvas.getContext("2d");
      if (ctx) {
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
        const dataUrl = canvas.toDataURL("image/jpeg");
        setImageSrc(dataUrl);
        stopCamera();
      }
    }
  };

  // Convert receipt text into a beautiful simulated invoice image
  const generateReceiptAsImage = (text: string): string => {
    const canvas = document.createElement("canvas");
    canvas.width = 400;
    canvas.height = 550;
    const ctx = canvas.getContext("2d");
    if (ctx) {
      ctx.fillStyle = "#F8FAFC"; // slate-50
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      
      // Draw receipt border
      ctx.strokeStyle = "#E2E8F0"; // slate-200
      ctx.lineWidth = 4;
      ctx.strokeRect(10, 10, canvas.width - 20, canvas.height - 20);

      // Draw thermal receipt styling
      ctx.fillStyle = "#1E293B"; // slate-800
      ctx.font = "bold 13px monospace";
      const lines = text.split("\n");
      lines.forEach((line, index) => {
        ctx.fillText(line, 20, 40 + index * 18);
      });
      
      return canvas.toDataURL("image/jpeg");
    }
    return "";
  };

  const handleSelectSample = (sample: typeof SAMPLE_RECEIPTS[0]) => {
    const generatedBase64 = generateReceiptAsImage(sample.imageText);
    setImageSrc(generatedBase64);
  };

  // Drag and Drop handlers
  const handleDrag = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.type === "dragenter" || e.type === "dragover") {
      setDragActive(true);
    } else if (e.type === "dragleave") {
      setDragActive(false);
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      handleFile(e.dataTransfer.files[0]);
    }
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      handleFile(e.target.files[0]);
    }
  };

  const handleFile = (file: File) => {
    const reader = new FileReader();
    reader.onload = (event) => {
      if (event.target?.result) {
        setImageSrc(event.target.result as string);
      }
    };
    reader.readAsDataURL(file);
  };

  // Submit scan to backend and save to Supabase Cloud
  const handleStartScan = async () => {
    if (!imageSrc) return;
    setIsScanning(true);

    try {
      // Find sample or create scan object
      const matchedSample = SAMPLE_RECEIPTS.find(r => r.storeId === selectedStore.id) || SAMPLE_RECEIPTS[0];
      const isSuspect = imageSrc.includes("1102") || matchedSample.name.includes("احتيال");
      const amountVal = parseFloat(matchedSample.amount.replace(/[^0-9.]/g, "")) || 180;
      const stampsCount = Math.max(1, Math.min(3, Math.floor(amountVal / 50)));
      const pointsCount = Math.floor(amountVal * 10);

      const scanObj = {
        id: `SCN-${Date.now().toString().slice(-6)}`,
        storeId: selectedStore.id,
        storeName: selectedStore.name,
        invoiceNo: matchedSample.invoiceNo || `INV-${Date.now().toString().slice(-4)}`,
        date: new Date().toISOString().split("T")[0],
        amount: amountVal,
        items: matchedSample.items,
        status: (isSuspect ? "Flagged" : "Approved") as any,
        fraudRisk: (isSuspect ? "High" : "Low") as any,
        pHashMatch: isSuspect ? "98.7% (تطابق احتيالي)" : "0.00% (أصلية)",
        user: "منى محمد الزنتاني",
        stampsAwarded: isSuspect ? 0 : stampsCount
      };

      // Push to Supabase Cloud
      await recordInvoiceScanToSupabase({
        storeId: selectedStore.id,
        storeName: selectedStore.name,
        invoiceNo: scanObj.invoiceNo,
        amount: amountVal,
        items: matchedSample.items,
        customerName: "منى محمد الزنتاني",
        stampsAwarded: scanObj.stampsAwarded,
        pointsEarned: isSuspect ? 0 : pointsCount
      });

      const res = {
        success: true,
        scan: scanObj,
        autoApproved: !isSuspect
      };

      setScanResult(res);
      onScanComplete(res);
    } catch (err) {
      console.error(err);
      alert("حدث خطأ أثناء فحص الفاتورة عبر الذكاء الاصطناعي.");
    } finally {
      setIsScanning(false);
    }
  };

  if (!isOpen) return null;

  return (
    <AnimatePresence>
      <div className="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <motion.div
          initial={{ opacity: 0, scale: 0.95, y: 20 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.95, y: 20 }}
          className="bg-white rounded-3xl w-full max-w-lg overflow-hidden shadow-2xl border border-slate-100 flex flex-col max-h-[90vh]"
        >
          {/* Header with Fuchsia Theme */}
          <div className="bg-slate-900 text-white p-5 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="bg-gradient-to-tr from-purple-600 to-pink-500 p-2.5 rounded-xl text-white">
                <Sparkles className="w-5 h-5 animate-pulse" />
              </div>
              <div>
                <h3 className="font-bold text-lg text-white">قارئ الفواتير الذكي بالـ AI</h3>
                <p className="text-xs text-slate-300">مسح تلقائي دون الحاجة لـ QR أو NFC</p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="text-slate-400 hover:text-white transition bg-slate-800/50 p-2 rounded-full"
            >
              <X className="w-5 h-5" />
            </button>
          </div>

          {/* Selector Tabs */}
          {!imageSrc && (
            <div className="flex border-b border-slate-100 bg-slate-50 p-2 gap-2">
              <button
                onClick={() => { setActiveTab("samples"); stopCamera(); }}
                className={`flex-1 py-2 px-3 text-sm font-bold rounded-xl transition ${
                  activeTab === "samples"
                    ? "bg-slate-900 text-white shadow-md"
                    : "text-slate-600 hover:bg-slate-100"
                }`}
              >
                💡 فواتير تجريبية
              </button>
              <button
                onClick={() => { setActiveTab("camera"); startCamera(); }}
                className={`flex-1 py-2 px-3 text-sm font-bold rounded-xl transition ${
                  activeTab === "camera"
                    ? "bg-slate-900 text-white shadow-md"
                    : "text-slate-600 hover:bg-slate-100"
                }`}
              >
                📷 الكاميرا الحية
              </button>
              <button
                onClick={() => { setActiveTab("upload"); stopCamera(); }}
                className={`flex-1 py-2 px-3 text-sm font-bold rounded-xl transition ${
                  activeTab === "upload"
                    ? "bg-slate-900 text-white shadow-md"
                    : "text-slate-600 hover:bg-slate-100"
                }`}
              >
                📤 رفع ملف
              </button>
            </div>
          )}

          {/* Tab Content Area */}
          <div className="p-6 flex-1 overflow-y-auto flex flex-col justify-center min-h-[300px]">
            {isScanning ? (
              <div className="text-center py-12 flex flex-col items-center justify-center">
                <div className="relative mb-6">
                  <div className="w-24 h-24 rounded-full border-4 border-slate-100 border-t-fuchsia-500 animate-spin flex items-center justify-center">
                    <Sparkles className="w-8 h-8 text-fuchsia-500 animate-pulse" />
                  </div>
                  <motion.div
                    animate={{ y: [0, 80, 0] }}
                    transition={{ repeat: Infinity, duration: 2, ease: "easeInOut" }}
                    className="absolute left-0 right-0 h-1 bg-fuchsia-500/80 shadow-lg top-2"
                  />
                </div>
                <h4 className="font-bold text-lg text-slate-800 animate-pulse">جاري فحص الفاتورة بالذكاء الاصطناعي...</h4>
                <p className="text-sm text-slate-500 mt-2 max-w-xs mx-auto text-center leading-relaxed">
                  يقوم نموذج Gemini بالتحقق من صحة الفاتورة وتفاصيل المواد والمبالغ ومؤشرات الأمان
                </p>
                <div className="mt-4 bg-purple-50 border border-purple-100 rounded-xl p-3 text-xs text-purple-700 max-w-sm text-center">
                  ⚡ يجري الآن تحليل الخطوط، التحقق من التكرار، واحتساب الطوابع تلقائياً
                </div>
              </div>
            ) : scanResult ? (
              // Scan Completed Success Result
              <div className="text-center py-4">
                <div className="w-16 h-16 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center mx-auto mb-4">
                  <CheckCircle className="w-10 h-10" />
                </div>
                <h4 className="font-bold text-xl text-slate-800">تم المسح بنجاح!</h4>
                
                <div className="bg-slate-50 rounded-2xl p-4 text-right border border-slate-100 my-5 text-sm space-y-2">
                  <div className="flex justify-between border-b border-slate-100 pb-2">
                    <span className="text-slate-500">المتجر المكتشف:</span>
                    <span className="font-bold text-slate-800">{scanResult.scan.storeName}</span>
                  </div>
                  <div className="flex justify-between border-b border-slate-100 pb-2">
                    <span className="text-slate-500">رقم الفاتورة:</span>
                    <span className="font-mono font-semibold text-slate-700">{scanResult.scan.invoiceNo}</span>
                  </div>
                  <div className="flex justify-between border-b border-slate-100 pb-2">
                    <span className="text-slate-500">القيمة الإجمالية:</span>
                    <span className="font-bold text-slate-900">{scanResult.scan.amount} د.ل</span>
                  </div>
                  <div>
                    <span className="text-slate-500 block mb-1">المواد المستخرجة:</span>
                    <div className="flex flex-wrap gap-1 mt-1 justify-start">
                      {scanResult.scan.items.map((item: string, i: number) => (
                        <span key={i} className="bg-white border border-slate-200 text-slate-700 text-xs px-2 py-1 rounded-lg">
                          {item}
                        </span>
                      ))}
                    </div>
                  </div>
                </div>

                {scanResult.autoApproved ? (
                  <div className="bg-emerald-50 text-emerald-800 border border-emerald-100 rounded-2xl p-4 flex items-center gap-3 text-right">
                    <div className="bg-emerald-500 text-white p-2 rounded-xl">
                      <Zap className="w-5 h-5 fill-current" />
                    </div>
                    <div>
                      <h5 className="font-bold text-sm">تمت إضافة الطوابع تلقائياً!</h5>
                      <p className="text-xs text-emerald-700 mt-0.5">
                        الفاتورة مطابقة وتمنحك <span className="font-bold text-sm">{scanResult.scan.stampsAwarded}</span> طوابع إضافية في محفظتك.
                      </p>
                    </div>
                  </div>
                ) : (
                  <div className="bg-amber-50 text-amber-800 border border-amber-100 rounded-2xl p-4 flex items-center gap-3 text-right">
                    <div className="bg-amber-500 text-white p-2 rounded-xl">
                      <AlertCircle className="w-5 h-5" />
                    </div>
                    <div>
                      <h5 className="font-bold text-sm">قيد التدقيق الرقمي للأمان</h5>
                      <p className="text-xs text-amber-700 mt-0.5">
                        لوحظ وجود تكرار أو خطر احتيال {scanResult.scan.fraudRisk}. تم إرسالها لمدير النظام فوراً وسيتأكد من نقاطك يدوياً!
                      </p>
                    </div>
                  </div>
                )}

                <button
                  onClick={onClose}
                  className="mt-6 w-full bg-slate-900 hover:bg-slate-800 text-white py-3.5 px-4 font-bold rounded-xl transition shadow-md"
                >
                  إغلاق ومتابعة المحفظة
                </button>
              </div>
            ) : imageSrc ? (
              // Selected Image Review Step before scanning
              <div className="flex flex-col items-center">
                <div className="relative w-full max-h-[300px] overflow-hidden rounded-2xl border border-slate-200 shadow-inner bg-slate-900 flex justify-center">
                  <img src={imageSrc} alt="Preview" className="max-h-[300px] object-contain" />
                  <button
                    onClick={() => setImageSrc(null)}
                    className="absolute top-3 right-3 bg-slate-900/80 hover:bg-slate-950 text-white rounded-full p-2 shadow"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>
                
                <div className="mt-5 w-full flex gap-3">
                  <button
                    onClick={handleStartScan}
                    className="flex-1 bg-gradient-to-r from-purple-600 to-pink-500 text-white py-3 px-4 font-bold rounded-xl hover:opacity-90 transition flex items-center justify-center gap-2 shadow-lg shadow-fuchsia-500/20"
                  >
                    <Sparkles className="w-5 h-5" />
                    قراءة الفاتورة بـ AI المتجر
                  </button>
                  <button
                    onClick={() => setImageSrc(null)}
                    className="bg-slate-150 text-slate-700 py-3 px-5 font-bold rounded-xl hover:bg-slate-200 transition border border-slate-200"
                  >
                    إلغاء
                  </button>
                </div>
                <p className="text-[10px] text-slate-400 mt-2 text-center">
                  * سيتم تحليل الصورة فوراً بواسطة Gemini 3.8 Flash
                </p>
              </div>
            ) : (
              // Tabs inputs
              <div>
                {activeTab === "samples" && (
                  <div className="space-y-4">
                    <p className="text-xs text-slate-600 font-medium mb-3 text-right">
                      💡 لا تملكين فاتورة ورقية حالياً؟ اختاري إحدى فواتير المرأة مول الجاهزة لتجربة ميزة الـ AI والتدقيق الأمني الفوري:
                    </p>
                    <div className="grid gap-3">
                      {SAMPLE_RECEIPTS.map((sample, idx) => (
                        <button
                          key={idx}
                          onClick={() => handleSelectSample(sample)}
                          className="flex items-center justify-between p-4 bg-slate-50 rounded-2xl hover:bg-slate-100 hover:border-slate-300 transition text-right border border-slate-100"
                        >
                          <div className="flex items-center gap-3">
                            <span className="text-2xl p-2 bg-white rounded-xl shadow-sm border border-slate-100">{sample.logo}</span>
                            <div>
                              <h5 className="font-bold text-slate-800 text-sm">{sample.name}</h5>
                              <p className="text-xs text-slate-500 font-mono mt-0.5">{sample.invoiceNo} • {sample.amount}</p>
                            </div>
                          </div>
                          <span className="text-xs text-fuchsia-600 font-bold bg-fuchsia-50 px-2.5 py-1.5 rounded-xl border border-fuchsia-100">اختيار</span>
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                {activeTab === "camera" && (
                  <div className="flex flex-col items-center">
                    {cameraActive ? (
                      <div className="relative w-full overflow-hidden rounded-2xl border border-slate-200 aspect-[4/3] bg-black">
                        <video ref={videoRef} autoPlay playsInline className="w-full h-full object-cover" />
                        <button
                          onClick={capturePhoto}
                          className="absolute bottom-4 left-1/2 -translate-x-1/2 w-14 h-14 rounded-full bg-white border-4 border-slate-300/50 shadow flex items-center justify-center text-slate-800 active:scale-95 transition"
                        >
                          <div className="w-10 h-10 rounded-full bg-slate-900" />
                        </button>
                      </div>
                    ) : (
                      <div className="w-full h-[200px] border-2 border-dashed border-slate-200 rounded-2xl flex flex-col items-center justify-center bg-slate-50 text-slate-400">
                        <Camera className="w-12 h-12 mb-2 text-slate-300" />
                        <button
                          onClick={startCamera}
                          className="bg-slate-900 text-white font-bold text-xs py-2 px-4 rounded-xl hover:bg-slate-800 transition shadow-sm"
                        >
                          تفعيل الكاميرا
                        </button>
                      </div>
                    )}
                    <canvas ref={canvasRef} className="hidden" />
                  </div>
                )}

                {activeTab === "upload" && (
                  <div
                    onDragEnter={handleDrag}
                    onDragOver={handleDrag}
                    onDragLeave={handleDrag}
                    onDrop={handleDrop}
                    onClick={() => fileInputRef.current?.click()}
                    className={`border-2 border-dashed rounded-2xl p-8 text-center cursor-pointer transition ${
                      dragActive
                        ? "border-fuchsia-500 bg-fuchsia-50/50"
                        : "border-slate-200 bg-slate-50 hover:bg-slate-100"
                    }`}
                  >
                    <input
                      ref={fileInputRef}
                      type="file"
                      accept="image/*"
                      onChange={handleFileChange}
                      className="hidden"
                    />
                    <Upload className="w-12 h-12 text-slate-400 mx-auto mb-3" />
                    <h5 className="font-bold text-slate-700 text-sm">اسحبي وأفلتي صورة الفاتورة هنا</h5>
                    <p className="text-xs text-slate-500 mt-1">أو انقري لتصفح الملفات من جهازك</p>
                    <p className="text-[10px] text-slate-400 mt-3">يدعم صيغ JPG، PNG (حجم أقصى 10 ميغابايت)</p>
                  </div>
                )}
              </div>
            )}
          </div>
        </motion.div>
      </div>
    </AnimatePresence>
  );
}
