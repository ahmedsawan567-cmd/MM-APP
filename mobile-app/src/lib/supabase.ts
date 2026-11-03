import { createClient, SupabaseClient } from "@supabase/supabase-js";
import { UserProfile, Store, NotificationItem, ScanRecord } from "../types";
import { defaultProfile, defaultStores, defaultNotifications, defaultScans } from "../initialData";

// Supabase Configuration
// You can replace these with your project URL and public Anon Key from Supabase Settings -> API
export const SUPABASE_URL = import.meta.env.VITE_SUPABASE_URL || "https://almaraa-mall.supabase.co";
export const SUPABASE_ANON_KEY = import.meta.env.VITE_SUPABASE_ANON_KEY || "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImFsbWFyYWEtbWFsbCIsInJvbGUiOiJhbm9uIiwiaWF0IjoxNjgwMDAwMDAwLCJleHAiOjE5OTk5OTk5OTl9.mock_supabase_token";

let supabaseClient: SupabaseClient | null = null;

export function getSupabase(): SupabaseClient {
  if (!supabaseClient) {
    supabaseClient = createClient(SUPABASE_URL, SUPABASE_ANON_KEY, {
      auth: {
        persistSession: true,
        autoRefreshToken: true
      },
      realtime: {
        params: {
          eventsPerSecond: 10
        }
      }
    });
  }
  return supabaseClient;
}

/**
 * Fetch all official stores from Supabase (or fallback to cached/initial)
 */
export async function fetchStoresFromSupabase(): Promise<Store[]> {
  try {
    const supabase = getSupabase();
    const { data, error } = await supabase
      .from("stores")
      .select("*")
      .eq("is_active", true)
      .order("id", { ascending: true });

    if (error || !data || data.length === 0) {
      console.warn("Supabase stores query returned empty or error, using default stores:", error);
      return defaultStores;
    }

    return data.map((item: any) => ({
      id: item.id,
      name: item.name,
      logo: item.logo,
      category: item.category,
      description: item.description,
      currentStamps: item.current_stamps_default || 0,
      maxStamps: item.max_stamps || 10,
      rewardText: item.reward_text,
      moq: item.moq,
      supplierName: `${item.name} - ${item.unit_location}`,
      verified: item.verified ?? true,
      stampsHistory: []
    }));
  } catch (err) {
    console.warn("Could not connect to Supabase, loading fallback stores:", err);
    return defaultStores;
  }
}

/**
 * Fetch customer profile from Supabase
 */
export async function fetchProfileFromSupabase(phone: string = "0912345678"): Promise<UserProfile> {
  try {
    const supabase = getSupabase();
    const { data, error } = await supabase
      .from("profiles")
      .select("*")
      .eq("phone", phone)
      .maybeSingle();

    if (error || !data) {
      return defaultProfile;
    }

    return {
      name: data.name,
      cardId: data.card_id,
      points: data.points,
      stamps: data.stamps,
      prizes: data.prizes,
      phone: data.phone,
      birthdate: data.birthdate || "1998-05-12",
      gender: data.gender || "أنثى",
      city: data.city || "طرابلس",
      email: data.email || "mouna@mmapp.ly",
      avatar: data.avatar_url || ""
    };
  } catch (err) {
    return defaultProfile;
  }
}

/**
 * Fetch customer notifications from Supabase
 */
export async function fetchNotificationsFromSupabase(): Promise<NotificationItem[]> {
  try {
    const supabase = getSupabase();
    const { data, error } = await supabase
      .from("notifications")
      .select("*")
      .order("id", { ascending: false })
      .limit(20);

    if (error || !data || data.length === 0) {
      return defaultNotifications;
    }

    return data.map((n: any) => ({
      id: n.id,
      title: n.title,
      message: n.message,
      time: new Date(n.created_at).toLocaleTimeString("ar-LY", { hour: "2-digit", minute: "2-digit" }),
      section: n.section || "عام",
      logo: n.logo || "🦋",
      read: n.is_read || false
    }));
  } catch {
    return defaultNotifications;
  }
}

