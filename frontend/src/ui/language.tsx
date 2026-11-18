import { getLang, setLang, t } from "../i18n";

/** Switches between Hebrew and English; shows the language you switch to. */
export function LanguageButton({ className = "" }: { className?: string }) {
  const next = getLang() === "he" ? "en" : "he";
  const name = next === "he" ? "עברית" : "English";
  return (
    <button className={`icon-btn ${className}`.trim()} type="button" title={name} aria-label={`${t("language")}: ${name}`}
      onClick={() => setLang(next)}>
      <span className="xs bold">{next === "he" ? "עב" : "EN"}</span>
    </button>
  );
}
