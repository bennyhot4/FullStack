# Operations: local setup, configuration and deployment

Moved from the README on 2026-11-18 so the README stays a short, non-technical overview. Content unchanged (Hebrew).

## מבנה הריפו

```
backend/
  HourOffice.Api/        ה-API (Program.cs, Endpoints/, Services/, Data/, Seed/ = תקנון התחלתי)
  HourOffice.Tests/      48 בדיקות אינטגרציה מול MongoDB אמיתי
  Dockerfile
frontend/
  public/config.js       ← כתובת ה-API נקבעת כאן בלבד (קובץ זמן-ריצה, לא נארז ב-build)
  src/app.tsx (ניתוב + הרשאות), src/pages/ (מסך לכל עמוד), src/components/, src/ui/
  css/                   עיצוב (mobile-first, RTL)
  React 19 + TypeScript, נבנה עם Vite ל-dist/ (תוכנית 009)
e2e/smoke.mjs            בדיקת דפדפן מקצה לקצה בגודל טלפון
```

## הרצה מקומית

דרישות: .NET 8 SDK, Node 22, ו-MongoDB מקומי (למשל `docker run -d -p 27017:27017 mongo:7`).

```bash
# 1. API על http://localhost:5080 (סביבת Development: כניסת מפתחים עם אימייל בלבד, admin@example.com הוא מנהל)
cd backend/HourOffice.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:5080

# 2. אתר על http://localhost:5500 (Vite, טעינה מחדש אוטומטית)
cd frontend
npm ci        # פעם אחת
npm run dev
```

נכנסים ל-http://localhost:5500, מתחברים עם `admin@example.com` בטופס "כניסת מפתחים", ממלאים פרופיל וחותמים על התקנון.

## בדיקות

