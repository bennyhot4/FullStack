// Swipeable carousel built on CSS scroll-snap (native touch scrolling, no library).
import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { isRtl } from "../i18n";

interface Props {
  slides: ReactNode[];
  /** Milliseconds between slides; 0 = no autoplay. */
  autoplay?: number;
  className?: string;
  onChange?: (index: number) => void;
  /** Render the dots somewhere else on the page (the welcome page puts them under its text). */
  dotsHost?: HTMLElement | null;
}

export function Carousel({ slides, autoplay = 0, className = "", onChange, dotsHost }: Props) {
  const track = useRef<HTMLDivElement>(null);
  const timer = useRef<number | undefined>(undefined);
  const indexRef = useRef(0);
  const onChangeRef = useRef(onChange);
  const [index, setIndex] = useState(0);
  const count = slides.length;

  useEffect(() => { onChangeRef.current = onChange; });

  const update = useCallback((i: number) => {
    indexRef.current = i;
    setIndex(i);
    onChangeRef.current?.(i);
  }, []);

  const go = useCallback((i: number) => {
    const next = (i + count) % count;
    const el = track.current;
    if (el) el.scrollTo({ left: (isRtl() ? -1 : 1) * next * el.clientWidth, behavior: "smooth" });
    update(next);
  }, [count, update]);

  const restart = useCallback(() => {
    if (!autoplay || count < 2) return;
    window.clearInterval(timer.current);
    timer.current = window.setInterval(() => go(indexRef.current + 1), autoplay);
  }, [autoplay, count, go]);

  useEffect(() => {
    restart();
    return () => window.clearInterval(timer.current);
  }, [restart]);

  const onScroll = () => {
    const el = track.current;
    if (!el) return;
    const i = Math.round(Math.abs(el.scrollLeft) / (el.clientWidth || 1));
    if (i !== indexRef.current) update(i);
  };

  const dots = (
    <div className="carousel-dots">
      {count > 1 ? slides.map((_, i) => (
        <button key={i} type="button" aria-label={`${i + 1}`} aria-current={i === index ? "true" : "false"} onClick={() => { go(i); restart(); }} />
      )) : null}
    </div>
  );

  return (
    <div className={`carousel ${className}`} role="region" aria-roledescription="carousel">
      <div className="carousel-track" ref={track} onScroll={count > 1 ? onScroll : undefined} onPointerDown={restart}>
        {slides.map((s, i) => <div key={i} className="carousel-slide">{s}</div>)}
      </div>
      {dotsHost === undefined ? dots : dotsHost ? createPortal(dots, dotsHost) : null}
    </div>
  );
}
