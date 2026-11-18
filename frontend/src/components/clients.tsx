// The user's private client list: the add/edit sheet and the client picker, shared by
// /MyClients, /BookOffice and /MyBookings.
import { useState, type FormEvent } from "react";
import { del, post, put } from "../api";
import { t, type TranslationKey } from "../i18n";
import { Link } from "../router";
import type { ClientDto, ClientRequest } from "../types/api";
import { Field, confirmSheet, errorText, openSheet, toast } from "../ui";

export const clientName = (c: Pick<ClientDto, "firstName" | "lastName">) => `${c.firstName} ${c.lastName}`.trim();

export const sortClients = (list: ClientDto[]) =>
  [...list].sort((a, b) => a.lastName.localeCompare(b.lastName) || a.firstName.localeCompare(b.firstName));

const FORM_ID = "client-form";

/** An input plus an "add another" link that reveals a second input (phones, emails). */
function Pair({ type, values, onChange, labels, inputProps }: {
  type: "tel" | "email";
  values: [string, string];
  onChange: (v: [string, string]) => void;
  labels: [TranslationKey, TranslationKey, TranslationKey];
  inputProps: Record<string, string | number>;
}) {
  const [showSecond, setShowSecond] = useState(!!values[1]);
  const [first, second] = values;
  return (
    <>
      <Field label={t(labels[0])}>
        <input className="input" type={type} value={first} onChange={(e) => onChange([e.target.value, second])} {...inputProps} />
      </Field>
      {showSecond ? (
        <Field label={t(labels[1])}>
          <input className="input" type={type} value={second} autoFocus={!second} onChange={(e) => onChange([first, e.target.value])} {...inputProps} />
        </Field>
      ) : null}
      <div>
        {showSecond ? null : <button className="btn ghost sm" type="button" onClick={() => setShowSecond(true)}>{t(labels[2])}</button>}
      </div>
    </>
  );
}

function ClientForm({ client, onSaved, close, bookingsLink }: {
  client: ClientDto | null; onSaved?: (c: ClientDto) => void; close: () => void; bookingsLink: boolean;
}) {
  const [firstName, setFirstName] = useState(client?.firstName ?? "");
  const [lastName, setLastName] = useState(client?.lastName ?? "");
  const [phones, setPhones] = useState<[string, string]>([client?.phones[0] ?? "", client?.phones[1] ?? ""]);
  const [emails, setEmails] = useState<[string, string]>([client?.emails[0] ?? "", client?.emails[1] ?? ""]);
  const [language, setLanguage] = useState(client?.language ?? "he");
  const [consent, setConsent] = useState(client?.whatsAppConsent ?? false);
  const [notes, setNotes] = useState(client?.notes ?? "");
  const [error, setError] = useState("");

  const clean = (v: string[]) => v.map((x) => x.trim()).filter(Boolean);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    const body: ClientRequest = {
      firstName: firstName.trim(), lastName: lastName.trim(), phones: clean(phones), emails: clean(emails),
      language, whatsAppConsent: consent, notes: notes.trim(),
    };
    try {
      const saved = client ? await put<ClientDto>(`/api/clients/${client.clientId}`, body) : await post<ClientDto>("/api/clients", body);
      toast(t("clientSaved"), "success");
      close();
      onSaved?.(saved);
    } catch (ex) { setError(errorText(ex)); }
  };

  return (
    <form id={FORM_ID} className="stack" style={{ "--gap": "14px" }} onSubmit={submit}>
      <div className="row" style={{ "--gap": "10px", alignItems: "flex-start" }}>
        <div className="grow"><Field label={t("firstName")}>
          <input className="input" required maxLength={60} autoComplete="off" value={firstName} onChange={(e) => setFirstName(e.target.value)} />
        </Field></div>
        <div className="grow"><Field label={t("lastName")}>
          <input className="input" maxLength={60} autoComplete="off" value={lastName} onChange={(e) => setLastName(e.target.value)} />
        </Field></div>
      </div>
      <Pair type="tel" values={phones} onChange={setPhones} labels={["phoneNumber", "secondPhone", "addPhone"]}
        inputProps={{ dir: "ltr", inputMode: "tel", autoComplete: "off", maxLength: 30 }} />
      <Pair type="email" values={emails} onChange={setEmails} labels={["email", "secondEmail", "addEmail"]}
        inputProps={{ dir: "ltr", autoComplete: "off", maxLength: 200 }} />
      <Field label={t("clientLanguage")}>
        <select className="select" value={language} onChange={(e) => setLanguage(e.target.value === "en" ? "en" : "he")}>
          <option value="he">{t("langHe")}</option>
          <option value="en">{t("langEn")}</option>
        </select>
      </Field>
      <div>
        <label className="switch"><input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} />{t("whatsAppConsent")}</label>
        <p className="xs muted">{t("whatsAppConsentHint")}</p>
      </div>
      <Field label={t("clientNotes")} hint={t("clientNotesHint")}>
        <textarea className="textarea" maxLength={500} rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
      </Field>
      {client && bookingsLink ? (
        <Link className="btn ghost sm" href={`/MyBookings?clientId=${encodeURIComponent(client.clientId)}`} onClick={() => close()}>
          {t("bookingsWithClient")}
        </Link>
      ) : null}
      <p className="error-text" role="alert">{error}</p>
    </form>
  );
}

