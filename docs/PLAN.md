# HourOffice – תוכנית פיתוח מלאה מאפס

> מקור: האפליקציה הקיימת ב-Base44 – `https://hour-office-0e2d785c.base44.app/`
> ("Book office spaces by the hour for your flexible work needs").
> התוכנית מבוססת על ניתוח הקוד של האפליקציה הקיימת: המסכים, הישויות, השדות, הטקסטים והלוגיקה.
> **עודכן לפי הגרסה שפורסמה ב-09/11/2026** (נוספו: תקנון עם חתימה דיגיטלית, מיתוג, צפייה בהזמנות של משתמש, תאריך ביטול).
> לפי התקנון, העסק משכיר **חדרי טיפול** לאנשי מקצוע בתחום הטיפול והבריאות.
>
> **Stack יעד:** Frontend ב-HTML + JavaScript (Vanilla, בלי framework) · Backend ב-C# (ASP.NET Core 8 Minimal API / Controllers) · MongoDB (Atlas) · Mobile-first, דו-לשוני (עברית RTL / אנגלית LTR).

---

## 0. החלטות (גוברות על כל מה שכתוב בהמשך)

| # | נושא | החלטה |
|---|---|---|
| 1 | מטבע | סמל לפי שפה כמו היום (`₪` בעברית, `$` באנגלית, אותו מספר). **תשתית למטבעות:** כל משרד והזמנה שומרים `currency` (קוד ISO, ברירת מחדל `ILS`), והגדרות התצוגה (`currency.display`) מגיעות מהשרת – כך שאפשר להוסיף ניהול מטבעות ושערים בעתיד בלי לשנות את מבנה הנתונים |
| 2 | פריסה | **Frontend ו-Backend נפרדים לגמרי** – שני פרויקטים, שתי פריסות. ה-Frontend הוא אתר סטטי (כל ספק: Cloudflare Pages / Netlify / GitHub Pages), ה-Backend הוא קונטיינר Docker (כל ספק: Cloud Run / Render / VPS). החיבור ביניהם רק דרך `config.js` (כתובת ה-API) ו-CORS בשרת |
| 3 | התחברות | **Google בלבד** (Google Identity Services). אין סיסמאות |
| 4 | הודעות | **אין שליחת אימיילים/הודעות בכלל.** "הזמנת משתמש" = יצירת רשומה מראש עם אימייל ותפקיד; כשהאדם מתחבר עם Google באותו אימייל – הוא מקבל את התפקיד. אין OTP, אין איפוס סיסמה |
| 5 | סדר עבודה | קודם בונים ומוודאים שהקוד עובד (בדיקות אוטומטיות + MongoDB זמני מקומי), פריסה אחר כך |
| 6 | נתונים | אין העברת נתונים מ-Base44 בשלב זה |
| 7 | DB | חשבון MongoDB Atlas קיים של הבעלים. מחרוזת החיבור נכנסת רק כמשתנה סביבה/סוד בפריסה (`Mongo__ConnectionString`), לעולם לא לקוד או לצ'אט |

**נגזרות:** שלבים 2 ("אימות") ו-12 ("העברת נתונים") מצטמצמים; אין צורך ב-Brevo/Resend; ה-refresh token נשמר בצד הלקוח ונשלח בגוף הבקשה (cookie בין דומיינים שונים נחסם בספארי); אין collection `authTokens` מסוג otp/reset/invite.

## 1. מה קיים היום באפליקציה (מיפוי מלא)

### 1.1 מסכים

