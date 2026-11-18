import { useState, type FormEvent } from "react";
import { del, get, post, put } from "../api";
import { t } from "../i18n";
import { fmtShortDate, me } from "../store";
import type { BookingDto, Role, UserDto } from "../types/api";
import { useAsync } from "../useAsync";
import { BookingCard } from "../components/bookingCard";
import { Empty, ErrorBox, Field, Icon, PageHeader, Skeletons, confirmSheet, errorText, openSheet, toast, type IconName } from "../ui";

export default function AdminUsers() {
  const { state, reload } = useAsync(() => get<UserDto[]>("/api/admin/users"));
  const meId = me().user.id;
  const users = state.status === "done" ? state.data : null;

  function invite() {
    openSheet({
      title: t("inviteNewUser"),
      body: ({ close }) => <InviteForm onSaved={() => { close(); reload(); }} />,
      actions: (close) => [
        <button key="cancel" className="btn outline" type="button" onClick={close}>{t("cancel")}</button>,
        <button key="save" className="btn" type="submit" form="invite-form">{t("sendInvite")}</button>,
      ],
    });
  }

  function edit(u: UserDto) {
    openSheet({
      title: t("editUser"),
      body: ({ close }) => <EditUserForm user={u} isMe={u.id === meId} onSaved={() => { close(); reload(); }} />,
      actions: (close) => [
        <button key="cancel" className="btn outline" type="button" onClick={close}>{t("cancel")}</button>,
        <button key="save" className="btn" type="submit" form="edit-user">{t("update")}</button>,
      ],
    });
  }

  async function remove(u: UserDto) {
    if (!await confirmSheet({ title: t("deleteUser"), message: `${t("deleteUserConfirm")} ${u.fullName || u.email}? ${t("willRemoveBookings")}`, confirmText: t("delete"), danger: true })) return;
    try { await del(`/api/admin/users/${u.id}`); reload(); } catch (e) { toast(errorText(e), "error"); }
  }

  function showBookings(u: UserDto) {
    openSheet({ title: `${t("userBookings")} ${u.fullName || u.email}`, body: <UserBookings userId={u.id} /> });
  }

  const section = (icon: IconName, title: string, list: UserDto[]) => (
    <section className="stack">
      <h2 className="section-title"><Icon name={icon} className="sm" />{title}</h2>
      {list.length ? (
        <div className="grid-cards">
          {list.map((u) => (
            <UserCard key={u.id} user={u} isMe={u.id === meId} onBookings={() => showBookings(u)} onEdit={() => edit(u)}
              onRole={reload} onDelete={() => remove(u)} />
          ))}
        </div>
      ) : <Empty icon="users" text={t("noUsersYet")} />}
    </section>
  );

  const admins = users?.filter((u) => u.role === "admin") ?? [];
  const regular = users?.filter((u) => u.role !== "admin") ?? [];
  return (
    <>
      <PageHeader title={t("manageUsers")}
        subtitle={users ? `${users.length} ${users.length === 1 ? t("totalUsers") : t("totalUsersPlural")}` : undefined} />
      <div className="container wide stack" style={{ paddingBlock: "16px", "--gap": "16px" }}>
        <button className="btn block" type="button" onClick={invite}><Icon name="plus" />{t("inviteUser")}</button>
        <div className="stack" style={{ "--gap": "20px" }}>
          {state.status === "loading" ? <Skeletons n={3} height={120} />
            : state.status === "error" ? <ErrorBox error={state.error} onRetry={reload} />
              : <>
                {section("shield", `${t("administrators")} (${admins.length})`, admins)}
                {section("users", `${t("users")} (${regular.length})`, regular)}
              </>}
        </div>
      </div>
    </>
  );
}

