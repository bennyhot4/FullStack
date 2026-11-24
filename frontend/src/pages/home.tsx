import { useState } from "react";
import { get } from "../api";
import { t } from "../i18n";
import { Link } from "../router";
import { money, settings } from "../store";
import type { ConnectedApp, OfficeDto } from "../types/api";
import { useAsync } from "../useAsync";
import { Carousel } from "../components/carousel";
import { Empty, ErrorBox, Icon, Skeletons, amenityIcon } from "../ui";

const FALLBACK_IMG = "https://images.unsplash.com/photo-1497366216548-37526070297c?w=600&auto=format";

export default function Home() {
  const s = settings();
  const banners = s.banners.map((b) => b.url);
  if (!banners.length && s.homeBannerImage) banners.push(s.homeBannerImage);
  const { state, reload } = useAsync(() => get<OfficeDto[]>("/api/offices"));

  return (
    <>
      <header className="page-header" style={{ padding: 0 }}>
        {banners.length ? <Carousel autoplay={5000} className="banner" slides={banners.map((src) => <img key={src} src={src} alt="" />)} /> : null}
        <div className="container" style={{ paddingBlock: banners.length ? "16px" : "calc(16px + var(--safe-top)) 16px" }}>
          <div className="row" style={{ "--gap": "12px", minHeight: "44px", paddingInlineEnd: banners.length ? 0 : "96px" }}>
            {s.branding.logo ? <img src={s.branding.logo} alt="" style={{ width: "44px", height: "44px", borderRadius: "12px", objectFit: "cover" }} /> : null}
            <div className="header-title">
              <h1>{s.branding.title || t("officeBooking")}</h1>
              <p className="small muted">{s.branding.description || t("findAndBook")}</p>
            </div>
          </div>
        </div>
      </header>
      <div className="container wide stack" style={{ paddingBlock: "16px", "--gap": "16px" }}>
        {s.mcp.available ? <ConnectCard /> : null}
        <div className="grid-cards">
          {state.status === "loading" ? <Skeletons n={3} height={280} />
            : state.status === "error" ? <ErrorBox error={state.error} onRetry={reload} />
              : state.data.length ? state.data.map((o) => <OfficeCard key={o.id} office={o} />)
                : <Empty icon="search" text={t("noOfficesFound")} />}
        </div>
      </div>
    </>
  );
}

const CONNECT_CARD_KEY = "connectCardHidden";

const cardHidden = () => { try { return localStorage.getItem(CONNECT_CARD_KEY) === "1"; } catch { return false; } };

/** Plan 012: invites the person to connect their AI assistant, until they have one or hide the card. */
function ConnectCard() {
  const [hidden, setHidden] = useState(cardHidden);
  const { state } = useAsync(() => (hidden ? Promise.resolve([]) : get<ConnectedApp[]>("/api/me/connected-apps")), hidden);
  if (hidden || state.status !== "done" || state.data.length) return null;
  const hide = () => {
    try { localStorage.setItem(CONNECT_CARD_KEY, "1"); } catch { /* private mode: hidden for this visit only */ }
    setHidden(true);
  };
  return (
    <section className="card pad connect-card">
      <Icon name="sparkle" className="lg" />
      <div className="stack grow" style={{ "--gap": "6px" }}>
        <h2>{t("connectHomeTitle")}</h2>
        <p className="small muted">{t("connectHomeText")}</p>
        <div className="row wrap" style={{ "--gap": "8px" }}>
          <Link className="btn sm" href="/ConnectAssistant">{t("connectAnother")}</Link>
          <button className="btn outline sm" type="button" onClick={hide}>{t("connectHomeDismiss")}</button>
        </div>
      </div>
    </section>
  );
}

function OfficeCard({ office: o }: { office: OfficeDto }) {
  return (
    <article className="card office-card">
      <div className="cover">
        <img src={o.imageUrl || FALLBACK_IMG} alt="" loading="lazy" />
        <span className="price-tag"><bdi>{money(o.hourlyRate, o.currency)}</bdi> <span className="xs muted">{t("perHour")}</span></span>
      </div>
      <div className="stack" style={{ padding: "16px", "--gap": "10px" }}>
        <h3>{o.name}</h3>
        <div className="meta">
          <span><Icon name="users" className="sm" />{`${o.capacity} ${t("people")}`}</span>
          {o.location ? <span><Icon name="pin" className="sm" />{o.location}</span> : null}
        </div>
        {o.amenities.length ? (
          <div className="row wrap" style={{ "--gap": "6px" }}>
            {o.amenities.slice(0, 3).map((a) => <span key={a} className="badge"><Icon name={amenityIcon(a)} className="sm" />{a}</span>)}
          </div>
        ) : null}
        <Link className="btn block" href={`/BookOffice?id=${encodeURIComponent(o.id)}`}>{t("bookNow")}</Link>
      </div>
    </article>
  );
}
