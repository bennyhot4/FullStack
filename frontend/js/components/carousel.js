// Swipeable carousel built on CSS scroll-snap (native touch scrolling, no library).
import { h } from "../ui.js";
import { isRtl } from "../i18n.js";

/**
 * slides: array of Nodes. Options: autoplay (ms), className, onChange(index).
 * Returns the root element.
 */
export function carousel(slides, { autoplay = 0, className = "", onChange, dotsHost } = {}) {
  const track = h("div", { class: "carousel-track" }, slides.map((s) => h("div", { class: "carousel-slide" }, s)));
  const dots = h("div", { class: "carousel-dots" });
  const root = h("div", { class: `carousel ${className}`, role: "region", "aria-roledescription": "carousel" }, track, dotsHost ? null : dots);
  if (dotsHost) dotsHost.append(dots);
  let index = 0;
  let timer = null;

  const go = (i) => {
    index = (i + slides.length) % slides.length;
    const w = track.clientWidth;
    track.scrollTo({ left: (isRtl() ? -1 : 1) * index * w, behavior: "smooth" });
    update();
  };
  const update = () => {
    [...dots.children].forEach((d, i) => d.setAttribute("aria-current", String(i === index)));
    onChange?.(index);
  };

  if (slides.length > 1) {
    slides.forEach((_, i) => dots.append(h("button", { type: "button", "aria-label": `${i + 1}`, onclick: () => { go(i); restart(); } })));
    track.addEventListener("scroll", () => {
      const w = track.clientWidth || 1;
      const i = Math.round(Math.abs(track.scrollLeft) / w);
      if (i !== index) { index = i; update(); }
    }, { passive: true });
  }

  const restart = () => {
    if (!autoplay || slides.length < 2) return;
    clearInterval(timer);
    timer = setInterval(() => { if (!root.isConnected) return clearInterval(timer); go(index + 1); }, autoplay);
  };
  track.addEventListener("pointerdown", restart, { passive: true });
  requestAnimationFrame(update);
  restart();
  return root;
}
