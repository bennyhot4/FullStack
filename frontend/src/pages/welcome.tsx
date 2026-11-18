import { useState } from "react";
import { t, type TranslationKey } from "../i18n";
import { Link } from "../router";
import { Carousel } from "../components/carousel";
import { Icon, type IconName } from "../ui";
import { LanguageButton } from "../ui/language";

const SLIDES: { img: string; title: TranslationKey; sub: TranslationKey }[] = [
  { img: "https://images.unsplash.com/photo-1497366216548-37526070297c?w=800&auto=format", title: "premiumOffices", sub: "bookProfessional" },
  { img: "https://images.unsplash.com/photo-1497215842964-222b430dc094?w=800&auto=format", title: "flexibleBooking", sub: "chooseYourTime" },
  { img: "https://images.unsplash.com/photo-1600494603989-9650cf6ddd3d?w=800&auto=format", title: "modernAmenities", sub: "everythingYouNeed" },
  { img: "https://images.unsplash.com/photo-1582653291997-079a1c04e5a1?w=800&auto=format", title: "perfectForTeams", sub: "spacesForCollaboration" },
];

const Tile = ({ icon, label }: { icon: IconName; label: string }) => <div className="tile"><Icon name={icon} />{label}</div>;

export default function Welcome() {
  const [slide, setSlide] = useState(0);
  const [dots, setDots] = useState<HTMLDivElement | null>(null);

  return (
    <div className="welcome">
      <Carousel autoplay={4500} onChange={setSlide} dotsHost={dots}
        slides={SLIDES.map((s, i) => <img key={s.img} src={s.img} alt="" loading={i ? "lazy" : "eager"} />)} />
      <div className="top-actions"><LanguageButton /></div>
      <div className="content">
        <div className="slide-text">
          <h1 style={{ fontSize: "1.9rem" }}>{t(SLIDES[slide].title)}</h1>
          <p style={{ opacity: 0.85, marginTop: "6px" }}>{t(SLIDES[slide].sub)}</p>
        </div>
        <div ref={setDots} />
        <div className="tiles">
          <Tile icon="clock" label={t("hourly")} />
          <Tile icon="pin" label={t("primeLocation")} />
          <Tile icon="users" label={t("anySize")} />
        </div>
        <Link className="btn light lg block" href="/">{t("exploreOffices")}<Icon name="arrowRight" className="flip-rtl" /></Link>
        <p className="xs center" style={{ opacity: 0.6, marginTop: "14px" }}>{t("availableSpaces")}</p>
      </div>
    </div>
  );
}
