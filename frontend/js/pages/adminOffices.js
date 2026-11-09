import { del, get, post, put } from "../api.js";
import { t } from "../i18n.js";
import { loadSettings, money, store } from "../store.js";
import { imageInput } from "../components/imageInput.js";
import { clear, confirmSheet, empty, errorBox, field, h, icon, openSheet, pageHeader, skeletons, toast } from "../ui.js";

export default async function adminOffices(el) {
  const list = h("div", { class: "grid-cards" });
  const banners = h("div");

  clear(el,
    pageHeader({
      title: t("manageOffices"), subtitle: t("editPricing"),
      actions: h("a", { class: "icon-btn flat", href: "/SiteSettingsPage", "data-link": "", "aria-label": t("siteSettings"), title: t("siteSettings") }, icon("settings")),
    }),
    h("div", { class: "container wide stack", style: { paddingBlock: "16px", "--gap": "16px" } },
      h("button", { class: "btn block", type: "button", onclick: () => edit(null) }, icon("plus"), t("addOffice")),
      list,
      h("section", { class: "card pad stack" }, h("h2", null, t("homeBanners")), banners)));

  async function load() {
    clear(list, skeletons(2, 200));
    try {
      const offices = await get("/api/admin/offices");
      clear(list, offices.length ? offices.map(card) : empty("building", t("noOfficesFound")));
    } catch (e) { clear(list, errorBox(e, load)); }
  }

  const card = (o) => h("article", { class: "card office-card" },
    h("div", { class: "cover", style: { aspectRatio: "16 / 7" } },
      o.imageUrl ? h("img", { src: o.imageUrl, alt: "", loading: "lazy" }) : null,
      h("span", { class: "price-tag" }, h("bdi", null, money(o.hourlyRate, o.currency)), " ", h("span", { class: "xs muted" }, t("perHour")))),
    h("div", { class: "stack", style: { padding: "14px 16px", "--gap": "8px" } },
      h("div", { class: "row between" }, h("h3", { class: "truncate" }, o.name),
        h("span", { class: `badge ${o.isAvailable ? "success" : ""}` }, o.isAvailable ? t("available") : t("unavailable"))),
      h("div", { class: "meta" }, h("span", null, icon("users", "sm"), `${o.capacity} ${t("people")}`),
        o.location ? h("span", null, icon("pin", "sm"), o.location) : null),
      h("div", { class: "row", style: { justifyContent: "flex-end" } },
        h("button", { class: "btn outline sm", type: "button", onclick: () => edit(o) }, icon("edit", "sm"), t("edit")),
        h("button", { class: "btn danger-outline sm", type: "button", onclick: () => remove(o) }, icon("trash", "sm"), t("delete")))));

  function edit(o) {
    const name = h("input", { class: "input", required: true, maxlength: 100, value: o?.name || "" });
    const description = h("textarea", { class: "textarea", maxlength: 2000 }, o?.description || "");
    const image = imageInput({ value: o?.imageUrl || "", label: t("image") });
    const capacity = h("input", { class: "input", type: "number", inputmode: "numeric", min: 1, max: 500, required: true, value: o?.capacity ?? "" });
    const rate = h("input", { class: "input", type: "number", inputmode: "decimal", min: 0, step: "0.5", required: true, value: o?.hourlyRate ?? "" });
    const location = h("input", { class: "input", value: o?.location || "" });
    const amenities = h("input", { class: "input", value: (o?.amenities || []).join(", "), placeholder: "WiFi, Coffee, Parking" });
    const available = h("input", { type: "checkbox", checked: o ? o.isAvailable : true });
    const err = h("p", { class: "error-text", role: "alert" });
    const formId = "office-form";

    const form = h("form", {
      id: formId, class: "stack", style: { "--gap": "14px" },
      onsubmit: async (e) => {
        e.preventDefault();
        const body = {
          name: name.value.trim(), description: description.value.trim(), imageUrl: image.getValue(),
          capacity: Number(capacity.value), hourlyRate: Number(rate.value), location: location.value.trim(),
          amenities: amenities.value.split(",").map((s) => s.trim()).filter(Boolean), isAvailable: available.checked,
        };
        try {
          if (o) await put(`/api/admin/offices/${o.id}`, body); else await post("/api/admin/offices", body);
          sheet.close();
          load();
        } catch (ex) { err.textContent = ex.message; }
      },
    },
    field(t("officeName"), name), field(t("description"), description), image,
    h("div", { class: "row", style: { "--gap": "10px", alignItems: "flex-start" } },
      h("div", { class: "grow" }, field(t("capacity"), capacity)),
      h("div", { class: "grow" }, field(`${t("hourlyRate")} (${money(0).replace(/[\d.,\s]/g, "")})`, rate))),
    field(t("location"), location), field(t("amenities"), amenities),
    h("label", { class: "switch" }, available, t("availableForBooking")), err);

    const sheet = openSheet({
      title: o ? t("editOffice") : t("addNewOffice"), body: form,
      actions: (close) => [
        h("button", { class: "btn outline", type: "button", onclick: close }, t("cancel")),
        h("button", { class: "btn", type: "submit", form: formId }, o ? t("update") : t("create")),
      ],
    });
  }

  async function remove(o) {
    if (!await confirmSheet({ title: t("deleteOffice"), message: `${t("deleteOfficeConfirm")} ${o.name}?`, confirmText: t("delete"), danger: true })) return;
    try { await del(`/api/admin/offices/${o.id}`); load(); } catch (e) { toast(e.message, "error"); }
  }

  function drawBanners() {
    const items = store.settings.banners;
    let pending = "";
    const adder = imageInput({ onChange: (v) => { pending = v; } });
    clear(banners, h("div", { class: "stack" },
      items.length ? h("div", { class: "thumbs" }, items.map((b) => h("div", { class: "thumb" }, h("img", { src: b.url, alt: "" }),
        h("button", {
          class: "icon-btn", type: "button", "aria-label": t("delete"),
          onclick: async () => { try { await del(`/api/admin/settings/banners/${b.id}`); await loadSettings(); drawBanners(); } catch (e) { toast(e.message, "error"); } },
        }, icon("trash", "sm"))))) : h("p", { class: "small muted" }, t("noBannersYet")),
      adder,
      h("button", {
        class: "btn outline", type: "button",
        onclick: async () => {
          if (!pending) return;
          try { await post("/api/admin/settings/banners", { value: pending }); await loadSettings(); drawBanners(); } catch (e) { toast(e.message, "error"); }
        },
      }, icon("plus", "sm"), t("addBanner"))));
  }

  load();
  drawBanners();
}
