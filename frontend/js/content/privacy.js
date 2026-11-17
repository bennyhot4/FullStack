// Privacy policy text (plan 007). Static on purpose: it changes rarely and every change is reviewed in git.
// Markdown subset rendered by components/markdown.js: "## heading", "**bold**", "- list", paragraphs.
// When a feature adds or removes personal data, update both languages here and bump `version` + `updated`.

export const version = 1;
export const updated = "2026-11-17";

export const he = `## מי אנחנו
HourOffice היא מערכת להזמנת משרדים וחדרים לפי שעה. המערכת מופעלת על ידי **בני חוטיאקוב**. לכל שאלה או בקשה בנושא פרטיות אפשר לפנות לכתובת **benny.hot4@gmail.com**.

## איזה מידע אנחנו אוספים
**מהכניסה עם Google** (ההרשאות "email" ו-"profile" בלבד):
- כתובת האימייל, השם המלא, השם הפרטי ושם המשפחה
- הקישור לתמונת הפרופיל ומזהה החשבון ב-Google
- איננו מקבלים את הסיסמה שלך, את אנשי הקשר, את הקבצים או כל מידע אחר מחשבון Google.

**מידע שאתם מזינים:**
- מספר טלפון
- הזמנות: משרד, תאריך, שעות, הערות ומחיר
- רשימת הלקוחות הפרטית: שמות, עד 2 טלפונים ו-2 כתובות אימייל, שפה, הסכמה לקבלת הודעות WhatsApp ומועד ההסכמה, והערות
- גרסת תנאי השימוש שאישרתם ומועד האישור

## למה אנחנו משתמשים במידע
- כדי לזהות אותך ולאפשר כניסה למערכת
- כדי לנהל הזמנות ולהציג אותן לך ולמנהלי המערכת
- כדי לאפשר לך לנהל את רשימת הלקוחות שלך ולשייך לקוח להזמנה
- המערכת אינה שולחת הודעות ללקוחות שלך. ההסכמה לקבלת WhatsApp נשמרת בלבד.

## מי רואה את המידע
- **רשימת הלקוחות ושמות הלקוחות בהזמנות גלויים רק לך.** מנהלי המערכת אינם רואים אותם.
- מנהלי המערכת רואים את פרטי המשתמשים, את ההזמנות ואת הערות ההזמנה.
- איננו מוכרים מידע, איננו משתפים אותו עם מפרסמים ואיננו משתמשים בו לפרסום.

## איפה המידע נשמר
- מסד הנתונים: MongoDB Atlas, בשרתי Google Cloud בבלגיה
- שרת המערכת: Google Cloud Run, בבלגיה
- תמונות המשרדים שמעלים מנהלי המערכת: Cloudflare R2. התמונות האלה ציבוריות.
- האתר עצמו: Cloudflare Pages
- בדפדפן נשמרים רק אסימוני הכניסה והשפה שבחרת (localStorage). האתר אינו משתמש בעוגיות (cookies), בכלי ניתוח או בפרסומות.
- Google שומרת יומני גישה לשרת (כתובת IP, שעה וכתובת הבקשה) למשך 30 יום.

## מחיקת המידע
אפשר לבקש מחיקה של החשבון בפנייה לכתובת **benny.hot4@gmail.com**. מחיקת חשבון מוחקת מיד את פרטי המשתמש, את כל ההזמנות שלו, את רשימת הלקוחות שלו ואת אסימוני הכניסה. אין גיבויים של מסד הנתונים, ולכן לא נשאר עותק.

## שינויים במדיניות
כשהמדיניות משתנה, תאריך העדכון בראש הדף מתעדכן.`;

export const en = `## Who we are
HourOffice is a system for booking offices and rooms by the hour. It is operated by **Benny Hutiakov**. For any privacy question or request, write to **benny.hot4@gmail.com**.

## What we collect
**From Google sign-in** (only the "email" and "profile" permissions):
- Your email address, full name, first name and last name
- Your profile picture link and your Google account id
- We never receive your Google password, contacts, files or any other data from your Google account.

**What you enter:**
- Your phone number
- Bookings: office, date, hours, notes and price
- Your private client list: names, up to 2 phones and 2 email addresses, language, WhatsApp consent and when it was given, and notes
- Which version of the terms of use you accepted, and when

## Why we use it
- To identify you and let you sign in
- To manage bookings and show them to you and to the administrators
- To let you manage your client list and link a client to a booking
- The system does not send messages to your clients. The WhatsApp consent is only stored.

## Who can see it
- **Your client list, and client names on your bookings, are visible only to you.** Administrators cannot see them.
- Administrators can see user details, bookings and booking notes.
- We do not sell data, share it with advertisers or use it for advertising.

## Where it is stored
- Database: MongoDB Atlas, on Google Cloud servers in Belgium
- Application server: Google Cloud Run, in Belgium
- Office images uploaded by administrators: Cloudflare R2. These images are public.
- The website itself: Cloudflare Pages
- Your browser stores only your sign-in tokens and your chosen language (localStorage). The site uses no cookies, no analytics and no ads.
- Google keeps server access logs (IP address, time and requested address) for 30 days.

## Deleting your data
You can ask for your account to be deleted by writing to **benny.hot4@gmail.com**. Deleting an account immediately removes your user details, all your bookings, your client list and your sign-in tokens. The database has no backups, so no copy remains.

## Changes to this policy
When this policy changes, the date at the top of the page is updated.`;
