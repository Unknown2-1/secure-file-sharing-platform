"use client";
import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";
import { translate } from "@/lib/i18n/messages";
export function TwoFactorForm() {
  const router = useRouter();
  const [code, setCode] = useState("");
  const [recovery, setRecovery] = useState(false);
  const [error, setError] = useState(false);
  const [loading, setLoading] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (loading) return;
    setError(false);
    setLoading(true);
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/auth/login/two-factor`, { method: "POST", credentials: "include", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf }, body: JSON.stringify(recovery ? { code: null, recoveryCode: code } : { code, recoveryCode: null }) });
      if (!response.ok) { setError(true); return; }
      router.push("/dashboard");
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }
  return <form className="auth-card" aria-busy={loading} onSubmit={(event) => void submit(event)}><h1 className="text-2xl font-bold">Verifikasi dua langkah</h1><p className="mt-2 text-sm text-slate-600">{recovery ? "Masukkan satu recovery code yang belum digunakan." : "Masukkan kode 6 digit dari aplikasi authenticator."}</p><label htmlFor="two-factor" className="mt-5 block text-sm font-semibold">{recovery ? "Recovery code" : "Kode autentikasi"}</label><input id="two-factor" className="form-input" value={code} onChange={(event) => setCode(event.target.value)} autoComplete="one-time-code" required maxLength={64} />{error && <p role="alert" className="mt-3 text-sm text-red-700">Kode tidak dapat diverifikasi.</p>}<button className="button-primary mt-5 w-full disabled:opacity-60" type="submit" disabled={loading}>{loading ? translate("id", "common.processing") : "Verifikasi"}</button><button type="button" className="button-quiet mt-2 w-full" disabled={loading} onClick={() => { setRecovery(!recovery); setCode(""); }}>{recovery ? "Gunakan authenticator" : "Gunakan recovery code"}</button></form>;
}