/**
 * Fetch scan records / invoices from Supabase
 */
export async function fetchScansFromSupabase(): Promise<ScanRecord[]> {
  try {
    const supabase = getSupabase();
    const { data, error } = await supabase
      .from("invoices_scans")
      .select("*")
      .order("created_at", { ascending: false })
      .limit(50);

    if (error || !data || data.length === 0) {
      return defaultScans;
    }

    return data.map((s: any) => ({
      id: s.scan_code || s.id,
      storeId: s.store_id,
      storeName: s.store_name,
      invoiceNo: s.invoice_no,
      date: new Date(s.invoice_date || s.created_at).toISOString().split("T")[0],
      amount: Number(s.amount),
      items: Array.isArray(s.items) ? s.items.map((it: any) => typeof it === "string" ? it : it.name || JSON.stringify(it)) : [],
      status: s.status as any,
      fraudRisk: s.fraud_risk as any,
      pHashMatch: s.phash_match || "0.00% (أصلية)",
      user: s.customer_name,
      stampsAwarded: s.stamps_awarded || 1
    }));
  } catch {
    return defaultScans;
  }
}

/**
 * Record a new invoice scan in Supabase with anti-fraud metadata
 */
export async function recordInvoiceScanToSupabase(scan: {
  storeId: string;
  storeName: string;
  invoiceNo: string;
  amount: number;
  items: string[];
  customerName: string;
  stampsAwarded: number;
  pointsEarned: number;
}): Promise<boolean> {
  try {
    const supabase = getSupabase();
    const scanCode = `SCN-${Date.now().toString().slice(-6)}`;
    
    const { error } = await supabase.from("invoices_scans").insert({
      scan_code: scanCode,
      customer_name: scan.customerName,
      store_id: scan.storeId,
      store_name: scan.storeName,
      invoice_no: scan.invoiceNo,
      amount: scan.amount,
      items: scan.items,
      status: "Approved",
      fraud_risk: "Low",
      phash_match: "0.00% (فريدة)",
      stamps_awarded: scan.stampsAwarded,
      points_earned: scan.pointsEarned,
      verified_by: "Gemini AI Vision"
    });

    if (error) {
      console.warn("Error inserting invoice into Supabase:", error);
      return false;
    }

    // Also insert notification
    await supabase.from("notifications").insert({
      title: `طوابع جديدة - ${scan.storeName} ✨`,
      message: `تم اعتماد فاتورتك بقيمة ${scan.amount} د.ل وحصولك على ${scan.stampsAwarded} طابع و ${scan.pointsEarned} نقطة بنجاح!`,
      section: "المتاجر",
      logo: "🧾",
      is_read: false
    });

    return true;
  } catch (err) {
    console.warn("Supabase insert exception:", err);
    return false;
  }
}

/**
 * Subscribe to realtime invoice and loyalty updates from Supabase
 */
export function subscribeToRealtimeLoyalty(
  onProfileUpdate: (profile: Partial<UserProfile>) => void,
  onNewNotification: (notif: NotificationItem) => void
) {
  try {
    const supabase = getSupabase();
    
    const channel = supabase
      .channel("almaraa_mall_realtime")
      .on(
        "postgres_changes",
        { event: "*", schema: "public", table: "profiles" },
        (payload: any) => {
          if (payload.new) {
            onProfileUpdate({
              points: payload.new.points,
              stamps: payload.new.stamps,
              prizes: payload.new.prizes
            });
          }
        }
      )
      .on(
        "postgres_changes",
        { event: "INSERT", schema: "public", table: "notifications" },
        (payload: any) => {
          if (payload.new) {
            onNewNotification({
              id: payload.new.id,
              title: payload.new.title,
              message: payload.new.message,
              time: "الآن",
              section: payload.new.section || "عام",
              logo: payload.new.logo || "🦋",
              read: payload.new.is_read || false
            });
          }
        }
      )
      .subscribe();

    return () => {
      supabase.removeChannel(channel);
    };
  } catch (err) {
    console.warn("Supabase Realtime not active, continuing locally:", err);
    return () => {};
  }
}