| # | מסך (Route) | מי רואה | מה יש בו |
|---|---|---|---|
| 1 | `/login` | כולם | לוגו, "Welcome to HourOffice", **Continue with Google**, אימייל+סיסמה, **Forgot password?**, **Sign up** |
| 2 | `/Welcome` | מחובר | קרוסלת Hero עם 4 שקפים (תמונה+כותרת+תת-כותרת, מעבר אוטומטי + נקודות), 3 אריחים (לפי שעה / מיקום מרכזי / כל גודל), כפתור "חקרו משרדים", בורר שפה. **בלי** סרגל ניווט תחתון |
| 3 | `/Register` (השלמת פרופיל) | מחובר בלי טלפון | שם פרטי, שם משפחה (ממולאים מ-full_name), אימייל (נעול), טלפון. ולידציה: לפחות 9 ספרות, נרמול ל-`+972XXXXXXXXX`. **כל משתמש בלי טלפון מנותב לכאן אוטומטית** (חוץ מ-`/` ו-`/Welcome`) |
| 3א | `/TermsOfUse` (תקנון) | מחובר עם טלפון שלא חתם | כותרת "תנאי שימוש" + "קראו את התקנון ואשרו כדי להמשיך", כפתור חזרה, **תקנון מלא בעברית ובאנגלית בפורמט Markdown** (15 פרקים, כ-13,700 תווים: כללי והגדרות, זכאות השוכר, הזמנות ושעות, תעריפים ותשלום, ביטולים, … דין וסמכות שיפוט, חתימה דיגיטלית), כפתור **"אני מאשר את התנאים"** → שומר `terms_accepted=true` + `terms_accepted_date` ומעביר לדף הבית. **כל משתמש עם טלפון שלא חתם מנותב לכאן אוטומטית** (חוץ מ-`/` ו-`/Welcome`). בתקנון יש מצייני מקום שעוד לא מולאו (`[שם העסק]`, `[X]`, `[14]`, `[עיר/מחוז]`) |
| 4 | `/Home` (Explore, דף הבית) | מחובר | קרוסלת באנרים (מ-SiteSettings `home_banner_N`), כותרת ותיאור מהמיתוג (`brand_title` / `brand_description`, ברירת מחדל "Office Booking" / "Find and book your perfect workspace"), חיפוש לפי שם/מיקום, סינון קיבולת (הכל / קטן 1-4 / בינוני 5-8 / גדול 9+), כרטיסי משרד: תמונה, תגית מחיר (`₪` בעברית / `$` באנגלית) + "/hr", שם, קיבולת, מיקום, עד 3 תגיות שירותים, "הזמן עכשיו". מציג רק `is_available=true` |
| 5 | `/BookOffice?id=` | מחובר | פרטי משרד, תעריף שעתי, אייקוני שירותים (WiFi, Projector, Coffee, Parking, Phone), **לוח שנה** (מהיום ועד שבועיים קדימה – "ניתן להזמין עד שבועיים מראש"), **משבצות שעה 08:00–22:00**, משבצות תפוסות מסומנות "תפוס", בחירת טווח, הערות, סיכום (שעות + מחיר כולל), "אישור הזמנה" → מסך "ההזמנה אושרה!" עם "צפייה בהזמנות שלי" / "הזמנה נוספת" |
| 6 | `/MyBookings` | מחובר | טאבים "קרובות" (מאושרות מהיום והלאה, ממוינות לפי תאריך ושעה) / "עבר" עם מונה, כרטיס הזמנה עם תגית סטטוס (מאושר=ירוק, בוטל=אדום, הושלם=אפור), ביטול עם דיאלוג אישור ("פעולה זו לא ניתנת לביטול") – **שומר גם `cancelled_at`**, מצב ריק + "הזמינו משרד" |
| 7 | `/AdminOffices` | Admin | רשימת משרדים, הוספה/עריכה בדיאלוג (שם, תיאור, תמונה – העלאה או URL, קיבולת, מחיר לשעה, מיקום, שירותים מופרדים בפסיק, זמין להזמנה), מחיקה עם אישור, **ניהול באנרים לדף הבית** (הוספה בהעלאה/URL, מחיקה) |
| 8 | `/AdminUsers` | Admin | מונה משתמשים, חלוקה ל"מנהלים" / "משתמשים", לכל אחד: שם, אימייל, תאריך הצטרפות, **סטטוס תקנון** ("נחתם · תאריך" / "לא נחתם"); **הזמנת משתמש** (אימייל + תפקיד), הגדר כמנהל / כמשתמש (לא על עצמך), עריכה (שם, אימייל, טלפון, תפקיד), מחיקה (מוחקת גם את ההזמנות שלו), **כפתור "Bookings" → חלון "ההזמנות של X"**: לכל הזמנה משרד, תאריך, שעות, משך, מחיר, סטטוס (עתידית / עבר / בוטל) ותאריך ביטול |
| 9 | `/AdminAnalytics` | Admin | בורר "השבוע" (שבוע מתחיל ביום שני) / "החודש" + טווח התאריכים, סה״כ הזמנות, סה״כ שעות, הכנסות, **שימוש לפי משתמש** (דירוג, שם, אימייל, הזמנות, שעות, הוצא) – רק הזמנות מאושרות, בלי מנהלים, ממוין לפי שעות |
| 10 | `/AdminBookings` | Admin | עד 500 הזמנות, מיון: קרובות קודם (עולה), אח"כ עבר (יורד). כרטיס: משרד, סטטוס, תאריך מלא, שעות + משך, שם המזמין. עריכה (משרד, מזמין, תאריך, שעות) עם בדיקת התנגשות, ביטול. **הזמנה חוזרת (Repeated Booking):** משרד, מזמין, שעת התחלה/סיום, ימים בשבוע (א'-ש'), מתאריך–עד תאריך (ברירת מחדל חודש), הערות → יוצר הזמנות בכמות, מדלג על משבצות תפוסות ומציג "X נוצרו, Y דולגו" |
| 11 | `/SiteSettingsPage` | Admin | תמונת באנר ראשית (`home_banner_image`) – העלאה או URL; **מיתוג (Branding):** כותרת הדף, תיאור הדף, לוגו (העלאה או URL + תצוגה מקדימה), צבע כפתורים (ברירת מחדל `#3B6A8F`), צבע רקע (`#F3F6F9`), צבע כרטיסים/דפים (`#FFFFFF`), "Save Branding". הצבעים מוחלים על כל האפליקציה כמשתני CSS (`--primary`, `--background`, `--card`) |
| 12 | — | — | (מסך התקנון הועבר לשורה 3א) |
| 13 | 404 | כולם | "Page Not Found" |
| 14 | Access Restricted | משתמש לא רשום | "You are not registered to use this application…" |

