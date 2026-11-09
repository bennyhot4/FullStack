# HourOffice

מערכת להזמנת חדרים/משרדים לפי שעה. בנייה מחדש של האפליקציה מ-Base44.

- **תוכנית מלאה:** [docs/PLAN.md](docs/PLAN.md)
- **Backend:** ASP.NET Core 8 (C#) + MongoDB – תיקיית [`backend/`](backend)
- **Frontend:** HTML + JavaScript בלי framework ובלי build – תיקיית [`frontend/`](frontend)
- שני החלקים **נפרדים לגמרי** ונפרסים בנפרד (כל אחד אפשר להחליף לספק אחר).

```
backend/
  HourOffice.Api/        ה-API (Program.cs, Endpoints/, Services/, Data/, Seed/ = תקנון התחלתי)
  HourOffice.Tests/      37 בדיקות אינטגרציה מול MongoDB אמיתי
  Dockerfile
frontend/
  index.html, config.js  ← כתובת ה-API נקבעת כאן בלבד
  css/, js/app.js (ניתוב + הרשאות), js/pages/ (מסך לכל עמוד), js/components/
  dev-server.mjs         שרת מקומי לפיתוח
e2e/smoke.mjs            בדיקת דפדפן מקצה לקצה בגודל טלפון
```

## הרצה מקומית

דרישות: .NET 8 SDK, Node 22, ו-MongoDB מקומי (למשל `docker run -d -p 27017:27017 mongo:7`).

```bash
# 1. API על http://localhost:5080 (סביבת Development: כניסת מפתחים עם אימייל בלבד, admin@example.com הוא מנהל)
cd backend/HourOffice.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:5080

# 2. אתר על http://localhost:5500
cd frontend
node dev-server.mjs 5500
```

נכנסים ל-http://localhost:5500, מתחברים עם `admin@example.com` בטופס "כניסת מפתחים", ממלאים פרופיל וחותמים על התקנון.

## בדיקות

```bash
cd backend && dotnet test                     # 37 בדיקות; מפעילות mongod זמני בעצמן, לא צריך MongoDB מותקן
NODE_PATH=$(npm root -g) node e2e/smoke.mjs   # דורש API + אתר רצים מול מסד נתונים ריק, ו-Playwright מותקן
```

## הגדרות ה-API (משתני סביבה)

| משתנה | חובה | תיאור |
|---|---|---|
| `Mongo__ConnectionString` | ✔ | מחרוזת החיבור ל-Atlas. **סוד** – רק ב-Secret Manager של ספק הפריסה |
| `Mongo__Database` | | ברירת מחדל `houroffice` |
| `Auth__JwtSigningKey` | ✔ | מחרוזת אקראית של 32+ תווים. **סוד** |
| `Auth__GoogleClientId` | ✔ | OAuth Client ID מ-Google Cloud Console |
| `Auth__AdminEmails__0` | ✔ | האימייל שיהיה מנהל בכניסה הראשונה (אפשר `__1`, `__2`...) |
| `Auth__AllowOpenSignup` | | `true` (ברירת מחדל): כל חשבון Google יכול להירשם. `false`: רק מי שמנהל הוסיף |
| `Cors__AllowedOrigins__0` | ✔ | כתובת האתר, למשל `https://houroffice.pages.dev` |
| `Storage__Provider` | | `Local` (פיתוח) או `S3` (ייצור – Cloudflare R2) + `Storage__S3__*` |
| `Booking__*`, `Currency__*` | | שעות פתיחה, ימים קדימה, אזור זמן, סמלי מטבע לפי שפה |

כניסת המפתחים (`Auth__DevLoginEnabled`) **לא פועלת בסביבת Production** גם אם מופעלת בטעות.

## Google Sign-In

1. https://console.cloud.google.com → APIs & Services → Credentials → Create OAuth client ID → Web application.
2. **Authorized JavaScript origins:** כתובת האתר (ו-`http://localhost:5500` לפיתוח).
3. את ה-Client ID שמים ב-`Auth__GoogleClientId` של ה-API. האתר מקבל אותו אוטומטית מ-`/api/settings/public`.

## פריסה (שלב הבא)

- **API:** `docker build -t houroffice-api backend` → Google Cloud Run (או כל מארח קונטיינרים). פורט 8080, בדיקת בריאות `/healthz`.
- **אתר:** תיקיית `frontend/` כמו שהיא ל-Cloudflare Pages / Netlify (הקובץ `_redirects` מטפל בניתוב). לעדכן את `apiBaseUrl` ב-`config.js`.
- **DB:** MongoDB Atlas – האינדקסים והתקנון ההתחלתי נוצרים אוטומטית בעליית ה-API.
