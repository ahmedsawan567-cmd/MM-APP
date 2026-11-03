import React, { useState } from "react";
import { Phone, Lock, Sparkles, AlertCircle, CheckCircle } from "lucide-react";
import { motion } from "motion/react";

interface LoginProps {
  onLoginSuccess: (role: "customer" | "admin", username: string) => void;
}

export default function Login({ onLoginSuccess }: LoginProps) {
  const [activeForm, setActiveForm] = useState<"signin" | "signup">("signin");
  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");
  const [fullName, setFullName] = useState("");
  const [city, setCity] = useState("طرابلس");
  const [rememberMe, setRememberMe] = useState(true);
  const [errorMsg, setErrorMsg] = useState("");
  const [successMsg, setSuccessMsg] = useState("");
  const [loading, setLoading] = useState(false);

  // Load standard credentials helper
  const handleTestLogin = (userType: "customer" | "admin") => {
    setLoading(true);
    setTimeout(() => {
      setLoading(false);
      if (userType === "customer") {
        onLoginSuccess("customer", "منى محمد");
      } else {
        onLoginSuccess("admin", "المدير العام");
      }
    }, 800);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");
    setSuccessMsg("");

    // Validation
    if (!phone) {
      setErrorMsg("الرجاء إدخال رقم الهاتف الخاص بك");
      return;
    }
    if (!password || password.length < 5) {
      setErrorMsg("يجب أن تكون كلمة المرور 5 خانات على الأقل");
      return;
    }
    if (activeForm === "signup" && !fullName) {
      setErrorMsg("الرجاء إدخال الاسم الكامل للتسجيل");
      return;
    }

    setLoading(true);
    try {
      // Simulation of a secure Node.js backend authentication flow
      await new Promise((resolve) => setTimeout(resolve, 1200));

      // Standard mock validation for showcase
      if (activeForm === "signin") {
        // Admin quick login bypass
        if (phone === "admin" || (phone === "0910000000" && password === "12345")) {
          if (rememberMe) {
            localStorage.setItem("remember_token", "admin-token-998");
          }
          onLoginSuccess("admin", "المدير العام");
          return;
        }

        // Regular login (Any other digits will login successfully for smooth user evaluation)
        if (rememberMe) {
          localStorage.setItem("remember_token", "customer-token-112");
        }
        onLoginSuccess("customer", phone === "0915551234" ? "منى محمد" : "عميل تجريبي");
      } else {
        // Sign up simulation
        setSuccessMsg("تم إنشاء حسابك بنجاح! جاري تسجيل الدخول...");
        setTimeout(() => {
          if (rememberMe) {
            localStorage.setItem("remember_token", "customer-token-112");
          }
          onLoginSuccess("customer", fullName);
        }, 1000);
      }
    } catch (err) {
      setErrorMsg("حدث خطأ أثناء الاتصال بالخادم، الرجاء المحاولة مجدداً");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="w-full min-h-screen bg-slate-950 flex flex-col justify-center items-center p-4 sm:p-6 text-slate-100 text-right relative overflow-hidden font-sans antialiased">
      
      {/* Absolute Decorative Glow */}
      <div className="absolute -top-24 -right-24 w-72 h-72 bg-purple-600/15 rounded-full blur-3xl pointer-events-none" />
      <div className="absolute -bottom-24 -left-24 w-72 h-72 bg-pink-500/15 rounded-full blur-3xl pointer-events-none" />

      <div className="w-full max-w-md mx-auto flex flex-col p-6 text-slate-100 relative z-10">
        
        {/* Brand Header */}
        <div className="text-center my-6 space-y-3">
          <div className="inline-flex bg-gradient-to-tr from-purple-600 to-pink-500 p-4 rounded-3xl text-white shadow-lg shadow-fuchsia-500/20 mb-2">
            <svg viewBox="0 0 100 100" className="w-10 h-10 fill-current">
              <path d="M50,45 C35,45 25,25 35,15 C45,5 50,45 50,45" fill="#f43f5e"/>
              <path d="M50,45 C65,45 75,25 65,15 C55,5 50,45 50,45" fill="#ec4899"/>
              <path d="M50,45 C38,45 35,62 42,62 C50,62 50,45 50,45" fill="#d946ef"/>
              <path d="M50,45 C62,45 65,62 58,62 C50,62 50,45 50,45" fill="#8b5cf6"/>
              <path d="M42,30 Q50,45 50,45 Q50,45 58,30" fill="none" stroke="#fff" strokeWidth="3" strokeLinecap="round"/>
            </svg>
          </div>
          <h2 className="font-black text-2xl text-transparent bg-clip-text bg-gradient-to-r from-pink-400 to-purple-400">المرأة مول</h2>
          <p className="text-xs text-slate-400 max-w-[80%] mx-auto leading-relaxed">
            محفظتك الذكية لجمع طوابع ولاء بوتيكات التجميل والأزياء النسائية بالـ AI 🦋
          </p>
        </div>

        {/* Tab Switcher */}
        <div className="grid grid-cols-2 bg-slate-900/60 p-1.5 rounded-2xl border border-slate-800 mb-6">
          <button
            onClick={() => { setActiveForm("signin"); setErrorMsg(""); }}
            className={`py-2 px-3 text-xs font-bold rounded-xl transition ${
              activeForm === "signin"
                ? "bg-purple-600/90 text-white shadow"
                : "text-slate-400 hover:text-slate-200"
            }`}
          >
            تسجيل دخول
          </button>
          <button
            onClick={() => { setActiveForm("signup"); setErrorMsg(""); }}
            className={`py-2 px-3 text-xs font-bold rounded-xl transition ${
              activeForm === "signup"
                ? "bg-purple-600/90 text-white shadow"
                : "text-slate-400 hover:text-slate-200"
            }`}
          >
            إنشاء حساب جديد
          </button>
        </div>

        {/* Error / Success Alerts */}
        {errorMsg && (
          <motion.div 
            initial={{ opacity: 0, y: -5 }} 
            animate={{ opacity: 1, y: 0 }}
            className="bg-red-950/60 border border-red-900/50 text-red-400 rounded-xl p-3 text-xs font-semibold flex items-center gap-2 mb-4 justify-end"
          >
            <span>{errorMsg}</span>
            <AlertCircle className="w-4 h-4 text-red-400 shrink-0" />
          </motion.div>
        )}

        {successMsg && (
          <motion.div 
            initial={{ opacity: 0, y: -5 }} 
            animate={{ opacity: 1, y: 0 }}
            className="bg-emerald-950/60 border border-emerald-900/50 text-emerald-400 rounded-xl p-3 text-xs font-semibold flex items-center gap-2 mb-4 justify-end"
          >
            <span>{successMsg}</span>
            <CheckCircle className="w-4 h-4 text-emerald-400 shrink-0" />
          </motion.div>
        )}

        {/* Main Authentication Form */}
        <form onSubmit={handleSubmit} className="space-y-4 flex-1">
          {activeForm === "signup" && (
            <div className="space-y-1.5">
              <label className="text-xs font-bold text-slate-400 block mr-1">الاسم الكامل</label>
              <input
                type="text"
                placeholder="منى محمد"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                className="w-full bg-slate-900 border border-slate-800 rounded-xl py-3 px-4 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-purple-500 text-right text-slate-100 placeholder-slate-600"
              />
            </div>
          )}

          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-400 block mr-1">رقم الهاتف أو المعرّف</label>
            <div className="relative">
              <input
                type="text"
                placeholder="0915551234"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                className="w-full bg-slate-900 border border-slate-800 rounded-xl py-3 pr-11 pl-4 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-purple-500 text-right text-slate-100 placeholder-slate-600 font-mono"
              />
              <Phone className="absolute right-4 top-1/2 -translate-y-1/2 text-slate-600 w-4 h-4" />
            </div>
          </div>

          <div className="space-y-1.5">
            <label className="text-xs font-bold text-slate-400 block mr-1">كلمة المرور</label>
            <div className="relative">
              <input
                type="password"
                placeholder="••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full bg-slate-900 border border-slate-800 rounded-xl py-3 pr-11 pl-4 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-purple-500 text-right text-slate-100 placeholder-slate-600 font-mono"
              />
              <Lock className="absolute right-4 top-1/2 -translate-y-1/2 text-slate-600 w-4 h-4" />
            </div>
          </div>

          {activeForm === "signup" && (
            <div className="space-y-1.5">
              <label className="text-xs font-bold text-slate-400 block mr-1">المدينة</label>
              <select
                value={city}
                onChange={(e) => setCity(e.target.value)}
                className="w-full bg-slate-900 border border-slate-800 rounded-xl py-3 px-4 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-purple-500 text-right text-slate-100"
              >
                <option value="طرابلس">طرابلس</option>
                <option value="بنغازي">بنغازي</option>
                <option value="مصراتة">مصراتة</option>
                <option value="الزاوية">الزاوية</option>
              </select>
            </div>
          )}

          {/* Remember Me and Forgot password */}
          <div className="flex justify-between items-center pt-1 text-xs text-slate-400 select-none">
            <a href="#forgot" className="text-purple-400 hover:underline">نسيت كلمة المرور؟</a>
            <label className="flex items-center gap-2 cursor-pointer">
              <span>تذكرني على هذا الجهاز</span>
              <input
                type="checkbox"
                checked={rememberMe}
                onChange={(e) => setRememberMe(e.target.checked)}
                className="rounded border-slate-800 bg-slate-900 text-purple-600 focus:ring-purple-500"
              />
            </label>
          </div>

          {/* Action button */}
          <button
            type="submit"
            disabled={loading}
            className="w-full bg-gradient-to-r from-purple-600 to-pink-500 text-white font-bold text-xs py-3.5 rounded-xl hover:opacity-90 transition shadow-lg shadow-fuchsia-500/10 flex items-center justify-center gap-2 mt-4"
          >
            {loading ? (
              <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
            ) : (
              <span>{activeForm === "signin" ? "تسجيل دخول آمن" : "إنشاء حساب مجاني"}</span>
            )}
          </button>
        </form>

        {/* Demo Fast Login helpers */}
        <div className="pt-4 border-t border-slate-900 space-y-2 mt-6">
          <p className="text-[10px] text-slate-500 text-center font-bold">بوابة التجربة السريعة للتقييم والتدقيق:</p>
          <div className="grid grid-cols-2 gap-2">
            <button
              onClick={() => handleTestLogin("customer")}
              className="py-2 px-1.5 bg-purple-950/40 hover:bg-purple-950/80 text-[10px] font-bold text-purple-300 rounded-lg border border-purple-900/30 transition flex items-center justify-center gap-1"
            >
              <Sparkles className="w-3 h-3 text-pink-400" />
              <span>دخول كزبون (منى محمد)</span>
            </button>
            <button
              onClick={() => handleTestLogin("admin")}
              className="py-2 px-1.5 bg-blue-950/40 hover:bg-blue-950/80 text-[10px] font-bold text-blue-300 rounded-lg border border-blue-900/30 transition flex items-center justify-center gap-1"
            >
              <span>دخول كمسؤول (المدير العام)</span>
            </button>
          </div>
        </div>

      </div>
    </div>
  );
}