### 1.2 Layout משותף
- **כפתורים קבועים בפינה העליונה:** התנתקות + בורר שפה (English / עברית, נשמר ב-`localStorage.appLanguage`, ברירת מחדל `en`, מחליף `dir=rtl/ltr` ו-`lang`).
- **סרגל ניווט תחתון קבוע** (`safe-area-inset-bottom`): Explore, My Bookings, ולמנהל גם Offices, Users, Analytics, Bookings (עוטף לשתי שורות אצל מנהל).
- ספינר טעינה במסך מלא בזמן בדיקת התחברות.

### 1.3 ישויות (Data model קיים)

| ישות | שדות |
|---|---|
| **User** | `id, email, full_name, first_name, last_name, phone_number, role (admin/user), terms_accepted, terms_accepted_date, created_date` |
| **Office** | `id, name, description, image_url, capacity, hourly_rate, amenities[], location, is_available, created_date` |
| **Booking** | `id, office_id, office_name, booker_name, date (yyyy-MM-dd), start_time, end_time (HH:mm), duration_hours, total_price, status (confirmed/cancelled/completed), notes, cancelled_at, created_by_id, created_date` |
| **SiteSettings** | `id, key, value` – מפתחות: `home_banner_image`, `home_banner_1..N`, `brand_title`, `brand_description`, `brand_logo`, `brand_buttons_color`, `brand_background_color`, `brand_pages_color` |

פעולות נוספות: העלאת קבצים (`UploadFile` → `file_url`), הזמנת משתמש (`invite-user`), הרשמה + אימות OTP, איפוס סיסמה, Google OAuth.

### 1.4 בעיות באפליקציה הקיימת שכדאי לתקן בבנייה מחדש
1. **בדיקת התנגשויות נעשית בצד הלקוח בלבד** – שני משתמשים יכולים להזמין אותה שעה במקביל. → בנייה מחדש: נעילה ב-DB (אינדקס ייחודי, ראו §4.3).
2. המחיר (`total_price`) מחושב בצד הלקוח → יחושב בשרת.
3. מגבלת "שבועיים קדימה" ושעות 08–22 נאכפות רק ב-UI → ייאכפו בשרת.
4. סטטוס `completed` לא מתעדכן אוטומטית → Job יומי / חישוב בזמן קריאה.
5. מטבע מוצג לפי שפה (₪/$) אבל המספר זהה → מטבע אחד (₪) לכל המערכת.
6. התקנון קבוע בקוד (hardcoded) ובו מצייני מקום לא ממולאים → בבנייה מחדש: נשמר ב-DB, ניתן לעריכה ע"י מנהל, **עם מספר גרסה**, וכל שינוי מחייב חתימה מחדש.
7. סטטיסטיקת משתמש באנליטיקה מתאימה הזמנות לפי `created_by` (אימייל) – שינוי אימייל מנתק היסטוריה → התאמה לפי `userId`.

---

## 2. ארכיטקטורה

```
 [ טלפון / דפדפן ]
        │  HTTPS
        ▼
 ┌──────────────────────────────┐
 │ Google Cloud Run (קונטיינר) │   ← שירות אחד
 │ ASP.NET Core 8               │
 │  • /api/*  → REST API        │
 │  • /*      → קבצי Frontend   │   (wwwroot: HTML/CSS/JS סטטיים)
 └──────┬───────────────┬───────┘
        │               │
        ▼               ▼
 MongoDB Atlas      Cloudflare R2
 (Free, 512MB)      (תמונות, 10GB חינם)
```

**החלטה:** ה-Frontend מוגש מתוך אותו שרת C# (`wwwroot`) – שירות אחד, דומיין אחד, בלי CORS, פריסה אחת. (חלופה: Frontend ב-Cloudflare Pages והשרת רק API – גם חינם, אבל מוסיף CORS ושתי פריסות.)

---

## 3. מבנה הריפו

