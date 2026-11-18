// UI texts. Keys from the original app are kept so the wording stays the same.
// `he` is typed as `typeof en`, so a key missing in either language fails `npm run typecheck`.

const en = {
  premiumOffices: "Premium Office Spaces", bookProfessional: "Book professional workspaces by the hour",
  flexibleBooking: "Flexible Booking", chooseYourTime: "Choose your time, choose your space",
  modernAmenities: "Modern Amenities", everythingYouNeed: "Everything you need to work productively",
  perfectForTeams: "Perfect for Teams", spacesForCollaboration: "Spaces designed for collaboration",
  exploreOffices: "Explore Offices", hourly: "Hourly", primeLocation: "Prime Location", anySize: "Any Size",
  availableSpaces: "Available spaces ready to book instantly", officeBooking: "Office Booking",
  findAndBook: "Find and book by the hour",
  people: "people", bookNow: "Book Now", noOfficesFound: "No offices found", reviewDetails: "Review Booking Details",
  selectDate: "Select Date", selectTime: "Select Time", selectTimeRange: "Tap a start hour, then an end hour",
  notesOptional: "Notes (optional)", anySpecialReq: "Any special requirements...", totalBookings: "Total Bookings",
  totalHours: "Total Hours", revenue: "Revenue", hours: "hours", hour: "hour", confirmBooking: "Confirm Booking",
  booking: "Booking...", bookingConfirmed: "Booking Confirmed!", viewMyBookings: "View My Bookings", bookAnother: "Book Another",
  officeNotFound: "Office not found", hourlyRate: "Hourly Rate", unavailable: "Unavailable", bookedSlot: "Booked",
  maxTwoWeeks: "Bookings available up to 2 weeks ahead", myBookings: "My Bookings", upcoming: "Upcoming", past: "Past",
  noUpcomingBookings: "No upcoming bookings", noPastBookings: "No past bookings", bookAnOffice: "Book an Office",
  cancelBooking: "Cancel Booking?", areYouSure: "Are you sure you want to cancel your booking for",
  cannotBeUndone: "This action cannot be undone.", keepBooking: "Keep Booking", yesCancel: "Yes, Cancel", cancel: "Cancel",
  cancelBookingBtn: "Cancel Booking", confirmed: "Confirmed", cancelled: "Cancelled", completed: "Completed",
  manageOffices: "Manage Offices", editPricing: "Edit pricing and availability", addOffice: "Add Office", editOffice: "Edit Office",
  addNewOffice: "Add New Office", officeName: "Office Name", description: "Description", image: "Image",
  capacity: "Capacity (people)", location: "Location", amenities: "Amenities (comma separated)",
  availableForBooking: "Available for Booking", yes: "Yes", no: "No", create: "Create", update: "Update",
  deleteOffice: "Delete Office?", deleteOfficeConfirm: "Are you sure you want to delete", delete: "Delete", available: "Available",
  manageUsers: "Manage Users", totalUsers: "total user", totalUsersPlural: "total users", inviteUser: "Add User",
  inviteNewUser: "Add New User", emailAddress: "Email Address", role: "Role", user: "User", admin: "Admin",
  sendInvite: "Add User", inviting: "Adding...", administrators: "Administrators", users: "Users", joined: "Joined",
  noUsersYet: "No users yet", deleteUser: "Delete User?", deleteUserConfirm: "Are you sure you want to delete",
  willRemoveBookings: "This will also remove all their bookings.", setAsAdmin: "Make Admin", setAsUser: "Make User",
  updatingRole: "Updating...", usageAnalytics: "Usage Analytics", thisWeek: "This Week", thisMonth: "This Month",
  usageByUser: "Usage by User", bookings: "Bookings", spent: "Spent", noBookingsInPeriod: "No bookings in this period",
  adminAccessRequired: "Admin access required", manageBookings: "Manage Bookings", allBookings: "total bookings",
  editBooking: "Edit Booking", bookingName: "Booking Name", noBookings: "No bookings yet", explore: "Explore",
  offices: "Offices", analytics: "Analytics", profileSetupTitle: "Complete Your Profile",
  profileSetupSubtitle: "Please fill in your details to continue", firstName: "First Name", lastName: "Last Name",
  phoneNumber: "Phone Number", save: "Save & Continue", saving: "Saving...",
  termsAccept: "I Accept the Terms", termsAccepting: "Accepting...", termsNotSigned: "Not signed", termsSigned: "Signed",
  termsSignedOn: "Signed on", termsTitle: "Terms of Use", termsViewTerms: "View Terms of Use",
  privacyTitle: "Privacy Policy", privacyViewPolicy: "Privacy Policy", privacyLastUpdated: "Last updated",
  termsReadAndAccept: "Read and accept to continue",
  // New in the rebuild
  welcomeTo: "Welcome to", signInToContinue: "Sign in to continue", signInWithGoogle: "Sign in with Google",
  devLogin: "Developer sign-in", devLoginHint: "Local development only", name: "Name", signIn: "Sign in",
  logout: "Log out", language: "Language", accessRestricted: "Access Restricted",
  notRegistered: "You are not registered to use this application. Please contact the app administrator to request access.",
  pageNotFound: "Page Not Found", goHome: "Go Home", back: "Back", retry: "Try again", loadingFailed: "Something went wrong, please try again.",
  invalidPhone: "Please enter a valid phone number", settings: "Settings", siteSettings: "Site Settings",
  homeBanners: "Home page banners", addBanner: "Add banner", uploadImage: "Upload image", orPasteUrl: "Or paste image URL",
  uploading: "Uploading...", saveSettings: "Save Settings", saved: "Saved", branding: "Branding", pageTitle: "Page Title",
  pageDescription: "Page Description", logo: "Logo", buttonsColor: "Buttons color", backgroundColor: "Background color",
  pagesColor: "Cards color", saveBranding: "Save Branding", mainBannerImage: "Main banner image",
  editTerms: "Edit terms of use", termsHebrew: "Hebrew", termsEnglish: "English", publishNewVersion: "Publish new version",
  publishTermsConfirm: "Publishing a new version will require every user to sign again. Continue?",
  version: "Version", repeatedBooking: "Repeated Booking", office: "Office", bookFor: "Book for", startTime: "Start",
  endTime: "End", fromDate: "From date", toDate: "To date", days: "Days", createBookings: "Create bookings",
  bookingsCreated: "bookings created", slotsSkipped: "slots already booked (skipped)", done: "Done", date: "Date",
  edit: "Edit", editUser: "Edit User", fullName: "Full Name", invited: "Invited", userBookings: "Bookings of",
  selectOffice: "Select office", selectUser: "Select user", rank: "#", cancelledOn: "Cancelled on", total: "Total",
  duration: "Duration", slotTaken: "One or more of the selected hours were just booked. Please choose again.",
  rangeHasBooked: "The selected range includes booked hours.", chooseEndAfterStart: "Tap an end hour after the start hour.",
  notesLabel: "Notes", openHours: "Opening hours", you: "You", amenitiesShort: "Amenities",
  noBannersYet: "No banners yet", signedTermsRequired: "Please sign the terms of use before booking.",
  perHour: "/hr", dayShort: ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"],
  // Clients (plan 002)
  myClients: "My Clients", addClient: "Add Client", editClient: "Edit Client", deleteClient: "Delete Client?",
  deleteClientConfirm: "Are you sure you want to delete", noClients: "No clients yet",
  filterClients: "Search by name, phone or email", clientLanguage: "Message language", langHe: "Hebrew", langEn: "English",
  whatsAppConsent: "The client agreed to receive messages",
  whatsAppConsentHint: "Mark only with the client's explicit consent. No messages are sent yet.",
  clientSaved: "Client saved", clientDeleted: "Client deleted", clientsPrivateNote: "Only you can see this list.",
  addPhone: "+ Add phone", secondPhone: "Second phone", addEmail: "+ Add email", secondEmail: "Second email",
  client: "Client", chooseClient: "Choose client", noClient: "No client", newClient: "+ New client",
  changeClient: "Change client", allClients: "All clients", filterByClient: "Filter by client",
  bookingsWithClient: "Bookings with this client",
  notesVisibleToAdmin: "* Notes are visible to the admin. Pick the client above instead of writing client details here.",
  adminMenu: "Admin", email: "Email", clientNotes: "Notes", clientNotesHint: "Do not store medical or therapy information.",
  saveClient: "Save",
  // Error texts keyed by the API error code (see errorText in ui.js)
  client_name_required: "Please enter the client's first name.", phone_required: "Please enter at least one phone number.",
  invalid_phone: "Please enter a valid phone number.", invalid_email: "Please enter a valid email address.",
  invalid_language: "Please choose Hebrew or English.", too_many_phones: "Up to 2 phone numbers.",
  too_many_emails: "Up to 2 email addresses.", notes_too_long: "The notes are too long.",
  client_limit: "You have reached the limit of 1,000 clients.", client_not_found: "This client no longer exists.",
  terms_required: "Please sign the terms of use first.",
  // Distribution lists + registered users as clients (plan 008)
  distributionLists: "Distribution Lists", distributionList: "Distribution list",
  newDistributionList: "New list", editDistributionList: "Edit list", deleteDistributionList: "Delete list?",
  deleteDistributionListConfirm: "The clients stay in your client list. Delete the list",
  noDistributionLists: "No distribution lists yet", distributionListsNote: "Group your clients for future updates. Only you can see your lists.",
  listName: "List name", listDescription: "Description (optional)", listMembers: "Clients in the list",
  selectAllShown: "Select all shown", clearSelection: "Clear", clientsCount: "clients", selectedCount: "selected",
  distributionListSaved: "List saved", distributionListDeleted: "List deleted",
  registeredUser: "Registered user of the site",
  marketingConsent: "I agree to receive updates and offers",
  list_name_required: "Please enter a list name (up to 60 characters).",
  list_description_too_long: "The description is too long.",
  too_many_list_clients: "A list can hold up to 1,000 clients.",
  list_limit: "You have reached the limit of 100 lists.",
  distribution_list_not_found: "This list no longer exists.",
};

