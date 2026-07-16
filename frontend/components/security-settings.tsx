"use client";

import { useCallback, useEffect, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/async-state";

type Session = { id: string; createdAt: string; lastSeenAt: string; expiresAt: string; revokedAt?: string; userAgent: string; isCurrent: boolean };

export function SecuritySettings() {
  const [sessions, setSessions] = useState<Session[]>([]);
  const [setup, setSetup] = useState<{ sharedKey: string; authenticatorUri: string } | null>(null);
  const [code, setCode] = useState("");
  const [recovery, setRecovery] = useState<string[]>([]);
  const [loadingSessions, setLoadingSessions] = useState(true);
  const [sessionError, setSessionError] = useState(false);
  const [operation, setOperation] = useState<"setup" | "enable" | "revoke" | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const loadSessions = useCallback(async () => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/sessions`, { credentials: "include" });
      if (!response.ok) { setSessionError(true); return; }
      setSessions(await response.json() as Session[]);
      setSessionError(false);
    } catch {
      setSessionError(true);
    } finally {
      setLoadingSessions(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    void fetch(`${apiBaseUrl}/api/v1/sessions`, { credentials: "include" })
      .then(async response => {
        if (!active) return;
        if (!response.ok) { setSessionError(true); return; }
        setSessions(await response.json() as Session[]);
        setSessionError(false);
      })
      .catch(() => { if (active) setSessionError(true); })
      .finally(() => { if (active) setLoadingSessions(false); });
    return () => { active = false; };
  }, []);

  async function beginTotp() {
    if (operation) return;
    setOperation("setup");
    setMessage(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/security/two-factor/setup`, { credentials: "include" });
      if (!response.ok) { setMessage("Penyiapan TOTP tidak dapat dimulai."); return; }
      setSetup(await response.json() as { sharedKey: string; authenticatorUri: string });
    } catch {
      setMessage("Penyiapan TOTP tidak dapat diproses. Periksa koneksi dan coba lagi.");
    } finally {
      setOperation(null);
    }
  }

  async function enable() {
    if (operation) return;
    setOperation("enable");
    setMessage(null);
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/security/two-factor/enable`, { method: "POST", credentials: "include", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf }, body: JSON.stringify({ code }) });
      if (!response.ok) { setMessage("Kode TOTP tidak dapat diverifikasi."); return; }
      const body = await response.json() as { recoveryCodes: string[] };
      setRecovery(body.recoveryCodes);
      setSetup(null);
      setCode("");
    } catch {
      setMessage("TOTP tidak dapat diaktifkan. Periksa koneksi dan coba lagi.");
    } finally {
      setOperation(null);
    }
  }

  async function revoke(id: string) {
    if (operation) return;
    setOperation("revoke");
    setMessage(null);
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/sessions/${id}`, { method: "DELETE", credentials: "include", headers: { "X-CSRF-TOKEN": csrf } });
      if (!response.ok) { setMessage("Sesi tidak dapat dicabut."); return; }
      await loadSessions();
    } catch {
      setMessage("Sesi tidak dapat dicabut. Periksa koneksi dan coba lagi.");
    } finally {
      setOperation(null);
    }
  }

  return <div className="grid gap-6 lg:grid-cols-2"><section className="rounded-2xl border border-slate-200 bg-white p-6"><h2 className="text-xl font-bold">Autentikasi dua langkah</h2>{message && <p className="mt-3 text-sm text-red-700" role="alert">{message}</p>}{!setup && recovery.length === 0 && <button className="button-primary mt-5 disabled:opacity-60" type="button" disabled={operation !== null} onClick={() => void beginTotp()}>{operation === "setup" ? "Memproses…" : "Siapkan TOTP"}</button>}{setup && <div className="mt-4"><p className="text-sm">Masukkan key ini ke aplikasi authenticator. Jangan bagikan key.</p><code className="mt-3 block break-all rounded-lg bg-slate-100 p-3">{setup.sharedKey}</code><label htmlFor="totp-code" className="mt-4 block text-sm font-semibold">Kode 6 digit</label><input id="totp-code" className="form-input" inputMode="numeric" autoComplete="one-time-code" value={code} onChange={event => setCode(event.target.value)} required maxLength={6} /><button className="button-primary mt-4 disabled:opacity-60" type="button" disabled={operation !== null || code.length !== 6} onClick={() => void enable()}>{operation === "enable" ? "Memproses…" : "Aktifkan"}</button></div>}{recovery.length > 0 && <div className="mt-4" role="status" aria-live="polite"><p className="font-semibold">Simpan recovery code sekarang. Kode tidak ditampilkan kembali.</p><ul className="mt-3 grid grid-cols-1 gap-2 font-mono text-sm sm:grid-cols-2">{recovery.map(item => <li key={item}>{item}</li>)}</ul></div>}</section><section className="rounded-2xl border border-slate-200 bg-white p-6"><h2 className="text-xl font-bold">Sesi aktif</h2><div className="mt-4">{loadingSessions && <LoadingState title="Memuat sesi aktif…" description="Perangkat dan masa berlaku sesi sedang diperiksa." />}{!loadingSessions && sessionError && <ErrorState title="Sesi tidak dapat dimuat" onRetry={() => { setLoadingSessions(true); void loadSessions(); }} />}{!loadingSessions && !sessionError && sessions.length === 0 && <EmptyState title="Tidak ada sesi aktif" description="Masuk kembali untuk membuat sesi perangkat baru." />}{!loadingSessions && !sessionError && sessions.length > 0 && <ul className="space-y-3">{sessions.map(session => <li key={session.id} className="rounded-lg border border-slate-200 p-3"><p className="truncate text-sm font-semibold">{session.userAgent || "Perangkat tidak dikenal"}</p><p className="mt-1 text-xs text-slate-500">Terakhir aktif {new Date(session.lastSeenAt).toLocaleString("id-ID")}{session.isCurrent ? " · Sesi ini" : ""}</p>{!session.revokedAt && !session.isCurrent && <button className="button-quiet mt-2 text-sm text-red-700 underline" type="button" disabled={operation !== null} onClick={() => void revoke(session.id)}>Logout perangkat ini</button>}</li>)}</ul>}</div></section></div>;
}