/**
 * Opens the add/edit sheet. `client` is null for a new client.
 * onSaved(client) after a save, onDeleted(client) after a delete.
 * With `bookingsLink`, an existing client gets a "Bookings with this client" link.
 */
export function openClientSheet({ client = null, onSaved, onDeleted, bookingsLink = false }: {
  client?: ClientDto | null; onSaved?: (c: ClientDto) => void; onDeleted?: (c: ClientDto) => void; bookingsLink?: boolean;
} = {}) {
  async function remove(c: ClientDto) {
    const ok = await confirmSheet({
      title: t("deleteClient"), message: `${t("deleteClientConfirm")} ${clientName(c)}?`,
      confirmText: t("delete"), danger: true,
    });
    if (!ok) return;
    try {
      await del(`/api/clients/${c.clientId}`);
      toast(t("clientDeleted"), "success");
      sheet.close();
      onDeleted?.(c);
    } catch (ex) { toast(errorText(ex), "error"); }
  }

  const sheet = openSheet({
    title: client ? t("editClient") : t("addClient"),
    body: ({ close }) => <ClientForm client={client} onSaved={onSaved} close={close} bookingsLink={bookingsLink} />,
    actions: (close) => [
      client ? <button key="del" className="btn danger-outline" type="button" onClick={() => remove(client)}>{t("delete")}</button> : null,
      <button key="cancel" className="btn outline" type="button" onClick={close}>{t("cancel")}</button>,
      <button key="save" className="btn" type="submit" form={FORM_ID}>{t("saveClient")}</button>,
    ],
  });
  return sheet;
}

/**
 * A <select> of the user's clients: "No client", each client, and "+ New client", which opens the
 * client sheet and selects the new client (reported through onAdded so the caller can keep its list).
 */
export function ClientSelect({ clients, value, onChange, onAdded, id }: {
  clients: ClientDto[]; value: string; onChange: (clientId: string) => void; onAdded?: (c: ClientDto) => void; id?: string;
}) {
  const NEW = "__new";
  const [extra, setExtra] = useState<ClientDto[]>([]);
  const all = sortClients([...clients, ...extra.filter((x) => !clients.some((c) => c.clientId === x.clientId))]);
  return (
    <select id={id} className="select" value={value}
      onChange={(e) => {
        if (e.target.value !== NEW) { onChange(e.target.value); return; }
        openClientSheet({
          onSaved: (saved) => {
            setExtra((x) => [...x, saved]);
            onAdded?.(saved);
            onChange(saved.clientId);
          },
        });
      }}>
      <option value="">{t("noClient")}</option>
      {all.map((c) => <option key={c.clientId} value={c.clientId}>{clientName(c)}</option>)}
      <option value={NEW}>{t("newClient")}</option>
    </select>
  );
}
