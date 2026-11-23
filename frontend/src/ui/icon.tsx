// Inline SVG icons (stroke = currentColor). Reuse the same name for the same concept; add a path here
// rather than pulling an icon library.
const PATHS = {
  search: "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm10 2-4.35-4.35",
  compass: "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20Zm4.24-14.24-2.12 6.36-6.36 2.12 2.12-6.36 6.36-2.12Z",
  calendar: "M8 2v4m8-4v4M3 10h18M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z",
  building: "M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18Zm-2 0h16M10 6h4m-4 4h4m-4 4h4m-4 4h4",
  users: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2m20 0v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z",
  user: "M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2m8-10a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z",
  chart: "M3 3v18h18M7 16v-5m5 5V8m5 8v-8",
  list: "M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01",
  clock: "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20Zm0-16v6l4 2",
  pin: "M12 22s7-6.2 7-12a7 7 0 1 0-14 0c0 5.8 7 12 7 12Zm0-9a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z",
  logout: "M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4m7 14 5-5-5-5m5 5H9",
  globe: "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20ZM2 12h20M12 2a15 15 0 0 1 0 20 15 15 0 0 1 0-20Z",
  arrowLeft: "M19 12H5m7 7-7-7 7-7",
  arrowRight: "M5 12h14m-7-7 7 7-7 7",
  chevronLeft: "m15 18-6-6 6-6",
  chevronRight: "m9 18 6-6-6-6",
  plus: "M12 5v14M5 12h14",
  edit: "M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z",
  trash: "M3 6h18m-2 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2",
  x: "M18 6 6 18M6 6l12 12",
  check: "M20 6 9 17l-5-5",
  checkCircle: "M22 11.08V12a10 10 0 1 1-5.93-9.14M22 4 12 14.01l-3-3",
  alert: "M12 9v4m0 4h.01M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z",
  shield: "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z",
  file: "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Zm0 0v6h6M16 13H8m8 4H8m2-8H8",
  settings: "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm7.4-3a7.4 7.4 0 0 0-.1-1.2l2-1.6-2-3.4-2.4 1a7.3 7.3 0 0 0-2-1.2L14.5 3h-5l-.4 2.6a7.3 7.3 0 0 0-2 1.2l-2.4-1-2 3.4 2 1.6a7.4 7.4 0 0 0 0 2.4l-2 1.6 2 3.4 2.4-1a7.3 7.3 0 0 0 2 1.2l.4 2.6h5l.4-2.6a7.3 7.3 0 0 0 2-1.2l2.4 1 2-3.4-2-1.6c.1-.4.1-.8.1-1.2Z",
  repeat: "m17 2 4 4-4 4M3 11V9a4 4 0 0 1 4-4h14M7 22l-4-4 4-4m14-1v2a4 4 0 0 1-4 4H3",
  wifi: "M5 12.55a11 11 0 0 1 14 0M1.42 9a16 16 0 0 1 21.16 0M8.53 16.11a6 6 0 0 1 6.95 0M12 20h.01",
  coffee: "M17 8h1a4 4 0 1 1 0 8h-1M3 8h14v9a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4ZM6 2v2m4-2v2m4-2v2",
  car: "M5 17h14M6 17v2m12-2v2M5 17l1.5-6h11l1.5 6M7 11l1.5-4h7L17 11M7.5 14h.01m9 0h.01",
  phone: "M22 16.92v3a2 2 0 0 1-2.18 2 19.8 19.8 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.12 4.18 2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.13.96.36 1.9.7 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.9.34 1.85.57 2.81.7A2 2 0 0 1 22 16.92Z",
  projector: "M2 8h20v8H2ZM7 12h.01M16 12a2 2 0 1 0-4 0 2 2 0 0 0 4 0ZM6 16v3m12-3v3",
  sparkle: "M12 3l1.9 5.8L20 10.7l-5.8 1.9L12 18.5l-1.9-5.9L4 10.7l6.1-1.9Z",
  mail: "M4 4h16a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Zm18 2-10 7L2 6",
  upload: "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4m14-7-5-5-5 5m5-5v12",
  palette: "M12 22a10 10 0 1 1 10-10c0 2.5-2 3-3.5 3H16a2 2 0 0 0-1.5 3.3A2 2 0 0 1 12 22ZM7.5 10.5h.01M12 7.5h.01M16.5 10.5h.01",
  image: "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Zm3.5 7a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3ZM21 15l-5-5L5 21",
  client: "M6 2h12a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2Zm6 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm-5 6a5 5 0 0 1 10 0",
  grid: "M3 3h7v7H3Zm11 0h7v7h-7Zm0 11h7v7h-7ZM3 14h7v7H3Z",
  link: "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71",
} as const;

export type IconName = keyof typeof PATHS;

export function Icon({ name, className = "" }: { name: IconName; className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round"
      aria-hidden="true" className={`icon ${className}`.trim()}>
      <path d={PATHS[name]} />
    </svg>
  );
}

/** Amenity name -> icon (the original app mapped WiFi, Projector, Coffee, Parking, Phone). */
export function amenityIcon(name: string): IconName {
  const n = name.toLowerCase();
  if (n.includes("wifi") || n.includes("wi-fi") || n.includes("אינטרנט")) return "wifi";
  if (n.includes("projector") || n.includes("מקרן")) return "projector";
  if (n.includes("coffee") || n.includes("קפה")) return "coffee";
  if (n.includes("parking") || n.includes("חניה")) return "car";
  if (n.includes("phone") || n.includes("טלפון")) return "phone";
  return "sparkle";
}