const he: typeof en = {
  premiumOffices: "משרדים פרימיום", bookProfessional: "הזמינו משרדים מקצועיים לפי שעה", flexibleBooking: "הזמנה גמישה",
  chooseYourTime: "בחרו את הזמן, בחרו את המרחב", modernAmenities: "שירותים מודרניים",
  everythingYouNeed: "כל מה שצריך לעבודה פרודוקטיבית", perfectForTeams: "מושלם לצוותים",
  spacesForCollaboration: "מרחבים המיועדים לשיתוף פעולה", exploreOffices: "חקרו משרדים", hourly: "לפי שעה",
  primeLocation: "מיקום מרכזי", anySize: "כל גודל", availableSpaces: "מרחבים זמינים להזמנה מיידית",
  officeBooking: "הזמנת משרדים", findAndBook: "מצאו והזמינו לפי שעה",
  people: "אנשים", bookNow: "הזמן עכשיו",
  noOfficesFound: "לא נמצאו משרדים", reviewDetails: "בדקו פרטי הזמנה", selectDate: "בחירת תאריך", selectTime: "בחירת שעה",
  selectTimeRange: "הקישו על שעת התחלה ואז על שעת סיום", notesOptional: "הערות (אופציונלי)", anySpecialReq: "דרישות מיוחדות...",
  totalBookings: "סה״כ הזמנות", totalHours: "סה״כ שעות", revenue: "הכנסות", hours: "שעות", hour: "שעה",
  confirmBooking: "אישור הזמנה", booking: "מזמין...", bookingConfirmed: "ההזמנה אושרה!", viewMyBookings: "צפייה בהזמנות שלי",
  bookAnother: "הזמנה נוספת", officeNotFound: "המשרד לא נמצא", hourlyRate: "תעריף שעתי", unavailable: "לא זמין",
  bookedSlot: "תפוס", maxTwoWeeks: "ניתן להזמין עד שבועיים מראש", myBookings: "ההזמנות שלי", upcoming: "קרובות", past: "עבר",
  noUpcomingBookings: "אין הזמנות קרובות", noPastBookings: "אין הזמנות קודמות", bookAnOffice: "הזמינו משרד",
  cancelBooking: "לבטל הזמנה?", areYouSure: "האם אתם בטוחים שברצונכם לבטל את ההזמנה עבור",
  cannotBeUndone: "פעולה זו לא ניתנת לביטול.", keepBooking: "שמור הזמנה", yesCancel: "כן, בטל", cancel: "ביטול",
  cancelBookingBtn: "בטל הזמנה", confirmed: "מאושר", cancelled: "בוטל", completed: "הושלם", manageOffices: "ניהול משרדים",
  editPricing: "עריכת תמחור וזמינות", addOffice: "הוספת משרד", editOffice: "עריכת משרד", addNewOffice: "הוספת משרד חדש",
  officeName: "שם המשרד", description: "תיאור", image: "תמונה", capacity: "קיבולת (אנשים)", location: "מיקום",
  amenities: "שירותים (מופרדים בפסיק)", availableForBooking: "זמין להזמנה", yes: "כן", no: "לא", create: "יצירה",
  update: "עדכון", deleteOffice: "למחוק משרד?", deleteOfficeConfirm: "האם אתם בטוחים שברצונכם למחוק את", delete: "מחיקה",
  available: "זמין", manageUsers: "ניהול משתמשים", totalUsers: "משתמש", totalUsersPlural: "משתמשים",
  inviteUser: "הוספת משתמש", inviteNewUser: "הוספת משתמש חדש", emailAddress: "כתובת אימייל", role: "תפקיד", user: "משתמש",
  admin: "מנהל", sendInvite: "הוספה", inviting: "מוסיף...", administrators: "מנהלים", users: "משתמשים", joined: "הצטרף",
  noUsersYet: "אין משתמשים עדיין", deleteUser: "למחוק משתמש?", deleteUserConfirm: "האם אתם בטוחים שברצונכם למחוק את",
  willRemoveBookings: "פעולה זו תמחק גם את כל ההזמנות שלו.", setAsAdmin: "הגדר כמנהל", setAsUser: "הגדר כמשתמש",
  updatingRole: "מעדכן...", usageAnalytics: "ניתוח שימוש", thisWeek: "השבוע", thisMonth: "החודש",
  usageByUser: "שימוש לפי משתמש", bookings: "הזמנות", spent: "הוצא", noBookingsInPeriod: "אין הזמנות בתקופה זו",
  adminAccessRequired: "נדרשת הרשאת מנהל", manageBookings: "ניהול הזמנות", allBookings: "הזמנות", editBooking: "עריכת הזמנה",
  bookingName: "שם ההזמנה", noBookings: "אין הזמנות עדיין", explore: "חדרים", offices: "משרדים", analytics: "ניתוח",
  profileSetupTitle: "השלמת פרופיל", profileSetupSubtitle: "אנא מלאו את פרטיכם כדי להמשיך", firstName: "שם פרטי",
  lastName: "שם משפחה", phoneNumber: "מספר טלפון", save: "שמור והמשך", saving: "שומר...",
  termsAccept: "אני מאשר את התנאים", termsAccepting: "מאשר...", termsNotSigned: "לא נחתם", termsSigned: "נחתם",
  termsSignedOn: "נחתם ב", termsTitle: "תנאי שימוש", termsViewTerms: "צפייה בתנאי שימוש",
  privacyTitle: "מדיניות פרטיות", privacyViewPolicy: "מדיניות פרטיות", privacyLastUpdated: "עודכן",
  termsReadAndAccept: "קראו את התקנון ואשרו כדי להמשיך",
  welcomeTo: "ברוכים הבאים ל", signInToContinue: "התחברו כדי להמשיך", signInWithGoogle: "התחברות עם Google",
  devLogin: "כניסת מפתחים", devLoginHint: "לפיתוח מקומי בלבד", name: "שם", signIn: "כניסה", logout: "התנתקות",
  language: "שפה", accessRestricted: "הגישה מוגבלת",
  notRegistered: "אינכם רשומים לשימוש באפליקציה. פנו למנהל המערכת כדי לקבל גישה.", pageNotFound: "הדף לא נמצא",
  goHome: "לדף הבית", back: "חזרה", retry: "נסו שוב", loadingFailed: "אירעה שגיאה, אנא נסו שנית.",
  invalidPhone: "אנא הזינו מספר טלפון תקין", settings: "הגדרות", siteSettings: "הגדרות האתר", homeBanners: "באנרים לדף הבית",
  addBanner: "הוספת באנר", uploadImage: "העלאת תמונה", orPasteUrl: "או הדביקו כתובת תמונה", uploading: "מעלה...",
  saveSettings: "שמירת הגדרות", saved: "נשמר", branding: "מיתוג", pageTitle: "כותרת הדף", pageDescription: "תיאור הדף",
  logo: "לוגו", buttonsColor: "צבע כפתורים", backgroundColor: "צבע רקע", pagesColor: "צבע כרטיסים", saveBranding: "שמירת מיתוג",
  mainBannerImage: "תמונת באנר ראשית", editTerms: "עריכת תנאי השימוש", termsHebrew: "עברית", termsEnglish: "אנגלית",
  publishNewVersion: "פרסום גרסה חדשה", publishTermsConfirm: "פרסום גרסה חדשה יחייב את כל המשתמשים לחתום מחדש. להמשיך?",
  version: "גרסה", repeatedBooking: "הזמנה חוזרת", office: "משרד", bookFor: "עבור", startTime: "התחלה", endTime: "סיום",
  fromDate: "מתאריך", toDate: "עד תאריך", days: "ימים", createBookings: "יצירת הזמנות", bookingsCreated: "הזמנות נוצרו",
  slotsSkipped: "משבצות כבר תפוסות (דולגו)", done: "סיום", date: "תאריך", edit: "עריכה", editUser: "עריכת משתמש",
  fullName: "שם מלא", invited: "ממתין לכניסה", userBookings: "ההזמנות של", selectOffice: "בחרו משרד", selectUser: "בחרו משתמש",
  rank: "#", cancelledOn: "בוטל ב", total: "סה״כ", duration: "משך",
  slotTaken: "אחת או יותר מהשעות שבחרתם נתפסה הרגע. אנא בחרו שוב.", rangeHasBooked: "הטווח שבחרתם כולל שעות תפוסות.",
  chooseEndAfterStart: "בחרו שעת סיום אחרי שעת ההתחלה.", notesLabel: "הערות", openHours: "שעות פעילות", you: "את/ה",
  amenitiesShort: "שירותים", noBannersYet: "אין באנרים עדיין", signedTermsRequired: "יש לחתום על תנאי השימוש לפני הזמנה.",
  perHour: "לשעה", dayShort: ["א׳", "ב׳", "ג׳", "ד׳", "ה׳", "ו׳", "ש׳"],
  // Clients (plan 002)
  myClients: "הלקוחות שלי", addClient: "הוספת לקוח", editClient: "עריכת לקוח", deleteClient: "למחוק את הלקוח?",
  deleteClientConfirm: "האם אתם בטוחים שברצונכם למחוק את", noClients: "אין לקוחות עדיין",
  filterClients: "חיפוש לפי שם, טלפון או דוא\"ל", clientLanguage: "שפת הודעות", langHe: "עברית", langEn: "אנגלית",
  whatsAppConsent: "הלקוח הסכים לקבל הודעות",
  whatsAppConsentHint: "סמנו רק בהסכמה מפורשת של הלקוח. כרגע לא נשלחות הודעות.",
  clientSaved: "הלקוח נשמר", clientDeleted: "הלקוח נמחק", clientsPrivateNote: "רק את/ה רואה את הרשימה הזאת.",
  addPhone: "+ הוספת טלפון", secondPhone: "טלפון נוסף", addEmail: "+ הוספת דוא\"ל", secondEmail: "דוא\"ל נוסף",
  client: "לקוח", chooseClient: "בחירת לקוח", noClient: "ללא לקוח", newClient: "+ לקוח חדש",
  changeClient: "שינוי לקוח", allClients: "כל הלקוחות", filterByClient: "סינון לפי לקוח",
  bookingsWithClient: "הזמנות עם הלקוח",
  notesVisibleToAdmin: "* ההערות גלויות למנהל. בחרו לקוח למעלה במקום לכתוב כאן את פרטי הלקוח.",
  adminMenu: "ניהול", email: "דוא\"ל", clientNotes: "הערות", clientNotesHint: "אין לשמור כאן מידע רפואי או טיפולי.",
  saveClient: "שמירה",
  client_name_required: "יש להזין שם פרטי ללקוח.", phone_required: "יש להזין לפחות מספר טלפון אחד.",
  invalid_phone: "מספר הטלפון אינו תקין.", invalid_email: "כתובת הדוא\"ל אינה תקינה.",
  invalid_language: "יש לבחור עברית או אנגלית.", too_many_phones: "עד 2 מספרי טלפון.",
  too_many_emails: "עד 2 כתובות דוא\"ל.", notes_too_long: "ההערות ארוכות מדי.",
  client_limit: "הגעתם למגבלה של 1,000 לקוחות.", client_not_found: "הלקוח הזה כבר לא קיים.",
  terms_required: "יש לחתום על תנאי השימוש תחילה.",
  // Distribution lists + registered users as clients (plan 008)
  distributionLists: "רשימות תפוצה", distributionList: "רשימת תפוצה",
  newDistributionList: "רשימה חדשה", editDistributionList: "עריכת רשימה", deleteDistributionList: "למחוק את הרשימה?",
  deleteDistributionListConfirm: "הלקוחות יישארו ברשימת הלקוחות שלך. למחוק את הרשימה",
  noDistributionLists: "אין רשימות תפוצה עדיין", distributionListsNote: "קבצו לקוחות לעדכונים עתידיים. רק את/ה רואה את הרשימות.",
  listName: "שם הרשימה", listDescription: "תיאור (לא חובה)", listMembers: "לקוחות ברשימה",
  selectAllShown: "בחירת כל המוצגים", clearSelection: "ניקוי", clientsCount: "לקוחות", selectedCount: "נבחרו",
  distributionListSaved: "הרשימה נשמרה", distributionListDeleted: "הרשימה נמחקה",
  registeredUser: "משתמש רשום באתר",
  marketingConsent: "אני מסכים/ה לקבל עדכונים והצעות",
  list_name_required: "יש להזין שם לרשימה (עד 60 תווים).",
  list_description_too_long: "התיאור ארוך מדי.",
  too_many_list_clients: "רשימה יכולה להכיל עד 1,000 לקוחות.",
  list_limit: "הגעתם למגבלה של 100 רשימות.",
  distribution_list_not_found: "הרשימה הזאת כבר לא קיימת.",
};

