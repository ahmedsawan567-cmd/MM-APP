import React, { useState } from "react";
import { 
  TrendingUp, Users, AlertTriangle, ShieldCheck, CheckCircle, 
  XCircle, Filter, FileText, Search, Plus, MapPin, DollarSign, 
  BarChart2, Settings, ListFilter, PlayCircle, Loader2, Award, ExternalLink
} from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import { 
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer 
} from "recharts";
import { Store, ScanRecord, AdminStats } from "../types";

interface AdminPortalProps {
  stores: Store[];
  scans: ScanRecord[];
  stats: AdminStats;
  onRefresh: () => void;
}

export default function AdminPortal({ stores, scans, stats, onRefresh }: AdminPortalProps) {
  const [activeTab, setActiveTab] = useState<"dashboard" | "audit" | "stores">("dashboard");
  const [selectedScanFilter, setSelectedScanFilter] = useState<string>("All");
  const [auditQuery, setAuditQuery] = useState("");
  const [processingId, setProcessingId] = useState<string | null>(null);

  // Filter Scans for the Audit queue
  const filteredScans = scans.filter(scan => {
    const matchesStatus = selectedScanFilter === "All" || scan.status === selectedScanFilter;
    const matchesSearch = scan.storeName.toLowerCase().includes(auditQuery.toLowerCase()) || 
                          scan.invoiceNo.toLowerCase().includes(auditQuery.toLowerCase()) ||
                          scan.user.toLowerCase().includes(auditQuery.toLowerCase());
    return matchesStatus && matchesSearch;
  });

  const handleAuditAction = async (scanId: string, action: "approve" | "reject") => {
    setProcessingId(scanId);
    try {
      const res = await fetch(`/api/admin/scans/${scanId}/action`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action })
      });
      const data = await res.json();
      if (data.success) {
        onRefresh();
      }
    } catch (err) {
      console.error(err);
    } finally {
      setProcessingId(null);
    }
  };

  return (
    <div className="bg-[#0B132B] text-slate-100 min-h-screen rounded-3xl overflow-hidden shadow-2xl border border-slate-800/80 flex flex-col font-sans">
      
      {/* Top Banner & Navigation Header */}
      <div className="bg-slate-950 p-6 border-b border-slate-900 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="bg-gradient-to-r from-sky-600 to-indigo-600 text-white font-bold px-2.5 py-0.5 rounded-lg text-xs tracking-wider uppercase flex items-center gap-1.5">
              <ShieldCheck className="w-3.5 h-3.5" />
              <span>الإدارة والرقابة</span>
            </span>
            <h1 className="font-extrabold text-2xl tracking-tight text-white">المرأة مول | منصة الإدارة والتدقيق الرقمي</h1>
          </div>
          <p className="text-xs text-slate-400 mt-1">
            منظومة إدارة برامج الولاء، التدقيق المالي بالذكاء الاصطناعي، وتسويات المتاجر المركزية
          </p>
        </div>

        {/* Tab Selection */}
        <div className="flex bg-slate-900 p-1.5 rounded-xl border border-slate-800 gap-1 self-stretch md:self-auto">
          <button
            onClick={() => setActiveTab("dashboard")}
            className={`flex-1 md:flex-none py-2 px-4 text-xs font-bold rounded-lg transition-all flex items-center justify-center gap-2 ${
              activeTab === "dashboard"
                ? "bg-slate-800 text-white shadow-sm border border-slate-700"
                : "text-slate-400 hover:text-slate-200"
            }`}
          >
            <BarChart2 className="w-4 h-4 text-sky-400" />
            <span>لوحة المؤشرات</span>
          </button>
          <button
            onClick={() => setActiveTab("audit")}
            className={`flex-1 md:flex-none py-2 px-4 text-xs font-bold rounded-lg transition-all flex items-center justify-center gap-2 relative ${
              activeTab === "audit"
                ? "bg-slate-800 text-white shadow-sm border border-slate-700"
                : "text-slate-400 hover:text-slate-200"
            }`}
          >
            <ShieldCheck className="w-4 h-4 text-amber-400" />
            <span>طابور التدقيق</span>
            {scans.filter(s => s.status === "Pending").length > 0 && (
              <span className="absolute -top-1 -left-1 w-5 h-5 bg-amber-500 text-white text-[10px] font-bold rounded-full flex items-center justify-center border border-slate-950 animate-pulse">
                {scans.filter(s => s.status === "Pending").length}
              </span>
            )}
          </button>
          <button
            onClick={() => setActiveTab("stores")}
            className={`flex-1 md:flex-none py-2 px-4 text-xs font-bold rounded-lg transition-all flex items-center justify-center gap-2 ${
              activeTab === "stores"
                ? "bg-slate-800 text-white shadow-sm border border-slate-700"
                : "text-slate-400 hover:text-slate-200"
            }`}
          >
            <Settings className="w-4 h-4 text-indigo-400" />
            <span>دليل المتاجر</span>
          </button>
        </div>
      </div>

      {/* Main Content Body */}
      <div className="flex-1 p-6 md:p-8 overflow-y-auto">
        <AnimatePresence mode="wait">
          
          {activeTab === "dashboard" && (
            <motion.div
              key="dashboard"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="space-y-8 text-right"
            >
              {/* Analytics Metric Cards Grid */}
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
                
                <div className="bg-slate-950 p-6 rounded-2xl border border-slate-850 shadow-lg relative overflow-hidden group">
                  <div className="absolute top-0 right-0 w-24 h-24 bg-purple-500/5 rounded-full -mr-8 -mt-8" />
                  <div className="flex justify-between items-start">
                    <span className="text-xs font-bold text-slate-500">مبيعات المتاجر المعتمدة</span>
                    <span className="p-2 bg-purple-500/10 text-purple-400 rounded-xl">
                      <TrendingUp className="w-5 h-5" />
                    </span>
                  </div>
                  <h3 className="font-extrabold text-2xl text-white mt-4 font-mono">
                    {stats.totalSales.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} د.ل
                  </h3>
                  <p className="text-[10px] text-emerald-400 mt-2 font-semibold">⚡ +18.7% هذا الأسبوع بالجملة</p>
                </div>

                <div className="bg-slate-950 p-6 rounded-2xl border border-slate-850 shadow-lg relative overflow-hidden group">
                  <div className="absolute top-0 right-0 w-24 h-24 bg-pink-500/5 rounded-full -mr-8 -mt-8" />
                  <div className="flex justify-between items-start">
                    <span className="text-xs font-bold text-slate-500">نقاط الولاء الممنوحة</span>
                    <span className="p-2 bg-pink-500/10 text-pink-400 rounded-xl">
                      <Award className="w-5 h-5" />
                    </span>
                  </div>
                  <h3 className="font-extrabold text-2xl text-white mt-4 font-mono">
                    {stats.totalPoints.toLocaleString()} نقطة
                  </h3>
                  <p className="text-[10px] text-slate-400 mt-2">معدل 100 نقطة لكل ستامبا مكتملة</p>
                </div>

                <div className="bg-slate-950 p-6 rounded-2xl border border-slate-850 shadow-lg relative overflow-hidden group">
                  <div className="absolute top-0 right-0 w-24 h-24 bg-fuchsia-500/5 rounded-full -mr-8 -mt-8" />
                  <div className="flex justify-between items-start">
                    <span className="text-xs font-bold text-slate-500">طابور تدقيق الاحتيال</span>
                    <span className="p-2 bg-fuchsia-500/10 text-fuchsia-400 rounded-xl">
                      <AlertTriangle className="w-5 h-5" />
                    </span>
                  </div>
                  <h3 className="font-extrabold text-2xl text-white mt-4 font-mono">
                    {stats.pendingAudits} فواتير معلقة
                  </h3>
                  <p className="text-[10px] text-amber-400 mt-2 font-semibold">⚠️ تتطلب فحصاً أمنياً يدوياً</p>
                </div>

                <div className="bg-slate-950 p-6 rounded-2xl border border-slate-850 shadow-lg relative overflow-hidden group">
                  <div className="absolute top-0 right-0 w-24 h-24 bg-blue-500/5 rounded-full -mr-8 -mt-8" />
                  <div className="flex justify-between items-start">
                    <span className="text-xs font-bold text-slate-500">عمليات فحص الفواتير اليوم</span>
                    <span className="p-2 bg-blue-500/10 text-blue-400 rounded-xl">
                      <CheckCircle className="w-5 h-5" />
                    </span>
                  </div>
                  <h3 className="font-extrabold text-2xl text-white mt-4 font-mono">
                    {stats.dailyScans} عمليات مسح
                  </h3>
                  <p className="text-[10px] text-emerald-400 mt-2 font-semibold">✓ كفاءة قراءة Gemini: 99.1%</p>
                </div>

              </div>

              {/* Math / Statistical Analysis Section */}
              <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
                
                {/* Poisson Distribution Chart - Recharts */}
                <div className="bg-slate-950 p-6 rounded-2xl border border-slate-850 shadow-lg lg:col-span-2">
                  <div className="flex justify-between items-center mb-6">
                    <div>
                      <h4 className="font-bold text-base text-white">توزيع بواسون الإحصائي لوصول المتسوقين الفاخر</h4>
                      <p className="text-xs text-slate-500 mt-0.5">
                        تحليل الفاصل الزمني ومعدل كثافة المعاملات مقارنة بالتوزيع النظري المستهدف للبوتيكات (λ = 3.8)
                      </p>
                    </div>
                    <span className="bg-slate-900 border border-slate-800 text-xs font-mono px-3 py-1 rounded-lg text-slate-300">
                      λ = {stats.poissonArrivalRate} معاملة / ساعة
                    </span>
                  </div>

                  {/* Recharts BarChart container with purple accents */}
                  <div className="h-[280px] w-full">
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart
                        data={stats.distributionData}
                        margin={{ top: 10, right: 10, left: -10, bottom: 0 }}
                      >
                        <CartesianGrid strokeDasharray="3 3" stroke="#1E293B" />
                        <XAxis dataKey="k" stroke="#64748B" fontSize={11} />
                        <YAxis stroke="#64748B" fontSize={11} />
                        <Tooltip 
                          contentStyle={{ backgroundColor: '#020617', borderColor: '#1E293B', color: '#fff', textAlign: 'right' }} 
                        />
                        <Legend wrapperStyle={{ fontSize: '11px', paddingTop: '10px' }} />
                        <Bar name="الوصول الفعلي المكتشف" dataKey="actual" fill="#d946ef" radius={[4, 4, 0, 0]} />
                        <Bar name="الوصول النظري المستهدف" dataKey="expected" fill="#8b5cf6" radius={[4, 4, 0, 0]} />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>

                {/* Mathematical Anomaly Checker Details */}
                <div className="bg-slate-950 p-6 rounded-2xl border border-slate-850 shadow-lg flex flex-col justify-between">
                  <div>
                    <h4 className="font-bold text-base text-white mb-1">النمذجة الرياضية لتدقيق الفواتير</h4>
                    <p className="text-xs text-slate-500">
                      يتم تتبع سلة التسوق وتحليل الانحرافات لحجب الفواتير المكررة والمشبوهة رياضياً بالذكاء الاصطناعي
                    </p>

                    <div className="mt-6 space-y-4">
                      
                      <div className="flex justify-between items-center p-3 bg-slate-900 rounded-xl border border-slate-800">
                        <div className="text-right">
                          <span className="text-[10px] text-slate-500 block">المتوسط الحسابي للسلة (Mean Basket)</span>
                          <span className="text-sm font-bold text-white font-mono">{stats.meanBasketSize} د.ل</span>
                        </div>
                        <span className="text-slate-600">μ</span>
                      </div>

                      <div className="flex justify-between items-center p-3 bg-slate-900 rounded-xl border border-slate-800">
                        <div className="text-right">
                          <span className="text-[10px] text-slate-500 block">الانحراف المعياري (Std Dev)</span>
                          <span className="text-sm font-bold text-white font-mono">{stats.standardDeviation} د.ل</span>
                        </div>
                        <span className="text-slate-600">σ</span>
                      </div>

                      <div className="flex justify-between items-center p-3 bg-slate-900 rounded-xl border border-slate-800">
                        <div className="text-right">
                          <span className="text-[10px] text-slate-500 block">درجة المعيار Z-Score للتحذير</span>
                          <span className="text-sm font-bold text-fuchsia-400 font-mono">Z &gt; {stats.zScore}</span>
                        </div>
                        <span className="text-fuchsia-500">Z</span>
                      </div>

                    </div>
                  </div>

                  <div className="mt-6 pt-4 border-t border-slate-850 text-xs text-slate-400 leading-relaxed text-right">
                    ⚡ يحسب النظام تلقائياً البصمة الرقمية للخطوط (pHash Verification) ويقارنها بأنماط الإيصالات المعتمدة لكشف التلاعب بالفوتوشوب في أجزاء من الثانية.
                  </div>
                </div>

              </div>

              {/* Verified Vendors overview table */}
              <div className="bg-slate-950 p-6 rounded-2xl border border-slate-850 shadow-lg">
                <h4 className="font-bold text-base text-white mb-4">ملخص نشاط البوتيكات النسائية والمحلات المعتمدة</h4>
                <div className="overflow-x-auto">
                  <table className="w-full text-right text-xs">
                    <thead>
                      <tr className="border-b border-slate-800 text-slate-500 font-bold">
                        <th className="pb-3 pt-1">اسم البوتيك</th>
                        <th className="pb-3 pt-1">الفئة التجارية</th>
                        <th className="pb-3 pt-1">الشركة المالكة / المورد</th>
                        <th className="pb-3 pt-1">شروط الستامبا</th>
                        <th className="pb-3 pt-1 text-center">الحد الأدنى للطلب (MOQ)</th>
                        <th className="pb-3 pt-1 text-left">الحالة</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-900">
                      {stores.map((store) => (
                        <tr key={store.id} className="text-slate-300 hover:bg-slate-900/40 transition">
                          <td className="py-3 font-semibold text-white flex items-center gap-2">
                            <span>{store.logo}</span>
                            <span>{store.name}</span>
                          </td>
                          <td className="py-3">{store.category}</td>
                          <td className="py-3 text-slate-400">{store.supplierName}</td>
                          <td className="py-3 text-fuchsia-400 font-semibold">{store.maxStamps} طوابع ➔ {store.rewardText}</td>
                          <td className="py-3 text-center font-mono text-fuchsia-300">{store.moq}</td>
                          <td className="py-3 text-left">
                            <span className="bg-emerald-500/10 text-emerald-400 px-2.5 py-1 rounded-full text-[10px] font-bold">
                              متصل ونشط
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </motion.div>
          )}

          {activeTab === "audit" && (
            <motion.div
              key="audit"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="space-y-6 text-right"
            >
              <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-950 p-5 rounded-2xl border border-slate-850">
                <div>
                  <h4 className="font-bold text-base text-white">فحص وإجازة الفواتير الرقمية</h4>
                  <p className="text-xs text-slate-500 mt-0.5">
                    قم بمراجعة الفواتير التي قام نموذج الذكاء الاصطناعي بتعليقها للاشتباه في الاحتيال أو الفحص الأمني
                  </p>
                </div>

                <div className="flex flex-wrap gap-2 self-stretch md:self-auto">
                  <button
                    onClick={() => setSelectedScanFilter("All")}
                    className={`py-1.5 px-3 rounded-lg text-xs font-bold transition-all ${
                      selectedScanFilter === "All" ? "bg-slate-800 text-white" : "bg-slate-900 text-slate-400 hover:text-slate-200"
                    }`}
                  >
                    الكل ({scans.length})
                  </button>
                  <button
                    onClick={() => setSelectedScanFilter("Pending")}
                    className={`py-1.5 px-3 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${
                      selectedScanFilter === "Pending" ? "bg-fuchsia-600 text-white shadow-md shadow-fuchsia-500/10" : "bg-slate-900 text-pink-500 hover:bg-slate-850"
                    }`}
                  >
                    <span className="w-1.5 h-1.5 rounded-full bg-pink-400 animate-ping" />
                    المعلقة بالتدقيق ({scans.filter(s => s.status === "Pending").length})
                  </button>
                  <button
                    onClick={() => setSelectedScanFilter("Approved")}
                    className={`py-1.5 px-3 rounded-lg text-xs font-bold transition-all ${
                      selectedScanFilter === "Approved" ? "bg-emerald-500 text-white" : "bg-slate-900 text-emerald-400 hover:bg-slate-850"
                    }`}
                  >
                    المعتمدة ({scans.filter(s => s.status === "Approved").length})
                  </button>
                  <button
                    onClick={() => setSelectedScanFilter("Flagged")}
                    className={`py-1.5 px-3 rounded-lg text-xs font-bold transition-all ${
                      selectedScanFilter === "Flagged" ? "bg-rose-500 text-white" : "bg-slate-900 text-rose-400 hover:bg-slate-850"
                    }`}
                  >
                    المرفوضة ({scans.filter(s => s.status === "Flagged").length})
                  </button>
                </div>
              </div>

              {/* Search Audit field */}
              <div className="relative">
                <input
                  type="text"
                  placeholder="ابحث برقم الفاتورة، اسم المتجر، أو اسم العميل..."
                  value={auditQuery}
                  onChange={(e) => setAuditQuery(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-850 rounded-xl py-3 pr-11 pl-4 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-fuchsia-500 transition-all text-right text-white"
                />
                <Search className="absolute right-4 top-1/2 -translate-y-1/2 text-slate-500 w-4.5 h-4.5" />
              </div>

              {/* Scans list audit view */}
              {filteredScans.length === 0 ? (
                <div className="bg-slate-950 p-12 text-center rounded-2xl border border-slate-850 text-slate-500">
                  <FileText className="w-12 h-12 mx-auto mb-3 opacity-40 text-slate-600" />
                  <p className="text-sm font-semibold">لا توجد فواتير مطابقة لخيارات البحث الحالية</p>
                </div>
              ) : (
                <div className="grid gap-4">
                  {filteredScans.map((scan) => (
                    <div
                      key={scan.id}
                      className="bg-slate-950 rounded-2xl p-5 border border-slate-850 shadow-md flex flex-col lg:flex-row justify-between items-start lg:items-center gap-6 text-right"
                    >
                      <div className="space-y-2 flex-1">
                        <div className="flex flex-wrap items-center gap-3">
                          <span className="font-mono text-xs font-bold text-slate-400 bg-slate-900 px-2 py-1 rounded">
                            {scan.id}
                          </span>
                          <h5 className="font-bold text-white text-base">{scan.storeName}</h5>
                          <span className="text-xs text-slate-500">•</span>
                          <span className="text-xs text-slate-400 font-semibold">{scan.user}</span>
                          <span className="text-xs text-slate-500">•</span>
                          <span className="text-xs text-slate-500 font-mono">{scan.date}</span>
                        </div>

                        {/* Extracted items */}
                        <div className="flex flex-wrap gap-1.5 mt-2 justify-start">
                          {scan.items.map((item, i) => (
                            <span key={i} className="bg-slate-900 text-slate-300 border border-slate-800 text-[11px] px-2 py-0.5 rounded-lg">
                              {item}
                            </span>
                          ))}
                        </div>

                        {/* Fraud risk banner */}
                        <div className="flex items-center gap-4 mt-2 pt-2 border-t border-slate-900">
                          <div className="flex items-center gap-1.5 text-xs">
                            <span className="text-slate-500">مستوى الاشتباه (Fraud Risk):</span>
                            <span className={`font-extrabold px-2 py-0.5 rounded text-[10px] ${
                              scan.fraudRisk === "High"
                                ? "bg-rose-500/10 text-rose-400 border border-rose-500/20"
                                : scan.fraudRisk === "Medium"
                                  ? "bg-amber-500/10 text-amber-400 border border-amber-500/20"
                                  : "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                            }`}>
                              {scan.fraudRisk === "High" ? "مرتفع جداً 🔴" : scan.fraudRisk === "Medium" ? "متوسط 🟡" : "آمن وضئيل 🟢"}
                            </span>
                          </div>

                          <div className="flex items-center gap-1.5 text-xs text-slate-400">
                            <span>فحص التكرار:</span>
                            <span className="font-medium text-slate-300 font-mono">{scan.pHashMatch}</span>
                          </div>
                        </div>
                      </div>

                      {/* Right side Amount and Actions */}
                      <div className="flex flex-row lg:flex-col items-center lg:items-end justify-between lg:justify-center gap-4 self-stretch lg:self-auto border-t lg:border-t-0 pt-4 lg:pt-0 border-slate-900">
                        <div className="text-right">
                          <span className="text-[10px] text-slate-500 block">قيمة الفاتورة</span>
                          <span className="text-lg font-bold text-white font-mono">{scan.amount} د.ل</span>
                        </div>

                        {/* State & Audit Controls */}
                        {scan.status === "Pending" ? (
                          <div className="flex gap-2">
                            <button
                              disabled={processingId !== null}
                              onClick={() => handleAuditAction(scan.id, "approve")}
                              className="bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs py-2 px-3 rounded-lg transition-all flex items-center gap-1 shadow-md shadow-emerald-600/10"
                            >
                              {processingId === scan.id ? (
                                <Loader2 className="w-3.5 h-3.5 animate-spin" />
                              ) : (
                                <CheckCircle className="w-3.5 h-3.5" />
                              )}
                              <span>اعتماد الفاتورة</span>
                            </button>
                            <button
                              disabled={processingId !== null}
                              onClick={() => handleAuditAction(scan.id, "reject")}
                              className="bg-slate-800 hover:bg-rose-900 hover:text-white text-slate-400 font-bold text-xs py-2 px-3 rounded-lg transition-all flex items-center gap-1 border border-slate-700"
                            >
                              <XCircle className="w-3.5 h-3.5" />
                              <span>رفض وتجميد</span>
                            </button>
                          </div>
                        ) : (
                          <div className="flex items-center gap-1.5">
                            <span className={`text-xs font-bold px-3 py-1.5 rounded-xl ${
                              scan.status === "Approved"
                                ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                                : "bg-rose-500/10 text-rose-400 border border-rose-500/20"
                            }`}>
                              {scan.status === "Approved" ? "✓ معتمدة وممنوحة" : "🗙 مرفوضة ومجمدة"}
                            </span>
                          </div>
                        )}
                      </div>

                    </div>
                  ))}
                </div>
              )}
            </motion.div>
          )}

          {activeTab === "stores" && (
            <motion.div
              key="stores"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="space-y-6 text-right"
            >
              <div className="bg-slate-950 p-6 rounded-2xl border border-slate-850 flex justify-between items-center">
                <div>
                  <h4 className="font-bold text-base text-white">إعدادات وقواعد منح طوابع الولاء</h4>
                  <p className="text-xs text-slate-500 mt-0.5">
                    تحكم في شروط الستامبا ومطابقة الحد الأدنى وقيم المكافآت للبوتيكات ومحلات التجميل
                  </p>
                </div>
                <button
                  onClick={() => alert("ميزة إضافة شروط بوتيك جديد ستتوفر في التحديث القادم!")}
                  className="bg-gradient-to-r from-purple-600 to-pink-500 text-white font-bold text-xs py-2.5 px-4 rounded-xl hover:opacity-90 transition flex items-center gap-2 shadow-md shadow-fuchsia-500/10"
                >
                  <Plus className="w-4 h-4" />
                  <span>إضافة بوتيك جديد</span>
                </button>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                {stores.map((store) => (
                  <div
                    key={store.id}
                    className="bg-slate-950 rounded-2xl p-5 border border-slate-850 shadow-md flex flex-col justify-between"
                  >
                    <div>
                      <div className="flex justify-between items-start">
                        <span className="text-3xl p-2 bg-slate-900 rounded-xl border border-slate-800">
                          {store.logo}
                        </span>
                        <span className="text-[10px] font-bold text-fuchsia-500 bg-fuchsia-500/5 px-2.5 py-1 rounded-full border border-fuchsia-500/10">
                          {store.category}
                        </span>
                      </div>

                      <h5 className="font-bold text-white text-base mt-4">{store.name}</h5>
                      <p className="text-xs text-slate-500 mt-1">{store.supplierName}</p>

                      <div className="mt-5 space-y-2 text-xs">
                        <div className="flex justify-between py-1.5 border-b border-slate-900">
                          <span className="text-slate-500">حد الفاتورة الأدنى (MOQ):</span>
                          <span className="font-mono text-fuchsia-400 font-bold">{store.moq}</span>
                        </div>
                        <div className="flex justify-between py-1.5 border-b border-slate-900">
                          <span className="text-slate-500">الطوابع الكلية:</span>
                          <span className="font-mono text-white">{store.maxStamps} طوابع</span>
                        </div>
                        <div className="flex justify-between py-1.5">
                          <span className="text-slate-500">المكافأة:</span>
                          <span className="font-bold text-fuchsia-400">{store.rewardText}</span>
                        </div>
                      </div>
                    </div>

                    <div className="mt-6 pt-4 border-t border-slate-900 flex gap-2">
                      <button
                        onClick={() => alert(`تعديل إعدادات ${store.name}`)}
                        className="flex-1 bg-slate-900 hover:bg-slate-850 text-slate-300 font-bold text-xs py-2 rounded-xl transition border border-slate-800"
                      >
                        تعديل الشروط
                      </button>
                      <button
                        onClick={() => alert(`مراقبة مبيعات ${store.name}`)}
                        className="bg-slate-900 hover:bg-slate-850 text-fuchsia-500 p-2 rounded-xl transition border border-slate-800"
                        title="عرض لوحة البائع"
                      >
                        <ExternalLink className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </motion.div>
          )}

        </AnimatePresence>
      </div>

    </div>
  );
}
