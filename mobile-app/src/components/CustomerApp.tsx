import React, { useState, useEffect } from "react";
import { 
  Search, Bell, User, Wallet, Store as StoreIcon, Plus, Info, 
  MapPin, Calendar, Mail, Phone, ShieldCheck, Trash2, Award, 
  Zap, LogOut, CheckCircle, RefreshCw, AlertCircle, Settings, Sliders, Eye
} from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import { Store, UserProfile, NotificationItem } from "../types";
import CameraModal from "./CameraModal";

interface CustomerAppProps {
  profile: UserProfile;
  stores: Store[];
  notifications: NotificationItem[];
  onRefresh: () => void;
  onProfileUpdate: (newProfile: Partial<UserProfile>) => void;
  onLogout: () => void;
  onSwitchToAdmin?: () => void;
}

export default function CustomerApp({ profile, stores, notifications, onRefresh, onProfileUpdate, onLogout, onSwitchToAdmin }: CustomerAppProps) {
  const [activeTab, setActiveTab] = useState<"home" | "wallet" | "notifications" | "profile">("home");
  const [selectedCategory, setSelectedCategory] = useState<string>("الكل");
  const [isCameraOpen, setIsCameraOpen] = useState(false);
  const [selectedStoreForScan, setSelectedStoreForScan] = useState<Store | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [profileForm, setProfileForm] = useState({ ...profile });
  const [successMsg, setSuccessMsg] = useState("");
  const [selectedCardId, setSelectedCardId] = useState<string | null>("emerald-fashion");

  // Settings states with persistence
  const [isMemberCardOpen, setIsMemberCardOpen] = useState(false);
  const [settingsTab, setSettingsTab] = useState<"profile_edit" | "preferences">("profile_edit");
  const [biometricsEnabled, setBiometricsEnabled] = useState(true);
  const [vibrationsEnabled, setVibrationsEnabled] = useState(true);
  const [isDarkMode, setIsDarkMode] = useState(false);
  const [appLanguage, setAppLanguage] = useState("العربية");

  useEffect(() => {
    setProfileForm({ ...profile });
  }, [profile]);

  const categories = [
    "الكل",
    "مواد تنظيف وعناية",
    "مواد غذائية وتموين",
    "فساتين سهرة وأزياء",
    "مقاهي ومشروبات",
    "ملابس نسائية",
    "ملابس وألعاب أطفال",
    "شوكولاتة وحلويات",
    "مطاعم ومأكولات",
    "ألعاب وإلكترونيات"
  ];

  // Static offers data for high-graphic showcase
  const mallOffers = [
    {
      id: "offer-evening-dress",
      storeName: "بوتيك فساتين السهرة والأزياء الراقية",
      logo: "👗",
      title: "عرض السهرات والأناقة الفاخرة",
      description: "احصلي على ضعف الطوابع (2x Stamps) عند الشراء بأكثر من 400 د.ل لجميع تشكيلات الفساتين والعباءات الراقية.",
      badge: "عرض مضاعف 🔥",
      gradient: "from-purple-950/95 via-fuchsia-950/90 to-transparent",
      image: "https://images.unsplash.com/photo-1595777457583-95e059d581b8?auto=format&fit=crop&w=350&q=80"
    },
    {
      id: "offer-cleaning",
      storeName: "مواد التنظيف الفاخرة والعناية",
      logo: "🧼",
      title: "مهرجان النظافة والعناية المنزلية",
      description: "باقة منظفات ومعقمات فاخرة مجانية عند جمع 10 طوابع من قسم مواد التنظيف والعناية المنزلية.",
      badge: "هدية فورية 🎁",
      gradient: "from-pink-950/95 via-rose-950/90 to-transparent",
      image: "https://images.unsplash.com/photo-1585670270608-b4b4f1da0d00?auto=format&fit=crop&w=350&q=80"
    },
    {
      id: "offer-kadi-cafe",
      storeName: "كافي كادي (Kadi Café)",
      logo: "☕",
      title: "قهوتك وحلواكِ علينا",
      description: "اجمعي 6 طوابع فقط واحصلي على فطور وسبانش لاتيه بارد وتشيز كيك توت مجاناً بالكامل.",
      badge: "عرض حصري ☕",
      gradient: "from-indigo-950/95 via-purple-950/90 to-transparent",
      image: "https://images.unsplash.com/photo-1501339847302-ac426a4a7cbb?auto=format&fit=crop&w=350&q=80"
    },
    {
      id: "offer-chocolate",
      storeName: "قصر الشكلاطة والحلويات الفاخرة",
      logo: "🍫",
      title: "صندوق الشوكولاتة البلجيكية",
      description: "اشتري أي تشكيلة ضيافة واحصلي على طابعين إضافيين مع صندوق شوكولاتة بلجيكية فاخرة مجاناً.",
      badge: "جديد وحصري ✨",
      gradient: "from-amber-950/95 via-yellow-950/90 to-transparent",
      image: "https://images.unsplash.com/photo-1549007994-cb92caebd54b?auto=format&fit=crop&w=350&q=80"
    }
  ];

  // Filter stores based on categories and search query
  const filteredStores = stores.filter(store => {
    const matchesCategory = selectedCategory === "الكل" || store.category === selectedCategory;
    const matchesSearch = store.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
                          store.supplierName.toLowerCase().includes(searchQuery.toLowerCase());
    return matchesCategory && matchesSearch;
  });

  const handleProfileSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const res = await fetch("/api/profile", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(profileForm)
      });
      const data = await res.json();
      if (data.success) {
        onProfileUpdate(profileForm);
        setSuccessMsg("تم حفظ البيانات الشخصية بنجاح!");
        setTimeout(() => setSuccessMsg(""), 3000);
      }
    } catch (err) {
      console.error(err);
    }
  };

  const handleResetData = async () => {
    if (confirm("هل تريد إعادة ضبط كروت الستامبا والبيانات الحية للتجربة مجدداً؟")) {
      await fetch("/api/reset-data", { method: "POST" });
      onRefresh();
      setSuccessMsg("تمت إعادة تهيئة البيانات حياً!");
      setTimeout(() => setSuccessMsg(""), 2000);
    }
  };

  const handleClearCache = () => {
    if (confirm("هل تريد مسح الملفات المؤقتة للتطبيق لتحسين الأداء؟")) {
      setSuccessMsg("تم مسح الذاكرة المؤقتة بنجاح!");
      setTimeout(() => setSuccessMsg(""), 2000);
    }
  };

  const handleMarkNotificationsRead = async () => {
    await fetch("/api/notifications/read-all", { method: "POST" });
    onRefresh();
  };

  const handleScanClick = (store: Store) => {
    setSelectedStoreForScan(store);
    setIsCameraOpen(true);
  };

  const handleMainScanClick = () => {
    setSelectedStoreForScan(stores[0] || null);
    setIsCameraOpen(true);
  };

  return (
    <div className={`w-full min-h-screen ${isDarkMode ? "bg-slate-950 text-slate-100" : "bg-slate-50 text-slate-800"} flex flex-col relative text-right transition-colors duration-300 font-sans antialiased`}>
      
      {/* Body Content */}
      <div className="flex-1 overflow-y-auto pb-28 pt-2">
            
            {/* Top User Summary Header */}
            {activeTab !== "profile" && (
              <div className={`px-5 pt-4 pb-3 flex items-center justify-between border-b ${isDarkMode ? "bg-slate-900 border-slate-800" : "bg-white border-slate-100"}`}>
                <div className="flex items-center gap-3">
                  <div className="w-11 h-11 rounded-full bg-purple-50 flex items-center justify-center border-2 border-fuchsia-500 overflow-hidden shadow-sm cursor-pointer" onClick={() => setIsMemberCardOpen(true)}>
                    {profile.avatar ? (
                      <img src={profile.avatar} className="w-full h-full object-cover" />
                    ) : (
                      <span className="text-xl font-bold text-purple-700">م</span>
                    )}
                  </div>
                  <div>
                    <div className="flex items-center gap-1">
                      <p className="text-[10px] text-slate-400 font-semibold">مرحباً بكِ 👋</p>
                      <span className="text-[9px] bg-amber-500/15 text-amber-600 font-bold px-1.5 py-0.2 rounded-md">
                        {profile.points >= 1500 ? "العضوية الماسية VIP" : "العضوية الذهبية"}
                      </span>
                    </div>
                    <h4 className={`font-bold text-sm cursor-pointer ${isDarkMode ? "text-slate-100" : "text-slate-800"}`} onClick={() => setIsMemberCardOpen(true)}>{profile.name}</h4>
                  </div>
                </div>
                
                <div className="flex items-center gap-2">
                  <button 
                    onClick={handleResetData}
                    title="إعادة ضبط التطبيق"
                    className={`p-2 rounded-xl transition ${isDarkMode ? "bg-slate-800 hover:bg-slate-750 text-slate-300" : "bg-slate-100 hover:bg-slate-200 text-slate-600"}`}
                  >
                    <RefreshCw className="w-4 h-4" />
                  </button>
                  <button
                    onClick={() => setIsMemberCardOpen(true)}
                    className="bg-fuchsia-50 text-fuchsia-600 hover:bg-fuchsia-100 font-bold px-3 py-1.5 rounded-xl text-xs flex items-center gap-1 border border-fuchsia-100 shadow-xs transition-transform active:scale-95"
                    title="عرض بطاقة الولاء الرقمية"
                  >
                    <Zap className="w-3.5 h-3.5 fill-current" />
                    <span>{profile.points.toLocaleString()} نقطة ⭐</span>
                  </button>
                </div>
              </div>
            )}

            <AnimatePresence mode="wait">
              {activeTab === "home" && (
                <motion.div
                  key="home"
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -10 }}
                  className="p-5 space-y-5"
                >
                  {/* Category Selection Carousel */}
                  <div className="flex gap-2 overflow-x-auto pb-1 no-scrollbar -mx-5 px-5">
                    {categories.map((cat, i) => (
                      <button
                        key={i}
                        onClick={() => setSelectedCategory(cat)}
                        className={`py-2 px-4 text-xs font-bold rounded-full whitespace-nowrap transition-all ${
                          selectedCategory === cat
                            ? "bg-gradient-to-r from-purple-600 to-fuchsia-600 text-white shadow-md shadow-fuchsia-500/10"
                            : isDarkMode 
                              ? "bg-slate-900 text-slate-300 hover:bg-slate-800 border border-slate-800"
                              : "bg-white text-slate-600 hover:bg-slate-100 border border-slate-100"
                        }`}
                      >
                        {cat}
                      </button>
                    ))}
                  </div>

                  {/* High Graphic Luxury Banner for Almaraa Mall */}
                  <div className="bg-gradient-to-tr from-purple-600 to-pink-500 rounded-3xl p-5 text-white relative overflow-hidden shadow-lg shadow-fuchsia-500/15">
                    <div className="absolute right-0 top-0 w-24 h-24 bg-white/10 rounded-full -mr-8 -mt-8" />
                    <div className="absolute left-4 top-4 text-4xl opacity-20 select-none">🦋</div>
                    <div className="relative z-10 space-y-2">
                      <span className="bg-black/20 text-[10px] font-bold tracking-wider uppercase px-2.5 py-1 rounded-full">قراءة فورية بالـ AI ✨</span>
                      <h3 className="font-extrabold text-lg leading-snug">امسحي فاتورة المتجر مباشرة!</h3>
                      <p className="text-xs text-white/95 leading-relaxed max-w-[85%]">
                        بديل الـ QR التقليدي لطلبات الجملة والتجزئة. وجهي الكاميرا للفاتورة، وسيتعرف نظامنا الذكي على التفاصيل وتضاف الطوابع فوراً!
                      </p>
                      <button
                        onClick={handleMainScanClick}
                        className="bg-white text-fuchsia-600 font-extrabold text-xs py-2 px-4.5 rounded-xl shadow-sm hover:bg-slate-50 transition mt-2 flex items-center gap-1.5 active:scale-95"
                      >
                        <Plus className="w-4 h-4 stroke-[3]" />
                        امسحي فاتورتكِ الآن
                      </button>
                    </div>
                  </div>

                  {/* Exclusive Offers Horizontal Scrollbar */}
                  <div className="space-y-2">
                    <div className="flex justify-between items-center px-1">
                      <h4 className={`font-bold text-xs ${isDarkMode ? "text-slate-200" : "text-slate-800"}`}>عروض المول الحصرية 🌟</h4>
                      <span className="text-[10px] text-fuchsia-500 font-bold animate-pulse">اسحبي لمشاهدة المزيد ➔</span>
                    </div>
                    
                    <div className="flex gap-3 overflow-x-auto pb-2 pt-1 no-scrollbar -mx-5 px-5 scroll-smooth">
                      {mallOffers.map((offer) => (
                        <div
                          key={offer.id}
                          className="w-[260px] h-[160px] shrink-0 rounded-2xl text-white relative overflow-hidden shadow-md border border-white/10 hover:scale-[1.02] transition-transform duration-250 select-none flex flex-col justify-end p-4"
                        >
                          {/* Background image underlay */}
                          <img 
                            src={offer.image} 
                            alt={offer.title} 
                            referrerPolicy="no-referrer"
                            className="absolute inset-0 w-full h-full object-cover z-0 filter brightness-95" 
                          />
                          
                          {/* Elegant dark gradient overlay to guarantee premium contrast */}
                          <div className={`absolute inset-0 bg-gradient-to-t ${offer.gradient} z-10`} />

                          {/* Interactive content layer */}
                          <div className="relative z-20 space-y-1">
                            <div className="flex justify-between items-center">
                              <span className="text-[9px] bg-fuchsia-600/90 text-white font-extrabold px-2 py-0.5 rounded-md">
                                {offer.badge}
                              </span>
                              <span className="text-sm">{offer.logo}</span>
                            </div>

                            <h5 className="font-extrabold text-[11px] text-white tracking-wide truncate">{offer.storeName}</h5>
                            <h6 className="font-extrabold text-[12px] text-fuchsia-300 truncate">{offer.title}</h6>
                            <p className="text-[10px] text-slate-100/90 leading-tight h-10 overflow-hidden text-ellipsis line-clamp-2 pr-0.5">
                              {offer.description}
                            </p>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>

                  {/* Search Bar */}
                  <div className="relative">
                    <input
                      type="text"
                      placeholder="ابحثي عن بوتيك أزياء، مستحضرات تجميل..."
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className={`w-full border rounded-2xl py-3 pr-11 pl-4 text-xs font-semibold shadow-sm focus:outline-none focus:ring-2 focus:ring-fuchsia-500 transition-all text-right ${
                        isDarkMode 
                          ? "bg-slate-900 border-slate-800 text-slate-100 placeholder-slate-500" 
                          : "bg-white border-slate-100 text-slate-800 placeholder-slate-400"
                      }`}
                    />
                    <Search className="absolute right-4 top-1/2 -translate-y-1/2 text-slate-400 w-4.5 h-4.5" />
                  </div>

                  {/* Stores Cards Grid with Luxury Styles & MOQ Ranges */}
                  <div className="space-y-3">
                    <div className="flex justify-between items-center">
                      <h4 className={`font-bold text-sm ${isDarkMode ? "text-slate-200" : "text-slate-800"}`}>المحلات النسائية المشاركة ({filteredStores.length})</h4>
                      <span className="text-[10px] bg-fuchsia-50 text-fuchsia-600 px-2 py-0.5 rounded-full font-bold">المرأة مول • طرابلس</span>
                    </div>

                    {filteredStores.length === 0 ? (
                      <div className="text-center py-8 text-slate-400">
                        <p className="text-xs">لا توجد محلات تطابق البحث حالياً</p>
                      </div>
                    ) : (
                      <div className="space-y-3">
                        {filteredStores.map((store) => (
                          <div
                            key={store.id}
                            className={`rounded-2xl p-4 border shadow-sm transition-all flex items-center justify-between ${
                              isDarkMode 
                                ? "bg-slate-900 border-slate-800 hover:bg-slate-850" 
                                : "bg-white border-slate-100 hover:border-slate-200"
                            }`}
                          >
                            <div className="flex items-center gap-3">
                              <span className={`text-3xl p-2.5 rounded-2xl shadow-inner flex items-center justify-center w-14 h-14 border ${
                                isDarkMode ? "bg-slate-950 border-slate-800" : "bg-slate-50 border-slate-100"
                              }`}>
                                {store.logo}
                              </span>
                              <div>
                                <div className="flex items-center gap-1.5">
                                  <h5 className={`font-bold text-sm ${isDarkMode ? "text-slate-100" : "text-slate-800"}`}>{store.name}</h5>
                                  {store.verified && (
                                    <span className="bg-blue-50 text-blue-500 p-0.5 rounded-full" title="مورد معتمد">
                                      <ShieldCheck className="w-3.5 h-3.5 fill-current text-blue-500 bg-white rounded-full" />
                                    </span>
                                  )}
                                </div>
                                <p className="text-[10px] text-slate-400 font-medium mt-0.5">{store.category} • الحد الأدنى: <span className="font-bold text-fuchsia-500">{store.moq}</span></p>
                                
                                {/* Stamping Progress Tracker with fuchsia color */}
                                <div className="mt-2.5 flex items-center gap-2">
                                  <div className={`w-24 h-2 rounded-full overflow-hidden ${isDarkMode ? "bg-slate-850" : "bg-slate-100"}`}>
                                    <div 
                                      className="bg-gradient-to-r from-purple-500 to-fuchsia-500 h-full rounded-full transition-all duration-500" 
                                      style={{ width: `${(store.currentStamps / store.maxStamps) * 100}%` }}
                                    />
                                  </div>
                                  <span className={`text-[10px] font-bold ${isDarkMode ? "text-slate-300" : "text-slate-600"}`}>
                                    {store.currentStamps}/{store.maxStamps} طوابع
                                  </span>
                                </div>

                                {/* International B2B marketplace contacts & download shortcuts */}
                                <div className="mt-2 flex items-center gap-2 flex-wrap">
                                  <a
                                    href="https://wa.me/218910000000"
                                    target="_blank"
                                    rel="noreferrer"
                                    className="text-[9px] font-extrabold text-emerald-500 bg-emerald-500/10 hover:bg-emerald-500/20 px-2 py-0.5 rounded-md transition-all flex items-center gap-1"
                                  >
                                    <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
                                    <span>واتساب الجملة 💬</span>
                                  </a>
                                  <button
                                    onClick={() => alert(`جاري تحميل كتالوج المنتجات ومبيعات الجملة لـ ${store.name} لعام 2026...`)}
                                    className="text-[9px] font-extrabold text-blue-500 bg-blue-500/10 hover:bg-blue-500/20 px-2 py-0.5 rounded-md transition-all flex items-center gap-1"
                                  >
                                    <span>📂 الكتالوج</span>
                                  </button>
                                </div>
                              </div>
                            </div>

                            {/* Quick AI Scan trigger */}
                            <button
                              onClick={() => handleScanClick(store)}
                              className={`p-3 rounded-xl border transition-all active:scale-95 flex items-center justify-center group ${
                                isDarkMode 
                                  ? "bg-slate-850 hover:bg-fuchsia-950/40 text-slate-300 hover:text-fuchsia-400 border-slate-800 hover:border-fuchsia-900" 
                                  : "bg-slate-50 hover:bg-fuchsia-50 hover:text-fuchsia-600 text-slate-600 border-slate-100 hover:border-fuchsia-200"
                              }`}
                              title="قراءة الفاتورة بـ AI المتجر"
                            >
                              <Plus className="w-5 h-5 group-hover:rotate-90 transition-transform duration-200" />
                            </button>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </motion.div>
              )}

              {activeTab === "wallet" && (
                <motion.div
                  key="wallet"
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -10 }}
                  className="p-5 space-y-5"
                >
                  <div className="flex justify-between items-center mb-1">
                    <h4 className={`font-bold text-sm ${isDarkMode ? "text-slate-200" : "text-slate-800"}`}>محفظتي لبطاقات الستامبا</h4>
                    <span className={`text-xs font-semibold px-2.5 py-1 rounded-xl border ${
                      isDarkMode ? "bg-slate-900 border-slate-800 text-slate-300" : "bg-white border-slate-100 text-slate-500"
                    }`}>
                      {stores.filter(s => s.currentStamps > 0).length} بطاقات نشطة
                    </span>
                  </div>

                  {/* Stamp loyalty cards selection carousel */}
                  <div className="space-y-4">
                    {stores.map((store) => (
                      <div
                        key={store.id}
                        onClick={() => setSelectedCardId(store.id === selectedCardId ? null : store.id)}
                        className={`rounded-3xl p-5 border cursor-pointer transition-all ${
                          selectedCardId === store.id
                            ? "bg-slate-950 text-white border-purple-900/60 shadow-xl shadow-fuchsia-950/10"
                            : isDarkMode
                              ? "bg-slate-900 text-slate-200 border-slate-800 hover:border-slate-700"
                              : "bg-white text-slate-800 border-slate-100 shadow-sm hover:border-slate-300"
                        }`}
                      >
                        <div className="flex justify-between items-start">
                          <div className="flex gap-3">
                            <span className={`text-2xl p-2 rounded-2xl flex items-center justify-center w-11 h-11 border ${
                              selectedCardId === store.id
                                ? "bg-purple-900/20 text-white border-purple-800/40"
                                : "bg-slate-100 text-slate-800 border-slate-200"
                            }`}>
                              {store.logo}
                            </span>
                            <div>
                              <h5 className="font-bold text-sm">{store.name}</h5>
                              <p className={`text-xs mt-0.5 ${selectedCardId === store.id ? "text-slate-400" : "text-slate-400"}`}>
                                {store.description}
                              </p>
                            </div>
                          </div>
                          
                          <div className="text-left">
                            <span className="text-[10px] font-bold px-2 py-1 bg-fuchsia-500/10 text-fuchsia-400 rounded-lg">
                              حد الطلب: {store.moq}
                            </span>
                          </div>
                        </div>

                        {/* Stamping Grid display in Fuchsia Theme */}
                        <div className="mt-5 grid grid-cols-5 gap-2.5">
                          {Array.from({ length: store.maxStamps }).map((_, i) => {
                            const isStamped = i < store.currentStamps;
                            const isPrize = i === store.maxStamps - 1;
                            
                            return (
                              <div
                                key={i}
                                className={`aspect-square rounded-xl flex items-center justify-center text-[10px] font-bold border transition-all ${
                                  isStamped
                                    ? "bg-gradient-to-tr from-purple-500 to-fuchsia-500 text-white border-fuchsia-500 shadow-sm"
                                    : selectedCardId === store.id
                                      ? "bg-slate-900 border-slate-800 text-slate-600"
                                      : "bg-slate-50 border-slate-100 text-slate-400"
                                }`}
                              >
                                {isStamped ? (
                                  <span>✓</span>
                                ) : isPrize ? (
                                  <span>🎁</span>
                                ) : (
                                  <span>{i + 1}</span>
                                )}
                              </div>
                            );
                          })}
                        </div>

                        {/* Stamp Card Details footer */}
                        {selectedCardId === store.id && (
                          <div className="mt-5 pt-4 border-t border-slate-900 space-y-3 text-xs text-slate-300">
                            <div className="flex justify-between">
                              <span>المكافأة القادمة:</span>
                              <span className="font-bold text-fuchsia-400">{store.rewardText}</span>
                            </div>
                            <div className="flex justify-between">
                              <span>حالة كرت الستامبا:</span>
                              <span>{store.currentStamps === store.maxStamps ? "🎉 ممتلئة وجاهزة للصرف" : `متبقي ${store.maxStamps - store.currentStamps} طوابع للمكافأة`}</span>
                            </div>
                            {store.stampsHistory.length > 0 && (
                              <div className="space-y-1 bg-slate-900/60 p-2.5 rounded-xl text-[10px] font-mono">
                                <span className="text-slate-500 font-semibold block mb-1">العمليات الأخيرة:</span>
                                {store.stampsHistory.map((hist, hIdx) => (
                                  <div key={hIdx} className="flex justify-between text-slate-400">
                                    <span>{hist.date}</span>
                                    <span>{hist.amount} ({hist.count} طابع)</span>
                                  </div>
                                ))}
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </motion.div>
              )}

              {activeTab === "notifications" && (
                <motion.div
                  key="notifications"
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -10 }}
                  className="p-5 space-y-4"
                >
                  <div className="flex justify-between items-center">
                    <h4 className={`font-bold text-sm ${isDarkMode ? "text-slate-200" : "text-slate-800"}`}>التنبيهات وحملات المتاجر</h4>
                    <button 
                      onClick={handleMarkNotificationsRead}
                      className="text-xs text-fuchsia-600 font-bold hover:underline"
                    >
                      تحديد الكل كمقروء
                    </button>
                  </div>

                  {notifications.length === 0 ? (
                    <div className="text-center py-12 text-slate-400">
                      <Bell className="w-10 h-10 mx-auto mb-2 opacity-50" />
                      <p className="text-xs">لا توجد تنبيهات جديدة حالياً</p>
                    </div>
                  ) : (
                    <div className="space-y-3">
                      {notifications.map((notif) => (
                        <div
                          key={notif.id}
                          className={`p-4 rounded-2xl border transition-all text-right ${
                            notif.read
                              ? isDarkMode 
                                ? "bg-slate-900 border-slate-850 text-slate-400" 
                                : "bg-white border-slate-100 text-slate-600"
                              : isDarkMode
                                ? "bg-purple-950/20 border-purple-900/30 text-slate-200"
                                : "bg-fuchsia-50/50 border-fuchsia-100 text-slate-800 shadow-xs"
                          }`}
                        >
                          <div className="flex items-center gap-2.5 mb-1.5">
                            <span className={`text-xl p-1 rounded-lg shadow-sm ${isDarkMode ? "bg-slate-800" : "bg-white"}`}>{notif.logo}</span>
                            <div className="flex-1">
                              <h5 className="font-bold text-xs">{notif.title}</h5>
                              <p className="text-[10px] text-slate-400 mt-0.5">{notif.time}</p>
                            </div>
                            {!notif.read && (
                              <span className="w-1.5 h-1.5 rounded-full bg-fuchsia-500 animate-pulse" />
                            )}
                          </div>
                          <p className="text-xs leading-relaxed text-slate-500 mr-8">
                            {notif.message}
                          </p>
                        </div>
                      ))}
                    </div>
                  )}
                </motion.div>
              )}

              {activeTab === "profile" && (
                <motion.div
                  key="profile"
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -10 }}
                  className="p-5"
                >
                  {/* Account detail Header */}
                  <div className="text-center pb-5 mb-4 border-b border-slate-100 flex flex-col items-center">
                    <div className="relative mb-3">
                      <div className="w-20 h-20 rounded-full bg-slate-200 border-2 border-fuchsia-500 flex items-center justify-center overflow-hidden">
                        <span className="text-3xl font-extrabold text-slate-800">م</span>
                      </div>
                      <span className="absolute bottom-0 left-0 bg-slate-900 text-white text-[10px] font-mono px-1.5 py-0.5 rounded-full border border-white">
                        {profile.cardId}
                      </span>
                    </div>
                    <h4 className={`font-bold text-lg ${isDarkMode ? "text-slate-100" : "text-slate-800"}`}>{profile.name}</h4>
                    <p className="text-xs text-slate-400 font-mono mt-0.5">{profile.email}</p>

                    <div className="mt-4 grid grid-cols-2 gap-3 w-full">
                      <div className="bg-purple-50 border border-purple-100 rounded-2xl p-3 text-center">
                        <span className="text-lg font-bold text-purple-700 block">{profile.stamps}</span>
                        <span className="text-[10px] text-purple-600 font-semibold">طوابع مجمعة</span>
                      </div>
                      <div className="bg-pink-50 border border-pink-100 rounded-2xl p-3 text-center">
                        <span className="text-lg font-bold text-pink-700 block">{profile.prizes}</span>
                        <span className="text-[10px] text-pink-600 font-semibold">مكافآت جاهزة</span>
                      </div>
                    </div>
                  </div>

                  {/* Settings & Profile Toggle Menu */}
                  <div className="grid grid-cols-2 bg-slate-900 p-1 rounded-xl border border-slate-850 mb-5 text-center text-slate-300">
                    <button
                      onClick={() => setSettingsTab("profile_edit")}
                      className={`py-1.5 px-3 text-xs font-bold rounded-lg transition ${
                        settingsTab === "profile_edit" ? "bg-purple-600 text-white" : "text-slate-400"
                      }`}
                    >
                      الملف الشخصي
                    </button>
                    <button
                      onClick={() => setSettingsTab("preferences")}
                      className={`py-1.5 px-3 text-xs font-bold rounded-lg transition ${
                        settingsTab === "preferences" ? "bg-purple-600 text-white" : "text-slate-400"
                      }`}
                    >
                      إعدادات التطبيق
                    </button>
                  </div>

                  {settingsTab === "profile_edit" ? (
                    /* Profile Edit Form */
                    <form onSubmit={handleProfileSave} className="space-y-4 text-right">
                      <div className="space-y-1">
                        <label className="text-[11px] font-bold text-slate-400 block mr-1">الاسم الكامل</label>
                        <input
                          type="text"
                          value={profileForm.name}
                          onChange={(e) => setProfileForm({ ...profileForm, name: e.target.value })}
                          className={`w-full border rounded-xl py-2.5 px-3 text-xs font-semibold focus:outline-none focus:ring-1 focus:ring-fuchsia-500 text-right ${
                            isDarkMode ? "bg-slate-900 border-slate-800 text-slate-100" : "bg-white border-slate-200 text-slate-800"
                          }`}
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-[11px] font-bold text-slate-400 block mr-1">رقم الهاتف</label>
                        <input
                          type="text"
                          value={profileForm.phone}
                          onChange={(e) => setProfileForm({ ...profileForm, phone: e.target.value })}
                          className={`w-full border rounded-xl py-2.5 px-3 text-xs font-semibold focus:outline-none focus:ring-1 focus:ring-fuchsia-500 text-right font-mono ${
                            isDarkMode ? "bg-slate-900 border-slate-800 text-slate-100" : "bg-white border-slate-200 text-slate-800"
                          }`}
                        />
                      </div>
                      <div className="grid grid-cols-2 gap-3">
                        <div className="space-y-1">
                          <label className="text-[11px] font-bold text-slate-400 block mr-1">تاريخ الميلاد</label>
                          <input
                            type="date"
                            value={profileForm.birthdate}
                            onChange={(e) => setProfileForm({ ...profileForm, birthdate: e.target.value })}
                            className={`w-full border rounded-xl py-2.5 px-3 text-xs font-semibold focus:outline-none focus:ring-1 focus:ring-fuchsia-500 text-right ${
                              isDarkMode ? "bg-slate-900 border-slate-800 text-slate-100" : "bg-white border-slate-200 text-slate-800"
                            }`}
                          />
                        </div>
                        <div className="space-y-1">
                          <label className="text-[11px] font-bold text-slate-400 block mr-1">المدينة</label>
                          <select
                            value={profileForm.city}
                            onChange={(e) => setProfileForm({ ...profileForm, city: e.target.value })}
                            className={`w-full border rounded-xl py-2.5 px-3 text-xs font-semibold focus:outline-none focus:ring-1 focus:ring-fuchsia-500 text-right ${
                              isDarkMode ? "bg-slate-900 border-slate-800 text-slate-100" : "bg-white border-slate-200 text-slate-800"
                            }`}
                          >
                            <option value="طرابلس">طرابلس</option>
                            <option value="بنغازي">بنغازي</option>
                            <option value="مصراتة">مصراتة</option>
                            <option value="الزاوية">الزاوية</option>
                          </select>
                        </div>
                      </div>

                      {successMsg && (
                        <div className="bg-emerald-50 text-emerald-800 border border-emerald-100 rounded-xl p-3 text-xs font-semibold text-center animate-pulse">
                          {successMsg}
                        </div>
                      )}

                      <button
                        type="submit"
                        className="w-full bg-slate-900 hover:bg-slate-800 text-white font-bold text-xs py-3 rounded-xl transition shadow-md"
                      >
                        حفظ التغييرات
                      </button>
                    </form>
                  ) : (
                    /* App Preferences & System Settings */
                    <div className="space-y-4 text-right">
                      {/* Dark Mode toggle */}
                      <div className={`p-3.5 rounded-2xl border flex items-center justify-between text-xs font-semibold ${
                        isDarkMode ? "bg-slate-900 border-slate-800" : "bg-white border-slate-100"
                      }`}>
                        <label className="relative inline-flex items-center cursor-pointer">
                          <input 
                            type="checkbox" 
                            checked={isDarkMode} 
                            onChange={(e) => setIsDarkMode(e.target.checked)}
                            className="sr-only peer"
                          />
                          <div className="w-9 h-5 bg-slate-700 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-purple-600"></div>
                        </label>
                        <span className={isDarkMode ? "text-slate-100" : "text-slate-800"}>تفعيل الوضع الداكن (Dark Mode)</span>
                      </div>

                      {/* Biometric Scan Toggle */}
                      <div className={`p-3.5 rounded-2xl border flex items-center justify-between text-xs font-semibold ${
                        isDarkMode ? "bg-slate-900 border-slate-800" : "bg-white border-slate-100"
                      }`}>
                        <label className="relative inline-flex items-center cursor-pointer">
                          <input 
                            type="checkbox" 
                            checked={biometricsEnabled} 
                            onChange={(e) => setBiometricsEnabled(e.target.checked)}
                            className="sr-only peer"
                          />
                          <div className="w-9 h-5 bg-slate-700 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-purple-600"></div>
                        </label>
                        <span className={isDarkMode ? "text-slate-100" : "text-slate-800"}>مسح سريع بالبصمة الحيوية</span>
                      </div>

                      {/* Vibrations toggle */}
                      <div className={`p-3.5 rounded-2xl border flex items-center justify-between text-xs font-semibold ${
                        isDarkMode ? "bg-slate-900 border-slate-800" : "bg-white border-slate-100"
                      }`}>
                        <label className="relative inline-flex items-center cursor-pointer">
                          <input 
                            type="checkbox" 
                            checked={vibrationsEnabled} 
                            onChange={(e) => setVibrationsEnabled(e.target.checked)}
                            className="sr-only peer"
                          />
                          <div className="w-9 h-5 bg-slate-700 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-purple-600"></div>
                        </label>
                        <span className={isDarkMode ? "text-slate-100" : "text-slate-800"}>الاهتزاز والتغذية الراجعة اللمسية</span>
                      </div>

                      {/* Language Selection */}
                      <div className={`p-3.5 rounded-2xl border flex items-center justify-between text-xs font-semibold ${
                        isDarkMode ? "bg-slate-900 border-slate-800" : "bg-white border-slate-100"
                      }`}>
                        <select 
                          value={appLanguage} 
                          onChange={(e) => setAppLanguage(e.target.value)}
                          className={`bg-transparent border-none text-xs font-bold focus:outline-none ${
                            isDarkMode ? "text-purple-400" : "text-purple-600"
                          }`}
                        >
                          <option value="العربية">العربية (ليبيا)</option>
                          <option value="English">English</option>
                        </select>
                        <span className={isDarkMode ? "text-slate-100" : "text-slate-800"}>لغة تطبيق الولاء</span>
                      </div>

                      {successMsg && (
                        <div className="bg-emerald-50 text-emerald-800 border border-emerald-100 rounded-xl p-3 text-xs font-semibold text-center animate-pulse">
                          {successMsg}
                        </div>
                      )}

                      {/* Utility buttons for developers */}
                      <div className="grid grid-cols-2 gap-2 pt-2">
                        <button
                          onClick={handleClearCache}
                          className="py-2.5 bg-slate-800 hover:bg-slate-750 text-white font-bold text-xs rounded-xl transition"
                        >
                          مسح الكاش
                        </button>
                        <button
                          onClick={handleResetData}
                          className="py-2.5 bg-purple-900/30 hover:bg-purple-900/50 text-purple-400 font-bold text-xs rounded-xl border border-purple-900/40 transition"
                        >
                          تهيئة الكروت
                        </button>
                      </div>

                      {/* Admin Switcher Button (if available) */}
                      {onSwitchToAdmin && (
                        <button
                          onClick={onSwitchToAdmin}
                          className="w-full bg-slate-900 hover:bg-slate-850 text-purple-300 border border-purple-800/40 font-bold text-xs py-3 rounded-xl transition shadow-md flex items-center justify-center gap-2 mt-2"
                        >
                          <ShieldCheck className="w-4 h-4 text-purple-400" />
                          <span>لوحة الإدارة والتدقيق والتحليلات (B2B)</span>
                        </button>
                      )}

                      {/* Logout Action Button */}
                      <button
                        onClick={onLogout}
                        className="w-full bg-red-600 hover:bg-red-700 text-white font-bold text-xs py-3 rounded-xl transition shadow-md flex items-center justify-center gap-1.5 mt-3"
                      >
                        <LogOut className="w-4 h-4" />
                        <span>تسجيل الخروج من التطبيق</span>
                      </button>
                    </div>
                  )}
                </motion.div>
              )}
            </AnimatePresence>
          </div>

          {/* Bottom Native Mobile Navigation Bar */}
          <div className={`fixed bottom-0 left-0 right-0 border-t px-4 py-2 pb-5 flex justify-between items-center z-40 select-none shadow-[0_-5px_20px_rgba(0,0,0,0.06)] backdrop-blur-lg ${
            isDarkMode ? "bg-slate-900/95 border-slate-800" : "bg-white/95 border-slate-100"
          }`}>
            
            <button
              onClick={() => setActiveTab("profile")}
              className={`flex-1 flex flex-col items-center py-1 transition-all ${
                activeTab === "profile" ? "text-fuchsia-500 font-bold scale-105" : "text-slate-400 hover:text-slate-600"
              }`}
            >
              <User className={`w-5.5 h-5.5 ${activeTab === "profile" ? "stroke-[2.5]" : "stroke-2"}`} />
              <span className="text-[10px] mt-1">الحساب</span>
            </button>

            <button
              onClick={() => setActiveTab("notifications")}
              className={`flex-1 flex flex-col items-center py-1 relative transition-all ${
                activeTab === "notifications" ? "text-fuchsia-500 font-bold scale-105" : "text-slate-400 hover:text-slate-600"
              }`}
            >
              <Bell className={`w-5.5 h-5.5 ${activeTab === "notifications" ? "stroke-[2.5]" : "stroke-2"}`} />
              <span className="text-[10px] mt-1">الإشعارات</span>
              {notifications.filter(n => !n.read).length > 0 && (
                <span className="absolute top-0.5 right-6 w-4 h-4 bg-fuchsia-500 text-white text-[9px] font-bold rounded-full flex items-center justify-center border border-white">
                  {notifications.filter(n => !n.read).length}
                </span>
              )}
            </button>

            {/* Middle Main Camera AI Floating Button in Fuchsia */}
            <div className="flex-1 flex justify-center -mt-8 relative z-40">
              <button
                onClick={handleMainScanClick}
                className="w-14 h-14 bg-gradient-to-tr from-purple-600 to-pink-500 text-white rounded-full flex items-center justify-center shadow-lg shadow-fuchsia-500/40 hover:scale-110 active:scale-95 transition-transform border-4 border-slate-950 relative group"
                title="مسح الفاتورة فوراً بالذكاء الاصطناعي"
              >
                <div className="absolute inset-0 rounded-full bg-fuchsia-400/20 animate-ping group-hover:hidden" />
                <Plus className="w-7 h-7 text-white stroke-[3]" />
              </button>
            </div>

            <button
              onClick={() => setActiveTab("wallet")}
              className={`flex-1 flex flex-col items-center py-1 transition-all ${
                activeTab === "wallet" ? "text-fuchsia-500 font-bold scale-105" : "text-slate-400 hover:text-slate-600"
              }`}
            >
              <Wallet className={`w-5.5 h-5.5 ${activeTab === "wallet" ? "stroke-[2.5]" : "stroke-2"}`} />
              <span className="text-[10px] mt-1">المحفظة</span>
            </button>

            <button
              onClick={() => setActiveTab("home")}
              className={`flex-1 flex flex-col items-center py-1 transition-all ${
                activeTab === "home" ? "text-fuchsia-500 font-bold scale-105" : "text-slate-400 hover:text-slate-600"
              }`}
            >
              <StoreIcon className={`w-5.5 h-5.5 ${activeTab === "home" ? "stroke-[2.5]" : "stroke-2"}`} />
              <span className="text-[10px] mt-1">المتاجر</span>
            </button>

          </div>

      {/* AI Invoice Scan Camera Modal */}
      {selectedStoreForScan && (
        <CameraModal
          isOpen={isCameraOpen}
          onClose={() => setIsCameraOpen(false)}
          selectedStore={selectedStoreForScan}
          onScanComplete={() => {
            onRefresh();
          }}
        />
      )}

      {/* 💳 Digital Loyalty Membership Card Modal */}
      {isMemberCardOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs text-right" dir="rtl">
          <motion.div 
            initial={{ scale: 0.93, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            className="bg-slate-900 border border-slate-800 rounded-3xl p-6 w-full max-w-[360px] shadow-2xl relative space-y-5"
          >
            {/* Close button */}
            <button 
              onClick={() => setIsMemberCardOpen(false)}
              className="absolute top-4 left-4 text-slate-400 hover:text-white bg-slate-800 p-1.5 rounded-full"
            >
              ✕
            </button>

            {/* Header */}
            <div className="text-center pt-2">
              <h4 className="text-base font-extrabold text-white">بطاقة العضوية الرقمية 💎</h4>
              <p className="text-[11px] text-slate-400 mt-1">امسحي البطاقة عند كاشير أي متجر لجمع النقاط تلقائياً</p>
            </div>

            {/* Physical Credit-Card looking Loyalty Membership Card */}
            <div className="bg-gradient-to-tr from-indigo-950 via-purple-900 to-fuchsia-800 rounded-2xl p-5 text-white relative overflow-hidden shadow-lg border border-white/10 aspect-[1.58/1]">
              <div className="absolute right-0 top-0 text-7xl opacity-5 pointer-events-none select-none">🦋</div>
              <div className="absolute left-4 bottom-4 text-3xl opacity-10 select-none font-bold">MM APP</div>
              
              <div className="flex justify-between items-start">
                <div>
                  <span className="text-[10px] bg-amber-500 text-slate-950 font-extrabold px-2.5 py-0.5 rounded-md shadow-sm">
                    {profile.points >= 1500 ? "عضوية ماسية VIP" : "عضوية ذهبية ممتازة"}
                  </span>
                  <h3 className="font-extrabold text-sm mt-3 tracking-wide">{profile.name}</h3>
                </div>
                <div className="text-left font-bold text-xs text-fuchsia-300">
                  المرأة مول
                </div>
              </div>

              <div className="mt-5 flex justify-between items-end">
                <div>
                  <p className="text-[9px] text-slate-300">رقم العضوية المميز</p>
                  <p className="font-mono text-xs text-white tracking-widest font-bold">MM-908-112</p>
                </div>
                <div className="text-left">
                  <p className="text-[9px] text-slate-300">مجموع النقاط</p>
                  <p className="font-extrabold text-xs text-amber-300">{profile.points.toLocaleString()} نقطة</p>
                </div>
              </div>
            </div>

            {/* Simulated Scanning Barcode for physical counter scans */}
            <div className="bg-white p-4 rounded-2xl flex flex-col items-center justify-center space-y-2 border border-slate-800">
              {/* Simulated barcode using variable width bars */}
              <div className="flex items-stretch h-14 w-full justify-center bg-white py-1 select-none">
                {[1, 3, 1, 2, 4, 1, 3, 2, 1, 4, 2, 1, 3, 1, 2, 1, 4, 1, 2, 3, 1, 4, 1, 2, 1].map((width, idx) => (
                  <div 
                    key={idx} 
                    className="bg-black" 
                    style={{ 
                      width: `${width}px`, 
                      marginLeft: idx % 3 === 0 ? "2px" : "1px" 
                    }} 
                  />
                ))}
              </div>
              <p className="font-mono text-[10px] text-slate-700 font-bold tracking-widest">*(MM-908-112)*</p>
            </div>

            {/* Quick action info */}
            <p className="text-center text-[9px] text-slate-400">
              * تدعم البطاقة القراءة السريعة بالليزر لعمليات الجملة الفورية وتحديد هوية المناديب والمشترين.
            </p>
          </motion.div>
        </div>
      )}
    </div>
  );
}
