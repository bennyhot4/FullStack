// Response shapes of the HourOffice API, hand-written from backend/HourOffice.Api/Endpoints/Dtos.cs
// and the anonymous objects in Endpoints/*.cs (JSON is camelCase). When a DTO changes in the API,
// update this file in the same PR. Dates are ISO strings; "date" fields are "yyyy-MM-dd".

export type Role = "admin" | "user";
export type UserStatus = "active" | "invited";
export type BookingStatus = "confirmed" | "cancelled" | "completed";

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  pictureUrl: string | null;
  role: Role;
  status: UserStatus;
  termsAccepted: boolean;
  termsAcceptedVersion: number | null;
  termsAcceptedAt: string | null;
  createdAt: string;
}

/** GET /api/me */
export interface Me {
  user: UserDto;
  needsProfile: boolean;
  needsTerms: boolean;
  currentTermsVersion: number;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: UserDto;
}

export interface OfficeDto {
  id: string;
  name: string;
  description: string;
  imageUrl: string;
  capacity: number;
  hourlyRate: number;
  currency: string;
  amenities: string[];
  location: string;
  isAvailable: boolean;
  createdAt: string;
}

/** GET /api/offices/{id}/availability */
export interface Availability {
  date: string;
  bookedSlots: string[];
  slots: string[];
  openHour: number;
  closeHour: number;
  today: string;
  nowHour: number;
  maxDaysAhead: number;
}

export interface BookingDto {
  id: string;
  officeId: string;
  officeName: string;
  userId: string;
  bookerName: string;
  date: string;
  startTime: string;
  endTime: string;
  durationHours: number;
  totalPrice: number;
  currency: string;
  status: BookingStatus;
  notes: string;
  seriesId: string | null;
  cancelledAt: string | null;
  createdAt: string;
}

/** A booking as its owner sees it (only /api/bookings/mine and POST /api/bookings return the client). */
export interface MyBookingDto extends BookingDto {
  clientId: string | null;
  clientName: string | null;
}

/** GET /api/bookings/mine */
export interface MyBookings {
  upcoming: MyBookingDto[];
  past: MyBookingDto[];
}

export interface ClientDto {
  clientId: string;
  firstName: string;
  lastName: string;
  phones: string[];
  emails: string[];
  language: "he" | "en";
  whatsAppConsent: boolean;
  whatsAppConsentAt: string | null;
  notes: string;
  createdAt: string;
  updatedAt: string;
}

export interface ClientRequest {
  firstName: string;
  lastName: string;
  phones: string[];
  emails: string[];
  language: string;
  whatsAppConsent: boolean;
  notes: string;
}

export interface Branding {
  title: string;
  description: string;
  logo: string;
  buttonsColor: string;
  backgroundColor: string;
  pagesColor: string;
}

export interface Banner {
  id: string;
  url: string;
}

/** GET /api/settings/public */
export interface PublicSettings {
  branding: Branding;
  homeBannerImage: string;
  banners: Banner[];
  currency: { base: string; displaySymbols: Record<string, string> };
  booking: { openHour: number; closeHour: number; maxDaysAhead: number; timeZone: string };
  auth: { googleClientId: string | null; devLoginEnabled: boolean; allowOpenSignup: boolean };
}

/** GET /api/terms/current */
export interface TermsDoc {
  version: number;
  contentHe: string;
  contentEn: string;
  publishedAt: string;
}

/** GET /api/admin/analytics */
export interface Analytics {
  period: "week" | "month";
  from: string;
  to: string;
  totalBookings: number;
  totalHours: number;
  revenue: number;
  byUser: { userId: string; fullName: string; email: string; bookings: number; hours: number; spent: number }[];
}

/** POST /api/admin/bookings/recurring */
export interface RecurringResult {
  created: number;
  skipped: number;
  seriesId: string;
}
