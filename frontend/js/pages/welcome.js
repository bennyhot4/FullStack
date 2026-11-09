import { t } from "../i18n.js";
import { languageButton } from "../app.js";
import { carousel } from "../components/carousel.js";
import { clear, h, icon } from "../ui.js";

const SLIDES = [
  { img: "https://images.unsplash.com/photo-1497366216548-37526070297c?w=800&auto=format", title: "premiumOffices", sub: "bookProfessional" },
  { img: "https://images.unsplash.com/photo-1497215842964-222b430dc094?w=800&auto=format", title: "flexibleBooking", sub: "chooseYourTime" },
  { img: "https://images.unsplash.com/photo-1600494603989-9650cf6ddd3d?w=800&auto=format", title: "modernAmenities", sub: "everythingYouNeed" },
  { img: "https://images.unsplash.com/photo-1582653291997-079a1c04e5a1?w=800&auto=format", title: "perfectForTeams", sub: "spacesForCollaboration" },
];

export default function welcome(el) {
  const heading = h("h1", { style: { fontSize: "1.9rem" } });
  const sub = h("p", { style: { opacity: 0.85, marginTop: "6px" } });
  const dots = h("div");
  const setText = (i) => { heading.textContent = t(SLIDES[i].title); sub.textContent = t(SLIDES[i].sub); };

  const slides = SLIDES.map((s, i) => h("img", { src: s.img, alt: "", loading: i ? "lazy" : "eager" }));
  const car = carousel(slides, { autoplay: 4500, onChange: setText, dotsHost: dots });
  setText(0);

  const tile = (name, label) => h("div", { class: "tile" }, icon(name), label);
  clear(el,
    h("div", { class: "welcome" },
      car,
      h("div", { class: "top-actions" }, languageButton()),
      h("div", { class: "content" },
        h("div", { class: "slide-text" }, heading, sub),
        dots,
        h("div", { class: "tiles" }, tile("clock", t("hourly")), tile("pin", t("primeLocation")), tile("users", t("anySize"))),
        h("a", { class: "btn light lg block", href: "/", "data-link": "" }, t("exploreOffices"), icon("arrowRight", "flip-rtl")),
        h("p", { class: "xs center", style: { opacity: 0.6, marginTop: "14px" } }, t("availableSpaces")))));
}
