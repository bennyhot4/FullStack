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

## פריסה

האתר באוויר בעלות של כ-$0 לחודש (כל השירותים בשכבה החינמית). ההחלטות וההיסטוריה: [`.plan/006-2026-11-17-low-cost-deployment.md`](.plan/006-2026-11-17-low-cost-deployment.md).
**אין כאן אף ערך סודי** – רק שמות. מחרוזת Atlas, מפתח ה-JWT ומפתחות R2 נמצאים רק ב-Secret Manager.

### מה רץ איפה

| חלק | שירות | שם / כתובת |
|---|---|---|
| API | Google Cloud Run, `europe-west1`, scale to zero, עד 2 מופעים | פרויקט `houroffice-prod`, שירות `houroffice-api` → `https://houroffice-api-770124643502.europe-west1.run.app` |
| אתר | Cloudflare Pages (Direct Upload) | פרויקט `houroffice` → `https://houroffice.pages.dev` |
| תמונות | Cloudflare R2 | באקט `houroffice-uploads`, כתובת ציבורית `https://pub-5c470ba18f4f4b59b37e8f7f9c9779aa.r2.dev` |
| מסד נתונים | MongoDB Atlas M0, GCP בלגיה | קלאסטר `BookingDB`, מסד `houroffice`, משתמש `houroffice-prod` (`readWrite@houroffice` בלבד) |
| סודות | Google Secret Manager | `mongo-connection-string`, `jwt-signing-key`, `r2-access-key-id`, `r2-secret-key` |
| כניסה | Google Auth Platform | לקוח Web "HourOffice web", סטטוס "In production" |
| תקציב | Google Billing | ₪20 לחודש על `houroffice-prod`, התראות 50/90/100% |

ה-API רץ כ-service account ייעודי `houroffice-api@houroffice-prod.iam.gserviceaccount.com` שיש לו רק `secretAccessor` על ארבעת הסודות.

### עדכון ה-API

דרישות: Google Cloud CLI מחובר (`gcloud auth login`), עם ברירות מחדל `core/project=houroffice-prod` ו-`run/region=europe-west1`. לא צריך Docker – הבנייה רצה ב-Cloud Build, ו-`backend/.gcloudignore` מונע העלאה של `bin/`, `obj/` והבדיקות.

1. קובץ משתני הסביבה הרגילים (לא סודות) – שמרו אותו **מחוץ לריפו**, למשל `env.prod.yaml`:
   ```yaml
   Auth__GoogleClientId: "770124643502-lf7d4908khfgkevsrv9rem99sdv6e2bp.apps.googleusercontent.com"
   Auth__AdminEmails__0: "<האימייל של המנהל>"
   Auth__AllowOpenSignup: "true"
   Cors__AllowedOrigins__0: "https://houroffice.pages.dev"
   Storage__Provider: "S3"
   Storage__S3__ServiceUrl: "https://<cloudflare-account-id>.r2.cloudflarestorage.com"
   Storage__S3__Bucket: "houroffice-uploads"
   Storage__S3__PublicBaseUrl: "https://pub-5c470ba18f4f4b59b37e8f7f9c9779aa.r2.dev"
   ```
2. פריסה משורש הריפו:
   ```bash
   gcloud run deploy houroffice-api --source backend --region europe-west1 --allow-unauthenticated --service-account houroffice-api@houroffice-prod.iam.gserviceaccount.com --max-instances 2 --min-instances 0 --cpu-boost --memory 512Mi --cpu 1 --env-vars-file env.prod.yaml --set-secrets Mongo__ConnectionString=mongo-connection-string:latest,Auth__JwtSigningKey=jwt-signing-key:latest,Storage__S3__AccessKey=r2-access-key-id:latest,Storage__S3__SecretKey=r2-secret-key:latest
   ```
3. בדיקה:
   ```bash
   curl --fail https://houroffice-api-770124643502.europe-west1.run.app/api/settings/public
   ```
   ⚠️ **לא `/healthz`:** ב-`*.run.app` גוגל שומרת לעצמה נתיבים שנגמרים ב-`z` ומחזירה 404 לפני שהבקשה מגיעה ל-API.

ב-Artifact Registry (`cloud-run-source-deploy`) יש מדיניות ניקוי ששומרת את 2 האימג'ים האחרונים, כך שלא חורגים מ-0.5GB החינמיים.

### עדכון האתר

ה-`config.js` בריפו מצביע על localhost ונשאר כך. בכל פריסה מעתיקים את `frontend/` לתיקייה זמנית (בלי `dev-server.mjs`), כותבים בה `config.js` של ייצור ומעלים:

```js
// config.js בעותק לפריסה בלבד
window.HOUROFFICE_CONFIG = { apiBaseUrl: "https://houroffice-api-770124643502.europe-west1.run.app" };
```
```bash
npx wrangler@4 pages deploy <התיקייה-הזמנית> --project-name=houroffice --branch=main
```

בדיקה: `https://houroffice.pages.dev` מציג את כפתור Google, וגם כתובת עמוקה כמו `/MyBookings` נטענת (בזכות `_redirects`). בפריסה הראשונה של כתובת חדשה Cloudflare יכול להחזיר 522 לכמה שניות.

### החלפת סוד

```bash
gcloud secrets versions add <שם-הסוד> --data-file=-   # מדביקים את הערך ב-stdin, לא בפקודה ולא בקובץ
```
אחר כך מריצים שוב את פקודת הפריסה של ה-API (הסודות נקראים ב-`:latest` רק בעליית מופע חדש), ומבטלים את הגרסה הקודמת: `gcloud secrets versions disable <מספר> --secret=<שם-הסוד>`.

### Windows

- `gcloud` ו-`npx` הם קובצי `.cmd`: ב-PowerShell משתמשים ב-`npx.cmd` (מדיניות ההרצה חוסמת את `npx.ps1`).
- `.cmd` נשבר על `|` ועל ארגומנטים עם רווחים: לכן משתני הסביבה ב-`--env-vars-file`, ול-wrangler מעבירים `--name=value` בלי רווחים (שימו את התיקייה הזמנית בנתיב בלי רווחים).

### החזרה לאחור

- **API:** `gcloud run services update-traffic houroffice-api --to-revisions=<revision-קודמת>=100` (מיידי). רשימת revisions: `gcloud run revisions list --service houroffice-api`.
- **אתר:** Cloudflare → Workers & Pages → `houroffice` → Deployments → "Rollback to this deployment".

### הזמנת משתמשים

בפרודקשן ההרשמה פתוחה (`Auth__AllowOpenSignup=true`, מאז 2026-11-22): כל חשבון Google נכנס כמשתמש רגיל, ומנהלים נוספים מקודמים במסך **משתמשים**. אפשר גם להוסיף משתמש מראש (אימייל + תפקיד) כדי שייכנס ישר עם התפקיד הנכון.

`Auth__AllowOpenSignup=false`: רק מי שהמנהל הוסיף במסך **משתמשים** (אימייל + תפקיד) יכול להיכנס. כשהוא נכנס עם Google הוא מקבל את התפקיד שנקבע לו.
