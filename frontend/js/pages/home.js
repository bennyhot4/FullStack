import { get } from "../api.js";
import { t } from "../i18n.js";
import { money, store } from "../store.js";
import { carousel } from "../components/carousel.js";
import { amenityIcon, clear, empty, errorBox, h, icon, skeletons } from "../ui.js";

const FALLBACK_IMG = "https://images.unsplash.com/photo-1497366216548-37526070297c?w=600&auto=format";

export default async function home(el) {
  const s = store.settings;
  const banners = s.banners.map((b) => b.url);
  if (!banners.length && s.homeBannerImage) banners.push(s.homeBannerImage);

  const list = h("div", { class: "grid-cards" });

  clear(el,
    h("header", { class: "page-header", style: { padding: 0 } },
      banners.length ? carousel(banners.map((src) => h("img", { src, alt: "" })), { autoplay: 5000, className: "banner" }) : null,
      h("div", { class: "container", style: { paddingBlock: banners.length ? "16px" : "calc(16px + var(--safe-top)) 16px" } },
        h("div", { class: "row", style: { "--gap": "12px", minHeight: "44px", paddingInlineEnd: banners.length ? 0 : "96px" } },
          s.branding.logo ? h("img", { src: s.branding.logo, alt: "", style: { width: "44px", height: "44px", borderRadius: "12px", objectFit: "cover" } }) : null,
          h("div", { class: "header-title" },
            h("h1", null, s.branding.title || t("officeBooking")),
            h("p", { class: "small muted" }, s.branding.description || t("findAndBook")))))),
    h("div", { class: "container wide", style: { paddingBlock: "16px" } }, list));

  let seq = 0;
  async function load() {
    const my = ++seq;
    clear(list, skeletons(3, 280));
    try {
      const offices = await get("/api/offices");
      if (my !== seq) return;
      clear(list, offices.length ? offices.map(officeCard) : empty("search", t("noOfficesFound")));
    } catch (e) {
      if (my === seq) clear(list, errorBox(e, load));
    }
  }
  load();
}

export function officeCard(o) {
  return h("article", { class: "card office-card" },
    h("div", { class: "cover" },
      h("img", { src: o.imageUrl || FALLBACK_IMG, alt: "", loading: "lazy" }),
      h("span", { class: "price-tag" }, h("bdi", null, money(o.hourlyRate, o.currency)), " ", h("span", { class: "xs muted" }, t("perHour")))),
    h("div", { class: "stack", style: { padding: "16px", "--gap": "10px" } },
      h("h3", null, o.name),
      h("div", { class: "meta" },
        h("span", null, icon("users", "sm"), `${o.capacity} ${t("people")}`),
        o.location ? h("span", null, icon("pin", "sm"), o.location) : null),
      o.amenities.length ? h("div", { class: "row wrap", style: { "--gap": "6px" } },
        o.amenities.slice(0, 3).map((a) => h("span", { class: "badge" }, icon(amenityIcon(a), "sm"), a))) : null,
      h("a", { class: "btn block", href: `/BookOffice?id=${encodeURIComponent(o.id)}`, "data-link": "" }, t("bookNow"))));
}
