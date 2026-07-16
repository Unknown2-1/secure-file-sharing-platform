"use client";

import Link from "next/link";
import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";

export function AuthForm({ mode }: { mode: "login" | "register" }) {
  const router = useRouter();
  const [email, setEmail] = useState(""); const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState(""); const [message, setMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault(); setLoading(true); setMessage(null);
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/auth/${mode}`, {
        method: "POST", credentials: "include", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
        body: JSON.stringify(mode === "login" ? { email, password } : { email, password, displayName }),
      });
      if (mode === "login" && response.status === 202) { router.push("/login/two-factor"); return; }
      if (!response.ok) throw new Error("auth_failed");
      if (mode === "register") { setPassword(""); setMessage("Akun dibuat. Buka Mailpit atau email Anda, lalu masukkan kode pada halaman verifikasi."); return; }
      router.push("/dashboard"); router.refresh();
    } catch { setMessage(mode === "login" ? "Email atau password tidak dapat diverifikasi." : "Pendaftaran gagal. Periksa data dan coba lagi."); }
    finally { setLoading(false); }
  }
  return <form onSubmit={(event) => void submit(event)} className="auth-card">
    <p className="section-label">VAULTSHARE</p><h1 className="mt-2 text-2xl font-bold">{mode === "login" ? "Masuk ke akun" : "Buat akun"}</h1>
    {mode === "register" && <Field id="display-name" label="Nama tampilan" value={displayName} onChange={setDisplayName} autoComplete="name" />}
    <Field id="email" label="Email" value={email} onChange={setEmail} type="email" autoComplete="email" />
    <Field id="password" label="Password" value={password} onChange={setPassword} type="password" autoComplete={mode === "login" ? "current-password" : "new-password"} minLength={12} />
    {message && <p role="status" className="mt-4 text-sm leading-6 text-slate-700">{message}</p>}
    <button className="button-primary mt-5 w-full disabled:opacity-60" type="submit" disabled={loading}>{loading ? "Memproses…" : mode === "login" ? "Masuk" : "Daftar"}</button>
    <div className="mt-5 flex justify-between text-sm"><Link className="text-blue-800 underline" href={mode === "login" ? "/register" : "/login"}>{mode === "login" ? "Buat akun" : "Sudah punya akun"}</Link>{mode === "login" && <Link className="text-blue-800 underline" href="/forgot-password">Lupa password</Link>}</div>
  </form>;
}

function Field({ id, label, value, onChange, type = "text", autoComplete, minLength }: { id: string; label: string; value: string; onChange: (value: string) => void; type?: string; autoComplete: string; minLength?: number }) {
  return <div className="mt-5"><label className="text-sm font-semibold" htmlFor={id}>{label}</label><input className="form-input" id={id} type={type} value={value} onChange={(event) => onChange(event.target.value)} autoComplete={autoComplete} minLength={minLength} maxLength={254} required /></div>;
}