```bash
cd backend && dotnet test                     # 48 בדיקות; מפעילות mongod זמני בעצמן, לא צריך MongoDB מותקן
NODE_PATH=$(npm root -g) node e2e/smoke.mjs   # דורש API + אתר רצים מול מסד נתונים ריק, ו-Playwright מותקן
cd frontend && npm run typecheck && npm run lint && npm run build   # בדיקות האתר (כמו ב-CI)
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
| `Mcp__Enabled` | | מתג ראשי לעוזרי AI (תוכנית 011). `false` (ברירת מחדל): `/mcp` ו-`/oauth/*` מחזירים 503 |
| `Mcp__Issuer` | עם MCP | הכתובת הציבורית של ה-API עם `/` בסוף, למשל `https://houroffice-api-….run.app/` |
| `Mcp__SiteUrl` | עם MCP | כתובת האתר (דף ההסכמה `/McpConsent`), למשל `https://houroffice.pages.dev/` |
| `Mcp__SigningKey` | עם MCP | מפתח RSA פרטי (PEM) שחותם על הטוקנים. **סוד** |
| `Mcp__EncryptionKey` | עם MCP | 32 בתים ב-base64 שמצפינים את הטוקנים (JWE). **סוד** |
| `Mcp__AllowedOrigins__0` | | מקורות דפדפן שמותר להם לקרוא ל-`/mcp` (בדרך כלל ריק) |
| `Mcp__BookingWritesPerHour`, `Mcp__AdminWritesPerHour`, `Mcp__CallsPerMinute` | | מגבלות (10 / 30 / 60) |
| `FieldEncryption__Mode` | | `off` (ברירת מחדל), `encrypt` או `decrypt` (הדרך חזרה). הצפנת טלפונים, אימיילים של לקוחות והערות |
| `FieldEncryption__KmsProvider` | | `gcp` בייצור (Cloud KMS), `local` רק בפיתוח ובבדיקות |
| `FieldEncryption__Gcp__ProjectId` | עם הצפנה | `houroffice-prod` (ה-key ring `houroffice`, המפתח `mongo-cmk`, `europe-west1` הם ברירות המחדל) |

כניסת המפתחים (`Auth__DevLoginEnabled`) **לא פועלת בסביבת Production** גם אם מופעלת בטעות.

## Google Sign-In

1. https://console.cloud.google.com → APIs & Services → Credentials → Create OAuth client ID → Web application.
2. **Authorized JavaScript origins:** כתובת האתר (ו-`http://localhost:5500` לפיתוח).
3. את ה-Client ID שמים ב-`Auth__GoogleClientId` של ה-API. האתר מקבל אותו אוטומטית מ-`/api/settings/public`.

## פריסה

האתר באוויר בעלות של כ-$0 לחודש (כל השירותים בשכבה החינמית). ההחלטות וההיסטוריה: [`.plan/006-2026-11-17-low-cost-deployment.md`](../.plan/006-2026-11-17-low-cost-deployment.md).
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

ה-`frontend/public/config.js` בריפו מצביע על localhost ונשאר כך. בכל פריסה בונים את האתר, מעתיקים את `frontend/dist/` (רק אותה – לעולם לא את `frontend/` עצמה, שבה `node_modules` של ~120MB) לתיקייה זמנית, כותבים בה `config.js` של ייצור ומעלים:

```bash
cd frontend && npm ci && npm run build   # יוצר את frontend/dist
```

```js
// config.js בעותק של dist לפריסה בלבד
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

### עוזרי AI (MCP) – תוכנית 011

שרת ה-MCP רץ בתוך אותו שירות Cloud Run (`/mcp`), ושרת ה-OAuth שלו הוא OpenIddict באותו תהליך. אין פריסה שלישית ואין עלות נוספת.

**הקמה חד-פעמית של הסודות** (המפתחות נוצרים מקומית ונכנסים ישר ל-Secret Manager דרך stdin – לא מודפסים, לא נשמרים בקובץ):
```bash
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 | gcloud secrets create mcp-signing-key --data-file=-
openssl rand -base64 32 | tr -d '\n' | gcloud secrets create mcp-encryption-key --data-file=-
gcloud secrets add-iam-policy-binding mcp-signing-key --member=serviceAccount:houroffice-api@houroffice-prod.iam.gserviceaccount.com --role=roles/secretmanager.secretAccessor
gcloud secrets add-iam-policy-binding mcp-encryption-key --member=serviceAccount:houroffice-api@houroffice-prod.iam.gserviceaccount.com --role=roles/secretmanager.secretAccessor
```

**פריסה:** מוסיפים ל-`env.prod.yaml`:
```yaml
Mcp__Enabled: "false"   # קודם "בחושך"; מדליקים ל-"true" אחרי הבדיקה (שלב 3 בתוכנית 011)
Mcp__Issuer: "https://houroffice-api-770124643502.europe-west1.run.app/"
Mcp__SiteUrl: "https://houroffice.pages.dev/"
```
ולפקודת הפריסה מוסיפים ל-`--set-secrets`: `,Mcp__SigningKey=mcp-signing-key:latest,Mcp__EncryptionKey=mcp-encryption-key:latest`.
בדיקה: `curl -s -o /dev/null -w "%{http_code}" -X POST .../mcp` מחזיר 503 כשכבוי ו-401 כשדולק; `curl .../.well-known/oauth-authorization-server` מחזיר את ה-issuer וה-`registration_endpoint`.
בייצור, `Mcp__Enabled=true` בלי שני המפתחות עוצר את עליית השרת (בכוונה).

**כתובת החיבור לעוזר:** `https://houroffice-api-770124643502.europe-west1.run.app/mcp`. ב-Claude: Settings → Connectors → Add custom connector → הכתובת. ההתחברות עוברת דרך דף ההסכמה באתר.

**מתג כיבוי:** הכי מהר – מתג "לאפשר עוזרי AI" ב**הגדרות האתר** (שניות, בלי פריסה). המתג הראשי הוא `Mcp__Enabled` (revision חדש). "לנתק את כולם" באותו מסך מבטל כל טוקן מיד.

**החלפת מפתחות (כל 6 חודשים או בחשד לדליפה):** מוסיפים גרסה לסוד (כמו למעלה, עם `gcloud secrets versions add`) ופורסים מחדש. כל הטוקנים הקיימים נפסלים, והמשתמשים מתחברים שוב דרך דף ההסכמה.

**Claude / ChatGPT כ"מאומתים":** לפי כתובות ה-redirect ב-`Mcp:KnownClients` (`appsettings.json`). לבדוק מול התיעוד של כל ספק לפני ההשקה; אם כתובת השתנתה, החיבור עובד אבל מוצג "לא מאומת".

### הצפנת שדות (Phase C של תוכנית 011)

טלפונים (`users.phoneNumber`, `clients.phones`), אימיילים של לקוחות (`clients.emails`) והערות (`clients.notes`, `bookings.notes`) נשמרים מוצפנים (MongoDB CSFLE, מצב explicit, עובד ב-Atlas M0). מפתח הנתונים נשמר באוסף `encryptionKeys`, עטוף במפתח-אב ב-Cloud KMS.

**הקמה חד-פעמית:**
```bash
gcloud services enable cloudkms.googleapis.com
gcloud kms keyrings create houroffice --location europe-west1
gcloud kms keys create mongo-cmk --keyring houroffice --location europe-west1 --purpose encryption --rotation-period 365d --next-rotation-time 2027-11-22T00:00:00Z
gcloud kms keys add-iam-policy-binding mongo-cmk --keyring houroffice --location europe-west1 --member=serviceAccount:houroffice-api@houroffice-prod.iam.gserviceaccount.com --role=roles/cloudkms.cryptoKeyEncrypterDecrypter
```
⚠️ **לעולם לא להשמיד את `mongo-cmk`** – בלעדיו השדות המוצפנים אבודים. השמדה ב-KMS מחכה 30 יום; אפשר לבטל אותה בזמן הזה.

**הפעלה:** קודם גיבוי מלא (הבעלים, מחוץ לענן): `mongodump --uri "$(gcloud secrets versions access latest --secret=mongo-connection-string)" --out <תיקייה-מקומית>`. אחר כך ל-`env.prod.yaml`:
```yaml
FieldEncryption__Mode: "encrypt"
FieldEncryption__KmsProvider: "gcp"
FieldEncryption__Gcp__ProjectId: "houroffice-prod"
```
ופריסה. ההצפנה של המסמכים הקיימים רצה ברקע בעליית השרת (קריאה עובדת גם על ערכים ישנים בינתיים). בדיקה ב-`mongosh`: `db.clients.findOne().phones` מחזיר `Binary(..., 6)`, ו-`db.siteSettings.findOne({key:"fields_encrypted"})` מחזיר `"true"`.

**החזרה לאחור:** פורסים עם `FieldEncryption__Mode: "decrypt"`, מחכים ש-`fields_encrypted` יהיה `"false"`, ורק אז `off` או revision ישן. **revision מלפני ההצפנה לא יודע לקרוא שדות מוצפנים** – לא לחזור אליו לפני הפענוח. עם `off` ונתונים מוצפנים, השרת מחזיר שגיאה ולא נתונים משובשים.

### הזמנת משתמשים

בפרודקשן ההרשמה פתוחה (`Auth__AllowOpenSignup=true`, מאז 2026-11-22): כל חשבון Google נכנס כמשתמש רגיל, ומנהלים נוספים מקודמים במסך **משתמשים**. אפשר גם להוסיף משתמש מראש (אימייל + תפקיד) כדי שייכנס ישר עם התפקיד הנכון.

`Auth__AllowOpenSignup=false`: רק מי שהמנהל הוסיף במסך **משתמשים** (אימייל + תפקיד) יכול להיכנס. כשהוא נכנס עם Google הוא מקבל את התפקיד שנקבע לו.
