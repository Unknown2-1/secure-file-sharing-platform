"use client";

import { FormEvent, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";
import { translate } from "@/lib/i18n/messages";

export function AcceptInvitationForm() {
  const [invitationId, setInvitationId] = useState("");
  const [secretToken, setSecretToken] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (loading) return;
    setLoading(true);
    setMessage(null);
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/workspace-invitations/accept`, {
        method: "POST", credentials: "include",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
        body: JSON.stringify({ invitationId, secretToken }),
      });
      setSecretToken("");
      setMessage(response.ok ? "Undangan diterima. Workspace tersedia di dashboard." : "Undangan tidak dapat diverifikasi atau sudah kedaluwarsa.");
    } catch {
      setMessage("Undangan tidak dapat diproses. Periksa koneksi dan coba lagi.");
    } finally {
      setLoading(false);
    }
  }
  return <form className="rounded-2xl border border-slate-200 bg-white p-6" aria-busy={loading} onSubmit={(event) => void submit(event)}><h1 className="text-2xl font-bold">Terima undangan</h1><p className="mt-2 text-sm text-slate-600">Masukkan ID dan secret secara manual. Secret tidak ditaruh di URL.</p><label className="mt-5 block text-sm font-semibold" htmlFor="invitation-id">Invitation ID</label><input id="invitation-id" className="form-input" value={invitationId} onChange={(event) => setInvitationId(event.target.value)} required /><label className="mt-4 block text-sm font-semibold" htmlFor="invitation-secret">Secret undangan</label><input id="invitation-secret" type="password" className="form-input" value={secretToken} onChange={(event) => setSecretToken(event.target.value)} required /><button className="button-primary mt-5 disabled:opacity-60" type="submit" disabled={loading}>{loading ? translate("id", "common.processing") : "Terima"}</button>{message && <p className="mt-4 text-sm" role="status" aria-live="polite">{message}</p>}</form>;
}
