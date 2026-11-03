export interface UserProfile {
  name: string;
  cardId: string;
  points: number;
  stamps: number;
  prizes: number;
  phone: string;
  birthdate: string;
  gender: string;
  city: string;
  email: string;
  avatar: string;
}

export interface StampHistory {
  date: string;
  amount: string;
  count: number;
}

export interface Store {
  id: string;
  name: string;
  logo: string;
  category: string;
  description: string;
  currentStamps: number;
  maxStamps: number;
  rewardText: string;
  moq: string;
  supplierName: string;
  verified: boolean;
  stampsHistory: StampHistory[];
}

export interface NotificationItem {
  id: number;
  title: string;
  message: string;
  time: string;
  section: string;
  logo: string;
  read: boolean;
}

export interface ScanRecord {
  id: string;
  storeId: string;
  storeName: string;
  invoiceNo: string;
  date: string;
  amount: number;
  items: string[];
  status: "Approved" | "Pending" | "Flagged";
  fraudRisk: "Low" | "Medium" | "High";
  pHashMatch: string;
  user: string;
  stampsAwarded: number;
}

export interface AdminStats {
  totalSales: number;
  totalPoints: number;
  pendingAudits: number;
  dailyScans: number;
  poissonArrivalRate: number;
  meanBasketSize: number;
  standardDeviation: number;
  zScore: number;
  distributionData: Array<{ k: number; actual: number; expected: number }>;
}