function UserCard({ user: u, isMe, onBookings, onEdit, onRole, onDelete }: {
  user: UserDto; isMe: boolean; onBookings: () => void; onEdit: () => void; onRole: () => void; onDelete: () => void;
}) {
  const [updatingRole, setUpdatingRole] = useState(false);
  const toggleRole = async () => {
    setUpdatingRole(true);
    try { await put(`/api/admin/users/${u.id}`, { role: u.role === "admin" ? "user" : "admin" }); }
    catch (e) { toast(errorText(e), "error"); }
    onRole();
  };
  return (
    <article className="card pad stack" style={{ "--gap": "10px" }}>
      <div className="list-item">
        <div className="avatar">{u.pictureUrl ? <img src={u.pictureUrl} alt="" /> : <Icon name={u.role === "admin" ? "shield" : "user"} />}</div>
        <div className="grow stack" style={{ "--gap": "2px" }}>
          <div className="row" style={{ "--gap": "6px" }}>
            <h3 className="truncate">{u.fullName || u.email}</h3>
            {u.role === "admin" ? <span className="badge primary">{t("admin")}</span> : null}
            {u.status === "invited" ? <span className="badge warning">{t("invited")}</span> : null}
          </div>
          <p className="small muted truncate" dir="ltr" style={{ textAlign: "start" }}>{u.email}</p>
          <p className="xs muted">{`${t("joined")} ${fmtShortDate(u.createdAt)}`}</p>
          {u.termsAccepted
            ? <span className="xs" style={{ color: "var(--success-fg)" }}><Icon name="checkCircle" className="sm" />{` ${t("termsSigned")}${u.termsAcceptedAt ? ` · ${fmtShortDate(u.termsAcceptedAt)}` : ""}`}</span>
            : <span className="xs faint"><Icon name="file" className="sm" />{` ${t("termsNotSigned")}`}</span>}
        </div>
      </div>
      <div className="row wrap" style={{ justifyContent: "flex-end", "--gap": "6px" }}>
        <button className="btn outline sm" type="button" onClick={onBookings}><Icon name="calendar" className="sm" />{t("bookings")}</button>
        <button className="btn outline sm" type="button" aria-label={t("edit")} onClick={onEdit}><Icon name="edit" className="sm" /></button>
        {!isMe ? (
          <button className="btn outline sm" type="button" disabled={updatingRole} onClick={toggleRole}>
            {updatingRole ? t("updatingRole") : u.role === "admin" ? t("setAsUser") : t("setAsAdmin")}
          </button>
        ) : null}
        {!isMe ? (
          <button className="btn danger-outline sm" type="button" aria-label={t("delete")} onClick={onDelete}><Icon name="trash" className="sm" /></button>
        ) : null}
      </div>
    </article>
  );
}

function RoleSelect({ value, onChange, disabled, id }: { value: Role; onChange: (r: Role) => void; disabled?: boolean; id?: string }) {
  return (
    <select id={id} className="select" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value === "admin" ? "admin" : "user")}>
      <option value="user">{t("user")}</option>
      <option value="admin">{t("admin")}</option>
    </select>
  );
}

function InviteForm({ onSaved }: { onSaved: () => void }) {
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<Role>("user");
  const [error, setError] = useState("");
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    try { await post("/api/admin/users/invite", { email: email.trim(), role }); onSaved(); }
    catch (ex) { setError(errorText(ex)); }
  };
  return (
    <form id="invite-form" className="stack" onSubmit={submit}>
      <Field label={t("emailAddress")}>
        <input className="input" type="email" required placeholder="user@example.com" autoComplete="off" dir="ltr" value={email} onChange={(e) => setEmail(e.target.value)} />
      </Field>
      <Field label={t("role")}><RoleSelect value={role} onChange={setRole} /></Field>
      <p className="error-text" role="alert">{error}</p>
    </form>
  );
}

function EditUserForm({ user: u, isMe, onSaved }: { user: UserDto; isMe: boolean; onSaved: () => void }) {
  const [name, setName] = useState(u.fullName);
  const [email, setEmail] = useState(u.email);
  const [phone, setPhone] = useState(u.phoneNumber || "");
  const [role, setRole] = useState<Role>(u.role);
  const [error, setError] = useState("");
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    try {
      await put(`/api/admin/users/${u.id}`, { fullName: name, email, phoneNumber: phone, role });
      onSaved();
    } catch (ex) { setError(errorText(ex)); }
  };
  return (
    <form id="edit-user" className="stack" onSubmit={submit}>
      <Field label={t("fullName")}><input className="input" required value={name} onChange={(e) => setName(e.target.value)} /></Field>
      <Field label={t("emailAddress")}><input className="input" type="email" required dir="ltr" value={email} onChange={(e) => setEmail(e.target.value)} /></Field>
      <Field label={t("phoneNumber")}><input className="input" type="tel" inputMode="tel" dir="ltr" value={phone} onChange={(e) => setPhone(e.target.value)} /></Field>
      <Field label={t("role")}><RoleSelect value={role} onChange={setRole} disabled={isMe} /></Field>
      <p className="error-text" role="alert">{error}</p>
    </form>
  );
}

function UserBookings({ userId }: { userId: string }) {
  const { state } = useAsync(() => get<BookingDto[]>(`/api/admin/users/${userId}/bookings`));
  return (
    <div className="stack">
      {state.status === "loading" ? <Skeletons n={2} height={100} />
        : state.status === "error" ? <ErrorBox error={state.error} />
          : state.data.length ? state.data.map((b) => <BookingCard key={b.id} booking={b} />)
            : <Empty icon="calendar" text={t("noBookings")} />}
    </div>
  );
}