```
FullStack/
├── backend/
│   ├── HourOffice.Api/
│   │   ├── Program.cs                 # DI, Auth, Routing, Static files
│   │   ├── appsettings.json
│   │   ├── Models/                    # User, Office, Booking, SiteSetting
│   │   ├── Dtos/                      # Request/Response records
│   │   ├── Endpoints/                 # AuthEndpoints, OfficeEndpoints, BookingEndpoints,
│   │   │                              # UserEndpoints, AnalyticsEndpoints, SettingsEndpoints, UploadEndpoints
│   │   ├── Services/                  # BookingService, AuthService, EmailService, StorageService, TokenService
│   │   ├── Data/MongoContext.cs       # חיבור + יצירת אינדקסים בעלייה
│   │   ├── Validation/                # FluentValidation
│   │   └── wwwroot/                   # ← ה-Frontend (נבנה ע"י העתקה מ-/frontend)
│   ├── HourOffice.Tests/              # xUnit + Testcontainers(Mongo)
│   └── Dockerfile
├── frontend/
│   ├── index.html                     # Shell יחיד (SPA)
│   ├── manifest.json, sw.js, icons/   # PWA
│   ├── css/
│   │   ├── tokens.css                 # צבעים, רווחים, פונטים
│   │   ├── base.css                   # reset, טיפוגרפיה, RTL
│   │   └── components.css             # כפתורים, כרטיסים, דיאלוג, טאבים, bottom-nav
│   ├── js/
│   │   ├── app.js                     # אתחול, guard-ים
│   │   ├── router.js                  # hash/history router
│   │   ├── api.js                     # fetch wrapper + JWT + refresh
│   │   ├── auth.js                    # login/signup/google/logout
│   │   ├── i18n.js + i18n/en.json, he.json
│   │   ├── store.js                   # currentUser, cache קטן
│   │   ├── components/                # carousel.js, calendar.js, timeSlots.js, dialog.js,
│   │   │                              # officeCard.js, bookingCard.js, bottomNav.js, toast.js, langSwitcher.js
│   │   └── pages/                     # login.js, welcome.js, register.js, home.js, bookOffice.js,
│   │                                  # myBookings.js, adminOffices.js, adminUsers.js,
│   │                                  # adminAnalytics.js, adminBookings.js, siteSettings.js, terms.js, notFound.js
│   └── assets/
├── docs/PLAN.md
└── .github/workflows/deploy.yml
```

---

## 4. Backend (C# / ASP.NET Core 8)

### 4.1 חבילות NuGet
- `MongoDB.Driver` – חיבור ל-MongoDB
- `Microsoft.AspNetCore.Authentication.JwtBearer` – JWT
- `BCrypt.Net-Next` – גיבוב סיסמאות
- `Google.Apis.Auth` – אימות Google ID Token
- `AWSSDK.S3` – העלאה ל-Cloudflare R2 (תואם S3)
- `FluentValidation` – ולידציה
- `Serilog.AspNetCore` – לוגים
- בדיקות: `xUnit`, `FluentAssertions`, `Testcontainers.MongoDb`

### 4.2 מודל MongoDB (Collections)

```jsonc
// users
{ "_id": ObjectId, "email": "a@b.com", "passwordHash": "...|null", "googleSub": "...|null",
  "firstName": "", "lastName": "", "fullName": "", "phoneNumber": "+9725...",
  "role": "user|admin", "emailVerified": true, "status": "active|invited",
  "termsAcceptedVersion": 3, "termsAcceptedAt": ISODate|null,   // חתימה על התקנון
  "createdAt": ISODate, "updatedAt": ISODate }

// offices
{ "_id": ObjectId, "name": "", "description": "", "imageUrl": "", "capacity": 4,
  "hourlyRate": 50, "amenities": ["WiFi","Coffee"], "location": "", "isAvailable": true,
  "createdAt": ISODate }

// bookings
{ "_id": ObjectId, "officeId": ObjectId, "officeName": "", "userId": ObjectId, "bookerName": "",
  "date": "2026-11-09", "startTime": "09:00", "endTime": "12:00",
  "slots": ["09:00","10:00","11:00"],          // ← לנעילת התנגשויות
  "durationHours": 3, "totalPrice": 150, "status": "confirmed|cancelled|completed",
  "notes": "", "seriesId": ObjectId|null,      // הזמנה חוזרת
  "cancelledAt": ISODate|null, "cancelledById": ObjectId|null,
  "createdById": ObjectId, "createdAt": ISODate }

// siteSettings  (באנרים + מיתוג)
{ "_id": ObjectId, "key": "home_banner_1", "value": "https://..." }
{ "_id": ObjectId, "key": "brand_buttons_color", "value": "#3B6A8F" }

// terms  (תקנון עם גרסאות)
{ "_id": ObjectId, "version": 3, "contentHe": "## 1. כללי...", "contentEn": "## 1. General...",
  "publishedAt": ISODate, "publishedById": ObjectId }

// authTokens  (refresh / reset / invite / OTP) – עם TTL
{ "_id": ObjectId, "userId": ObjectId, "type": "refresh|reset|invite|otp",
  "tokenHash": "...", "expiresAt": ISODate }
```

### 4.3 אינדקסים (נוצרים אוטומטית בעליית השרת)

