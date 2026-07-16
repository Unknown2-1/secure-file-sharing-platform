"use client";

import { FormEvent, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";
import { translate } from "@/lib/i18n/messages";

export function AccountCodeForm({ mode }: { mode: "forgot" | "verify" | "reset" }) {
  const [email, setEmail] = useState("");
  const [token, setToken] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (loading) return;
    setLoading(true);
    setMessage(null);
    try {
      const csrf = await getCsrfToken();
      const path = mode === "forgot" ? "forgot-password" : mode === "verify" ? "verify-email" : "reset-password";
      const body = mode === "forgot" ? { email } : mode === "verify" ? { email, token } : { email, token, newPassword: password };
      const response = await fetch(`${apiBaseUrl}/api/v1/auth/${path}`, {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
        body: JSON.stringify(body),
      });
      setMessage(response.ok || response.status === 202
        ? mode === "forgot" ? "Jika akun memenuhi syarat, kode reset telah dikirim." : "Permintaan berhasil diproses. Anda dapat kembali ke halaman masuk."
        : "Kode atau data tidak dapat diverifikasi.");
    } catch {
      setMessage("Permintaan tidak dapat diproses. Periksa koneksi dan coba lagi.");
    } finally {
      setLoading(false);
    }
  }

  return <form className="auth-card" aria-busy={loading} onSubmit={(event) => void submit(event)}><h1 className="text-2xl font-bold">{mode === "forgot" ? "Lupa password" : mode === "verify" ? "Verifikasi email" : "Reset password"}</h1><Field id="recovery-email" label="Email" value={email} set={setEmail} type="email" />{mode !== "forgot" && <Field id="account-token" label="Kode" value={token} set={setToken} />}{mode === "reset" && <Field id="new-password" label="Password baru" value={password} set={setPassword} type="password" />}<button className="button-primary mt-5 w-full disabled:opacity-60" type="submit" disabled={loading}>{loading ? translate("id", "common.processing") : "Kirim"}</button>{message && <p role="status" aria-live="polite" className="mt-4 text-sm leading-6">{message}</p>}</form>;
}

function Field({ id, label, value, set, type = "text" }: { id: string; label: string; value: string; set: (value: string) => void; type?: string }) { return <div className="mt-5"><label className="text-sm font-semibold" htmlFor={id}>{label}</label><input id={id} className="form-input" type={type} value={value} onChange={(event) => set(event.target.value)} required maxLength={4096} /></div>; }
