"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";
import { translate } from "@/lib/i18n/messages";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/async-state";

type Member = { userId: string; email: string; displayName: string; role: string; joinedAt: string };

export function WorkspaceMembers({ workspaceId }: { workspaceId: string }) {
  const [members, setMembers] = useState<Member[]>([]);
  const [email, setEmail] = useState("");
  const [role, setRole] = useState("Member");
  const [invitation, setInvitation] = useState<{ id: string; secretToken: string; expiresAt: string } | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [pendingRemoval, setPendingRemoval] = useState<Member | null>(null);
  const [removing, setRemoving] = useState(false);
  const [loadingMembers, setLoadingMembers] = useState(true);
  const [memberLoadError, setMemberLoadError] = useState(false);
  const load = useCallback(async () => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/workspaces/${workspaceId}/members`, { credentials: "include" });
      if (!response.ok) { setMemberLoadError(true); return; }
      setMembers(await response.json() as Member[]);
      setMemberLoadError(false);
    } catch {
      setMemberLoadError(true);
    } finally {
      setLoadingMembers(false);
    }
  }, [workspaceId]);
  useEffect(() => {
    let active = true;
    void fetch(`${apiBaseUrl}/api/v1/workspaces/${workspaceId}/members`, { credentials: "include" })
      .then(async response => {
        if (!active) return;
        if (!response.ok) { setMemberLoadError(true); return; }
        setMembers(await response.json() as Member[]);
        setMemberLoadError(false);
      })
      .catch(() => { if (active) setMemberLoadError(true); })
      .finally(() => { if (active) setLoadingMembers(false); });
    return () => { active = false; };
  }, [workspaceId]);

  async function invite(event: FormEvent) {
    event.preventDefault();
    const csrf = await getCsrfToken();
    const response = await fetch(`${apiBaseUrl}/api/v1/workspaces/${workspaceId}/invitations`, {
      method: "POST", credentials: "include",
      headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
      body: JSON.stringify({ email, role, expiresInHours: 24 }),
    });
    if (!response.ok) { setMessage("Undangan tidak dapat dibuat untuk role tersebut."); return; }
    setInvitation(await response.json() as { id: string; secretToken: string; expiresAt: string });
    setEmail("");
    setMessage("Undangan dibuat. Secret hanya ditampilkan sekali.");
  }

  async function changeRole(userId: string, nextRole: string) {
    const csrf = await getCsrfToken();
    const response = await fetch(`${apiBaseUrl}/api/v1/workspaces/${workspaceId}/members/${userId}/role`, {
      method: "PATCH", credentials: "include",
      headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
      body: JSON.stringify({ role: nextRole }),
    });
    if (response.ok) await load(); else setMessage("Role tidak dapat diubah oleh akun ini.");
  }

  async function remove() {
    if (!pendingRemoval || removing) return;
    setRemoving(true);
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/workspaces/${workspaceId}/members/${pendingRemoval.userId}`, {
        method: "DELETE", credentials: "include", headers: { "X-CSRF-TOKEN": csrf },
      });
      if (response.ok) {
        setPendingRemoval(null);
        await load();
      } else {
        setMessage("Anggota tidak dapat dihapus oleh akun ini.");
      }
    } catch {
      setMessage("Penghapusan anggota tidak dapat diproses. Periksa koneksi dan coba lagi.");
    } finally {
      setRemoving(false);
    }
  }

  return <div className="space-y-6">
    <form className="rounded-2xl border border-slate-200 bg-white p-6" onSubmit={(event) => void invite(event)}>
      <h1 className="text-2xl font-bold">Anggota workspace</h1>
      <div className="mt-5 grid gap-3 sm:grid-cols-[1fr_auto_auto]">
        <label className="sr-only" htmlFor="invite-email">Email penerima</label><input id="invite-email" className="form-input mt-0" type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="anggota@example.com" required />
        <label className="sr-only" htmlFor="invite-role">Role</label><select id="invite-role" className="form-input mt-0" value={role} onChange={(event) => setRole(event.target.value)}><option>Admin</option><option>Member</option><option>Viewer</option></select>
        <button className="button-primary" type="submit">Undang</button>
      </div>
      {message && <p className="mt-3 text-sm" role="status">{message}</p>}
      {invitation && <div className="mt-4 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm"><p className="font-bold">Simpan dan kirim melalui kanal tepercaya</p><p className="mt-2">Invitation ID: <code className="break-all">{invitation.id}</code></p><p className="mt-2">Secret: <code className="break-all">{invitation.secretToken}</code></p><p className="mt-2">Jangan memasukkan secret ke URL atau log. Penerima memasukkannya di halaman Terima undangan.</p></div>}
    </form>
    {pendingRemoval && <section className="rounded-2xl border border-red-200 bg-red-50 p-5" role="alertdialog" aria-labelledby="remove-member-title" aria-describedby="remove-member-description" aria-modal="false"><h2 id="remove-member-title" className="font-bold text-red-950">{translate("id", "workspace.members.remove.title")}</h2><p id="remove-member-description" className="mt-2 text-sm leading-6 text-red-900">{translate("id", "workspace.members.remove.description")} <strong>{pendingRemoval.displayName}</strong> ({pendingRemoval.email}).</p><div className="mt-4 flex flex-wrap gap-3"><button className="button-secondary" type="button" disabled={removing} onClick={() => setPendingRemoval(null)}>{translate("id", "workspace.members.remove.cancel")}</button><button className="button-primary bg-red-700 disabled:opacity-60" type="button" disabled={removing} onClick={() => void remove()}>{removing ? translate("id", "common.processing") : translate("id", "workspace.members.remove.confirm")}</button></div></section>}
    {loadingMembers && <LoadingState title="Memuat anggota…" description="Keanggotaan dan role workspace sedang dimuat." />}
    {!loadingMembers && memberLoadError && <ErrorState title="Anggota tidak dapat dimuat" onRetry={() => { setLoadingMembers(true); void load(); }} />}
    {!loadingMembers && !memberLoadError && members.length === 0 && <EmptyState title="Belum ada anggota" description={translate("id", "workspace.members.empty")} />}
    {!loadingMembers && !memberLoadError && members.length > 0 && <ul className="divide-y divide-slate-200 overflow-hidden rounded-2xl border border-slate-200 bg-white">{members.map(member => <li className="flex flex-wrap items-center justify-between gap-3 p-4" key={member.userId}><div><p className="font-semibold">{member.displayName}</p><p className="text-sm text-slate-500">{member.email}</p></div><div className="flex items-center gap-2">{member.role === "Owner" ? <span className="text-sm font-bold">Owner</span> : <><select aria-label={`Role ${member.email}`} className="min-h-11 rounded-lg border border-slate-300 p-2 text-sm" value={member.role} onChange={(event) => void changeRole(member.userId, event.target.value)}><option>Admin</option><option>Member</option><option>Viewer</option></select><button className="button-quiet text-sm text-red-700 underline" type="button" onClick={() => setPendingRemoval(member)}>Hapus</button></>}</div></li>)}</ul>}
  </div>;
}