| Collection | אינדקס | מטרה |
|---|---|---|
| users | `{ email: 1 }` unique | התחברות |
| offices | `{ isAvailable: 1, createdAt: -1 }` | דף הבית |
| bookings | `{ officeId: 1, date: 1, slots: 1 }` **unique, partial: `{status:"confirmed"}`** | **מונע הזמנה כפולה של אותה שעה ברמת ה-DB**. אינדקס multikey ייחודי לא מאפשר לשני מסמכים מאושרים לשתף משבצת; הזמנה שבוטלה משחררת את המשבצות |
| bookings | `{ userId: 1, date: -1 }` | ההזמנות שלי |
| bookings | `{ date: 1, status: 1 }` | אנליטיקה + ניהול |
| siteSettings | `{ key: 1 }` unique | |
| terms | `{ version: -1 }` unique | הגרסה האחרונה |
| authTokens | `{ expiresAt: 1 }` TTL (expireAfterSeconds: 0) | ניקוי אוטומטי |

> אם ההוספה נכשלת עם `DuplicateKey` (E11000) → השרת מחזיר `409 Conflict` "המשבצת נתפסה".

### 4.4 API Endpoints

**Auth**
| Method | Path | תיאור |
|---|---|---|
| POST | `/api/auth/register` | הרשמה (אימייל+סיסמה) → שולח OTP לאימייל |
| POST | `/api/auth/verify-otp` | אימות קוד → מחזיר tokens |
| POST | `/api/auth/login` | התחברות → `{accessToken, refreshToken(HttpOnly cookie), user}` |
| POST | `/api/auth/google` | `{ idToken }` מ-Google Identity Services → tokens |
| POST | `/api/auth/refresh` | חידוש access token מה-cookie |
| POST | `/api/auth/logout` | ביטול refresh token |
| POST | `/api/auth/forgot-password` | שליחת לינק איפוס |
| POST | `/api/auth/reset-password` | `{token, newPassword}` |
| POST | `/api/auth/accept-invite` | `{token, password}` – משתמש שהוזמן |
| GET | `/api/me` | המשתמש הנוכחי |
| PUT | `/api/me` | `{firstName, lastName, phone}` – נרמול ל-`+972` בשרת |
| GET | `/api/terms/current` | התקנון העדכני (he/en + גרסה) |
| POST | `/api/me/accept-terms` | `{version}` – שומר גרסה + תאריך חתימה (השרת קובע את הזמן) |

> `GET /api/me` מחזיר גם `needsProfile` ו-`needsTerms`, וה-Frontend מנתב לפיהם. בנוסף, middleware בשרת חוסם `POST /api/bookings` למשתמש שלא חתם על הגרסה העדכנית (`403 terms_required`).

**Offices**
| GET | `/api/offices?search=&size=small|medium|large` | רק זמינים (משתמש) |
| GET | `/api/offices/{id}` | פרטי משרד |
| GET | `/api/offices/{id}/availability?date=yyyy-MM-dd` | רשימת המשבצות התפוסות לאותו יום |
| GET | `/api/admin/offices` | כולם (Admin) |
| POST/PUT/DELETE | `/api/admin/offices/{id?}` | CRUD (Admin) |

**Bookings**
| POST | `/api/bookings` | `{officeId, date, startTime, endTime, notes}` – השרת מאמת: תאריך ≤ היום+14, שעות בטווח 08–22, endTime>startTime, משרד זמין; מחשב slots, duration, price |
| GET | `/api/bookings/mine?tab=upcoming|past` | ההזמנות שלי |
| POST | `/api/bookings/{id}/cancel` | ביטול (בעלים או Admin) – שומר `cancelledAt` + `cancelledById` |
| GET | `/api/admin/bookings?limit=500` | כל ההזמנות, ממוינות כמו היום |
| PUT | `/api/admin/bookings/{id}` | עריכה + בדיקת התנגשות |
| POST | `/api/admin/bookings/recurring` | `{officeId, userId/bookerName, startTime, endTime, days:[0..6], fromDate, toDate, notes}` → `{created, skipped}` (insertMany עם `ordered:false`, כפולים נספרים כ-skipped) |

**Users (Admin)**
| GET | `/api/admin/users` | רשימה |
| POST | `/api/admin/users/invite` | `{email, role}` → אימייל הזמנה |
| PUT | `/api/admin/users/{id}` | עריכת שם/אימייל/טלפון/תפקיד |
| DELETE | `/api/admin/users/{id}` | מחיקה + מחיקת כל ההזמנות שלו (טרנזקציה) |
| GET | `/api/admin/users/{id}/bookings` | ההזמנות של משתמש (חלון "ההזמנות של X") |

**Analytics (Admin)**
| GET | `/api/admin/analytics?period=week|month` | Aggregation pipeline: `$match` (status=confirmed, טווח תאריכים, שבוע מיום שני) → `$group` כללי (count, sum hours, sum price) + `$group` לפי userId (בלי מנהלים, ממוין לפי שעות) |