export type Lang = "en" | "he";
type Dict = typeof en;
/** Every key whose text is a plain string (all of them except list values such as `dayShort`). */
export type TranslationKey = { [K in keyof Dict]: Dict[K] extends string ? K : never }[keyof Dict];

const dict: Record<Lang, Dict> = { en, he };
const KEY = "appLanguage"; // same key as the original app

let lang: Lang = (() => {
  try {
    const v = localStorage.getItem(KEY);
    return v === "he" || v === "en" ? v : "en";
  } catch { return "en"; }
})();
const listeners = new Set<() => void>();

export const getLang = (): Lang => lang;
export const isRtl = () => lang === "he";
export const locale = () => (lang === "he" ? "he-IL" : "en-US");

export function t(key: TranslationKey): string {
  return dict[lang][key] ?? dict.en[key] ?? key;
}

/** Short weekday names, Sunday first. */
export const dayShort = (): readonly string[] => dict[lang].dayShort;

/** True when a text exists for this key (used to translate API error codes). */
export const hasKey = (key: unknown): key is TranslationKey =>
  typeof key === "string" && typeof (dict[lang] as Record<string, unknown>)[key] === "string";

export function applyDocumentLang() {
  document.documentElement.lang = lang;
  document.documentElement.dir = lang === "he" ? "rtl" : "ltr";
}

export function setLang(next: Lang) {
  if (next === lang) return;
  lang = next;
  try { localStorage.setItem(KEY, next); } catch { /* private mode */ }
  applyDocumentLang();
  listeners.forEach((fn) => fn());
}

/** For useSyncExternalStore: the app re-renders every page when the language changes. */
export function subscribeLang(fn: () => void) {
  listeners.add(fn);
  return () => { listeners.delete(fn); };
}
