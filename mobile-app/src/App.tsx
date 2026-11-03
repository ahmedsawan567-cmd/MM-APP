import React, { useState, useEffect } from "react";
import { Smartphone, ShieldCheck, RefreshCw, LogOut } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import { UserProfile, Store, NotificationItem, ScanRecord, AdminStats } from "./types";
import { defaultProfile, defaultStores, defaultNotifications, defaultScans, defaultStats } from "./initialData";
import { 
  subscribeToRealtimeLoyalty, 
  fetchStoresFromSupabase, 
  fetchProfileFromSupabase,
  fetchNotificationsFromSupabase,
  fetchScansFromSupabase
} from "./lib/supabase";
import CustomerApp from "./components/CustomerApp";
import AdminPortal from "./components/AdminPortal";
import Login from "./components/Login";

export default function App() {
  const [isLoggedIn, setIsLoggedIn] = useState<boolean>(false);
  const [userRole, setUserRole] = useState<"customer" | "admin">("customer");
  const [viewMode, setViewMode] = useState<"customer" | "admin">("customer");
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [stores, setStores] = useState<Store[]>(defaultStores);
  const [notifications, setNotifications] = useState<NotificationItem[]>(defaultNotifications);
  const [scans, setScans] = useState<ScanRecord[]>(defaultScans);
  const [stats, setStats] = useState<AdminStats | null>(defaultStats);
  const [loading, setLoading] = useState(true);
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  // Check persistent login token from SharedPreferences/LocalStorage on mount
  useEffect(() => {
    const savedToken = localStorage.getItem("remember_token");
    if (savedToken) {
      if (savedToken.includes("admin")) {
        setUserRole("admin");
        setViewMode("admin");
      } else {
        setUserRole("customer");
        setViewMode("customer");
      }
      setIsLoggedIn(true);
    }
  }, []);

  // Fetch all live data from Supabase Cloud
  useEffect(() => {
    async function fetchData() {
      try {
        const [supabaseStores, supabaseProfile, supabaseNotifs, supabaseScans] = await Promise.all([
          fetchStoresFromSupabase(),
          fetchProfileFromSupabase(),
          fetchNotificationsFromSupabase(),
          fetchScansFromSupabase()
        ]);

        setStores(supabaseStores.length > 0 ? supabaseStores : defaultStores);
        setProfile(supabaseProfile || defaultProfile);
        setNotifications(supabaseNotifs.length > 0 ? supabaseNotifs : defaultNotifications);
        setScans(supabaseScans.length > 0 ? supabaseScans : defaultScans);
        setStats(defaultStats);
      } catch (err) {
        console.warn("Supabase fetch fallback to local:", err);
        setProfile(defaultProfile);
        setStores(defaultStores);
        setNotifications(defaultNotifications);
        setScans(defaultScans);
        setStats(defaultStats);
      } finally {
        setLoading(false);
      }
    }

    if (isLoggedIn) {
      fetchData();
    } else {
      setLoading(false);
    }
  }, [refreshTrigger, isLoggedIn]);

  // Supabase Realtime Subscription for instant live sync
  useEffect(() => {
    if (!isLoggedIn) return;

    const unsubscribe = subscribeToRealtimeLoyalty(
      (updatedFields) => {
        setProfile((prev) => (prev ? { ...prev, ...updatedFields } : prev));
      },
      (newNotif) => {
        setNotifications((prev) => [newNotif, ...prev]);
      }
    );

    return () => {
      unsubscribe();
    };
  }, [isLoggedIn]);

  const handleRefresh = () => {
    setRefreshTrigger((prev) => prev + 1);
  };

  const handleProfileUpdate = (newProfile: Partial<UserProfile>) => {
    if (profile) {
      setProfile({ ...profile, ...newProfile });
    }
  };

  const handleLoginSuccess = (role: "customer" | "admin", username: string) => {
    setUserRole(role);
    setViewMode(role);
    setIsLoggedIn(true);
    handleRefresh();
  };

  const handleLogout = () => {
    localStorage.removeItem("remember_token");
    setIsLoggedIn(false);
    setProfile(null);
  };

  // Loading State
  if (loading || (isLoggedIn && (!profile || !stats))) {
    return (
      <div className="min-h-screen bg-slate-950 flex flex-col items-center justify-center text-slate-100 font-sans p-6 text-center">
        {/* Elegant butterfly loader */}
        <div className="relative mb-6">
          <div className="w-16 h-16 rounded-full border-4 border-slate-900 border-t-fuchsia-500 animate-spin" />
          <span className="absolute inset-0 flex items-center justify-center text-xl">🦋</span>
        </div>
        <h3 className="font-bold text-lg text-white">المرأة مول | Almaraa Mall</h3>
        <p className="text-xs text-slate-400 mt-2">جاري مزامنة بيانات بطاقات الولاء والمتاجر بالذكاء الاصطناعي</p>
      </div>
    );
  }

  // Not Logged In -> Full Screen Native Login
  if (!isLoggedIn) {
    return <Login onLoginSuccess={handleLoginSuccess} />;
  }

  // Admin Portal View (Clean Full-Width Dashboard with return toggle)
  if (viewMode === "admin") {
    return (
      <div className="min-h-screen bg-slate-950 flex flex-col text-slate-100 font-sans antialiased">
        {/* Admin Navigation Header Bar */}
        <header className="bg-slate-900/95 backdrop-blur-md border-b border-slate-800 py-3 px-4 sm:px-6 sticky top-0 z-50 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="bg-gradient-to-tr from-purple-600 to-pink-500 p-2 rounded-xl text-white">
              <ShieldCheck className="w-5 h-5" />
            </div>
            <div>
              <h2 className="font-extrabold text-sm text-white">لوحة الإدارة والتدقيق (B2B)</h2>
              <p className="text-[10px] text-purple-300">المرأة مول - منصة الرقابة ومكافحة الاحتيال</p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={() => setViewMode("customer")}
              className="py-1.5 px-3 bg-purple-600 hover:bg-purple-700 text-white rounded-xl text-xs font-bold transition flex items-center gap-1.5 shadow-md shadow-fuchsia-500/20"
            >
              <Smartphone className="w-3.5 h-3.5" />
              <span>تطبيق الهاتف</span>
            </button>
            <button
              onClick={handleRefresh}
              className="p-2 bg-slate-800 hover:bg-slate-750 text-slate-300 rounded-xl transition border border-slate-700"
              title="تحديث البيانات"
            >
              <RefreshCw className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={handleLogout}
              className="p-2 bg-red-950/50 hover:bg-red-900/60 text-red-400 rounded-xl transition border border-red-900/40"
              title="تسجيل الخروج"
            >
              <LogOut className="w-3.5 h-3.5" />
            </button>
          </div>
        </header>

        <main className="flex-1 max-w-7xl w-full mx-auto p-4 sm:p-6">
          <AdminPortal
            stores={stores}
            scans={scans}
            stats={stats!}
            onRefresh={handleRefresh}
          />
        </main>
      </div>
    );
  }

  // Customer View -> 100% Pure Native Full-Screen Mobile App
  return (
    <div className="min-h-screen bg-slate-950 w-full font-sans antialiased text-slate-800">
      <AnimatePresence mode="wait">
        <motion.div
          key="customer-app"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.2 }}
          className="w-full min-h-screen"
        >
          <CustomerApp
            profile={profile!}
            stores={stores}
            notifications={notifications}
            onRefresh={handleRefresh}
            onProfileUpdate={handleProfileUpdate}
            onLogout={handleLogout}
            onSwitchToAdmin={userRole === "admin" || localStorage.getItem("remember_token")?.includes("admin") ? () => setViewMode("admin") : undefined}
          />
        </motion.div>
      </AnimatePresence>
    </div>
  );
}