**Settings + Upload**
| GET | `/api/settings/banners` | ציבורי למשתמשים מחוברים |
| GET | `/api/settings/public` | מיתוג (כותרת, תיאור, לוגו, 3 צבעים) + באנרים – בקריאה אחת, עם Cache |
| GET/PUT | `/api/admin/settings/{key}` | `home_banner_image` |
| PUT | `/api/admin/settings/branding` | `{title, description, logo, buttonsColor, backgroundColor, pagesColor}` – ולידציה ל-`#RRGGBB` |
| PUT | `/api/admin/terms` | פרסום גרסת תקנון חדשה (גרסה +1) |
| POST/DELETE | `/api/admin/settings/banners` | הוספה/מחיקה של `home_banner_N` |
| POST | `/api/admin/upload` | multipart, עד 5MB, jpg/png/webp → R2 → `{fileUrl}` |

### 4.5 אבטחה
- JWT access token קצר (15 דק') + refresh token ב-cookie `HttpOnly; Secure; SameSite=Strict` (שמור מגובב ב-DB).
- Policy `AdminOnly` על כל `/api/admin/*`.
- Rate limiting (`AddRateLimiter`) על login/register/forgot.
- סודות (Mongo URI, JWT key, R2 keys, Google Client ID, Email API key) ב-**Secret Manager** של Google Cloud – לא בקוד.
- כותרות: HSTS, CSP, X-Content-Type-Options.
- המשתמש הראשון שנרשם עם האימייל שמוגדר ב-`ADMIN_EMAIL` מקבל `role=admin` (Seed).

### 4.6 שירותים חיצוניים
- **אימייל** (OTP, איפוס סיסמה, הזמנת משתמש): Brevo (חינם, 300 מיילים ביום) או Resend (חינם, 3,000 בחודש).
- **Google Login**: Google Cloud Console → OAuth Client ID (חינם).
- **תמונות**: Cloudflare R2 עם public bucket / דומיין.

---

## 5. Frontend (HTML + JavaScript)

### 5.1 עקרונות
- **SPA קל**: `index.html` אחד, ראוטר מבוסס `history.pushState` (תומך ב-`/BookOffice?id=` כמו היום). השרת מחזיר `index.html` לכל נתיב שאינו `/api` (fallback).
- **ES Modules מקוריים** (`<script type="module">`) – בלי build step. אופציונלי: `esbuild` לאיחוד וכיווץ בייצור.
- רכיבים = פונקציות שמחזירות `HTMLElement` (או Web Components למורכבים: `<ho-calendar>`, `<ho-time-slots>`, `<ho-carousel>`).
- **מיתוג דינמי:** בעלייה נטען `/api/settings/public` ומוגדרים `--primary`, `--background`, `--card` על `:root` – כל ה-CSS משתמש רק במשתנים, כך שהחלפת צבע במסך ההגדרות משנה את כל האפליקציה בלי build.
- **תקנון:** ה-Markdown מוצג עם `marked` (CDN, ~40KB) + `DOMPurify` לסינון HTML.
- כל הטקסטים דרך `t('key')` – אותם מפתחות כמו היום (`premiumOffices`, `bookNow`, `maxTwoWeeks` …) בקבצי `en.json` / `he.json`.
- `api.js`: מוסיף `Authorization`, מנסה refresh אוטומטית ב-401, מציג toast בשגיאה.
- Guard-ים בראוטר (בסדר הזה): לא מחובר → `/login`; אין טלפון → `/Register`; לא חתם על התקנון העדכני → `/TermsOfUse`; לא Admin → "נדרשת הרשאת מנהל".

### 5.2 Mobile-first (רוב השימוש מנייד)
1. `<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">`.
2. CSS נכתב קודם לרוחב 360px; Breakpoints עולים: `@media (min-width: 640px)` טאבלט, `(min-width: 1024px)` דסקטופ (גריד 2–3 עמודות לכרטיסים).
3. **סרגל ניווט תחתון** עם `padding-bottom: env(safe-area-inset-bottom)`, ו-`padding-bottom` לגוף הדף כדי שתוכן לא יוסתר.
4. **אזורי מגע ≥ 44×44px** לכל כפתור, משבצת שעה, יום בלוח שנה.
5. **RTL**: שימוש ב-CSS Logical Properties בלבד (`margin-inline-start`, `padding-inline`, `inset-inline-end`) – אותו CSS עובד בעברית ובאנגלית; אייקוני חצים מתהפכים ב-`[dir=rtl] .icon-arrow { transform: scaleX(-1) }`.
6. טיפוגרפיה נוזלית: `font-size: clamp(14px, 3.8vw, 16px)`; פונט `Heebo` / `Assistant` (תומכים עברית + לטינית).
7. **דיאלוגים כ-Bottom Sheet** בנייד (עולים מלמטה, `max-height: 90dvh`, גלילה פנימית), ומודאל רגיל בדסקטופ. שימוש ב-`<dialog>` מקורי.
8. שדות: `inputmode="tel"` לטלפון, `type="email"`, `autocomplete` נכון, `font-size ≥ 16px` כדי שאייפון לא יעשה זום.
9. קרוסלות: `scroll-snap-type: x mandatory` + swipe טבעי, בלי ספריות.
10. משבצות שעה: גריד של 3 עמודות בנייד / 5 בדסקטופ; בחירת טווח בשתי הקשות (התחלה → סוף).
11. תמונות: `loading="lazy"`, `srcset`, `aspect-ratio` למניעת קפיצות.
12. שימוש ב-`100dvh` (לא `100vh`) בגלל סרגל הכתובת בנייד.
13. **PWA**: `manifest.json` + Service Worker (cache של ה-shell) → "הוספה למסך הבית", פתיחה כמו אפליקציה.
14. בדיקות על: iPhone SE (375), iPhone 14 (390), Pixel 7 (412), iPad (768), דסקטופ – עם Playwright device emulation.

### 5.3 רכיבים לבנייה
`BottomNav`, `TopActions` (התנתקות + שפה), `LangSwitcher`, `Carousel`, `OfficeCard`, `SearchBar`, `CapacityFilter`, `Calendar` (חסימת עבר ו->14 יום), `TimeSlots` (פנוי/תפוס/נבחר), `BookingSummary`, `BookingCard` + `StatusBadge`, `Tabs`, `Dialog/BottomSheet`, `ConfirmDialog`, `FormField`, `ImageUpload` (העלאה + URL + תצוגה מקדימה), `StatCard`, `UserRow`, `RecurringBookingForm` (בחירת ימים א'-ש'), `TermsViewer` + `AcceptTermsBar` (כפתור אישור דביק בתחתית המסך בנייד), `TermsBadge` (נחתם/לא נחתם), `UserBookingsDialog`, `ColorPicker` (`<input type="color">` + שדה HEX), `BrandingForm`, `Toast`, `Skeleton` (3 שלדים בטעינה כמו היום), `EmptyState`, `Spinner`.

---

## 6. פריסה – האפשרות הזולה ביותר

### 6.1 המלצה: **0 ₪ לחודש**

| רכיב | שירות | עלות | מגבלות רלוונטיות |
|---|---|---|---|
| Backend + Frontend | **Google Cloud Run** (קונטיינר Docker) | חינם | 2M בקשות, 180K vCPU-שניות, 360K GiB-שניות בחודש. Scale-to-zero |
| DB | **MongoDB Atlas Free (M0)** | חינם | 512MB, ~100 פעולות לשנייה, 10GB תעבורה בשבוע – הרבה מעבר למה שאפליקציה כזו צריכה |
| תמונות | **Cloudflare R2** | חינם | 10GB אחסון, יציאת נתונים (egress) חינם |
| אימייל | Brevo / Resend | חינם | 300 ביום / 3,000 בחודש |
| CI/CD | GitHub Actions | חינם | |
| SSL | מגיע עם Cloud Run | חינם | |
| דומיין (אופציונלי) | Cloudflare Registrar | ~10$ לשנה | אפשר בלי – כתובת `*.run.app` |

**אזור:** Cloud Run ב-`europe-west1` (בלגיה, Tier 1) + Atlas M0 על GCP באותו אזור (`europe-west1`) → השהיה מינימלית בין השרת ל-DB, וקרוב יחסית לישראל.

**למה Cloud Run:** חינם בפועל לנפח של משרד/עסק קטן, תומך .NET בקונטיינר, HTTPS אוטומטי, לא "נרדם" לדקה כמו Render (Cold start של ~1–2 שניות בלבד, ואפשר לקצר עם ReadyToRun / Native AOT).
**שימו לב:** Cloud Run דורש חשבון חיוב (כרטיס אשראי) – להגדיר **Budget Alert על 1$** ו-`max-instances=2` כדי שלא יהיו הפתעות.

### 6.2 חלופות

| חלופה | עלות | מתי לבחור |
|---|---|---|
| Render Free | חינם | בלי כרטיס אשראי; אבל נרדם אחרי 15 דק' ומתעורר תוך 30–60 שניות – חוויה גרועה בנייד |
| Oracle Cloud Always Free (VM ARM) | חינם | שרת קבוע בלי cold start; דורש ניהול שרת (Linux, Nginx, עדכונים) |
| Azure App Service F1 | חינם | אקוסיסטם Microsoft, אבל 60 דק' CPU ביום ובלי Always-On |
| Render Starter / VPS (Hetzner) | 7$ / ~4–5€ לחודש | כשצריך זמינות מלאה בלי cold start |
| Atlas Flex | 8–30$ לחודש | אם ה-DB עובר 512MB |

### 6.3 Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/ backend/
COPY frontend/ backend/HourOffice.Api/wwwroot/
RUN dotnet publish backend/HourOffice.Api -c Release -o /app -p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HourOffice.Api.dll"]
```

### 6.4 CI/CD (GitHub Actions → Cloud Run)
1. בכל push ל-`main`: `dotnet test` → build image → push ל-Artifact Registry → `gcloud run deploy hour-office --region europe-west1 --min-instances 0 --max-instances 2 --memory 512Mi --set-secrets ...`.
2. אימות ל-GCP דרך Workload Identity Federation (בלי מפתחות JSON בריפו).

---

## 7. שלבי עבודה (Roadmap)

| שלב | משך משוער | תוצרים |
|---|---|---|
| **0. הקמה** | 1–2 ימים | ריפו, מבנה תיקיות, Atlas cluster, פרויקט GCP, R2 bucket, Google OAuth Client, Dockerfile, pipeline ריק שמפרסם "Hello" |
| **1. תשתית Backend** | 2–3 ימים | `MongoContext`, אינדקסים, מודלים, טיפול שגיאות גלובלי (ProblemDetails), Serilog, Health check `/healthz` |
| **2. אימות** | 3–4 ימים | register + OTP, login, Google, refresh, logout, forgot/reset, invite/accept, `/api/me`, Policy Admin, Seed admin |
| **2א. תקנון** | 2 ימים | collection `terms` עם גרסאות, Seed מהתקנון הקיים (he/en), `accept-terms`, middleware שחוסם הזמנה בלי חתימה, מסך TermsOfUse + Guard |
| **3. תשתית Frontend** | 3 ימים | `index.html`, tokens/base/components CSS, router, api.js, i18n (en/he + RTL), Layout: TopActions + BottomNav, Toast, Dialog/BottomSheet, Spinner/Skeleton |
| **4. מסכי משתמש** | 4–5 ימים | Login, Welcome (קרוסלה), Register (השלמת פרופיל + Guard), Home (באנרים, חיפוש, סינון, כרטיסים) |
| **5. הזמנה** | 4–5 ימים | API הזמנות + אינדקס ייחודי + availability; BookOffice: לוח שנה (14 יום), משבצות 08–22, טווח, סיכום, אישור, מסך הצלחה; MyBookings + ביטול |
| **6. ניהול משרדים + העלאה** | 3–4 ימים | AdminOffices CRUD, ImageUpload ל-R2, ניהול באנרים, SiteSettingsPage כולל מיתוג (כותרת, תיאור, לוגו, 3 צבעים) |
| **7. ניהול משתמשים** | 2–3 ימים | AdminUsers: רשימה, סטטוס תקנון, הזמנה, שינוי תפקיד, עריכה, מחיקה מדורגת, חלון "ההזמנות של X" |
| **8. ניהול הזמנות** | 3 ימים | AdminBookings: רשימה ומיון, עריכה עם בדיקת התנגשות, ביטול, הזמנה חוזרת (created/skipped) |
| **9. אנליטיקה** | 2 ימים | Aggregation שבוע/חודש, StatCards, שימוש לפי משתמש |
| **10. השלמות** | 2 ימים | 404, Access Restricted, PWA (manifest + SW), Job לסימון `completed` |
| **11. בדיקות ו-QA** | 3–4 ימים | Unit + Integration (Testcontainers), בדיקת מקביליות להזמנה כפולה, Playwright E2E על 4 גדלי מסך בעברית ובאנגלית, Lighthouse נייד ≥ 90 |
| **12. העברת נתונים ועלייה לאוויר** | 1–2 ימים | ייצוא נתונים מ-Base44 (CSV/JSON ממסך הנתונים של Base44) → סקריפט import ל-Mongo (מיפוי `snake_case` → `camelCase`, `office_id` → `ObjectId`), דומיין, Budget alert, ניטור |

**סה״כ:** כ-7–9 שבועות למפתח אחד.

---

## 8. בדיקות קבלה (Definition of Done)
- [ ] כל המסכים מ-§1.1 עובדים, כולל כל הטקסטים בעברית ובאנגלית.
- [ ] משתמש שלא חתם על גרסת התקנון העדכנית לא יכול להזמין (גם לא ב-API ישיר), ומנהל רואה מי חתם ומתי.
- [ ] שינוי צבעים/לוגו במסך ההגדרות משנה מיד את כל האפליקציה.
- [ ] אי אפשר ליצור שתי הזמנות מאושרות שחופפות באותו משרד ובאותה שעה – גם בשתי בקשות במקביל.
- [ ] אי אפשר להזמין לעבר, מעבר ל-14 יום, או מחוץ ל-08:00–22:00 – גם ב-API ישיר.
- [ ] משתמש בלי טלפון לא יכול להגיע לאף מסך חוץ מ-Welcome / Register.
- [ ] משתמש רגיל מקבל 403 על כל `/api/admin/*`.
- [ ] מחיקת משתמש מוחקת גם את ההזמנות שלו.
- [ ] הזמנה חוזרת מדלגת על משבצות תפוסות ומדווחת created / skipped.
- [ ] אין גלילה אופקית ב-360px; כל אזור מגע ≥ 44px; ה-BottomNav לא מסתיר תוכן; RTL תקין.
- [ ] Lighthouse נייד: Performance ≥ 90, Accessibility ≥ 90.
- [ ] עלות חודשית בפועל: 0 ₪ (Budget alert מוגדר).
