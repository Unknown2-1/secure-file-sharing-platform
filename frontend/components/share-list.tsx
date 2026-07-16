"use client";

import { useCallback, useEffect, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/async-state";

type Share = { id: string; name: string; expiresAt: string; downloadCount: number; maximumDownloads?: number; isRevoked: boolean; isPasswordProtected: boolean };

export function ShareList({ workspaceId }: { workspaceId: string }) {
  const [shares, setShares] = useState<Share[]>([]);
  const [error, setError] = useState(false);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/shares?workspaceId=${workspaceId}`, { credentials: "include" });
      if (!response.ok) { setError(true); return; }
      setShares(await response.json() as Share[]);
      setError(false);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, [workspaceId]);

  useEffect(() => {
    let active = true;
    void fetch(`${apiBaseUrl}/api/v1/shares?workspaceId=${workspaceId}`, { credentials: "include" })
      .then(async response => {
        if (!active) return;
        if (!response.ok) { setError(true); return; }
        setShares(await response.json() as Share[]);
        setError(false);
      })
      .catch(() => { if (active) setError(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [workspaceId]);

  async function revoke(id: string) {
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/shares/${id}/revoke`, {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf, "Idempotency-Key": crypto.randomUUID() },
        body: "{}",
      });
      if (!response.ok) { setError(true); return; }
      await load();
    } catch {
      setError(true);
    }
  }

  if (loading) return <LoadingState title="Memuat share…" description="Kebijakan dan statistik akses sedang dimuat." />;
  if (error) return <ErrorState title="Share tidak dapat dimuat" description="Daftar share tidak tersedia. Coba lagi tanpa membagikan informasi sensitif." onRetry={() => { setLoading(true); void load(); }} />;
  if (shares.length === 0) return <EmptyState title="Belum ada share" description="Buat share dari file yang sudah berstatus Available." />;

  return <ul className="space-y-3">{shares.map((share) => <li className="rounded-xl border border-slate-200 bg-white p-5" key={share.id}><div className="flex flex-wrap justify-between gap-3"><div><h2 className="font-bold">{share.name}</h2><p className="mt-1 text-sm text-slate-500">{share.downloadCount}{share.maximumDownloads ? ` / ${share.maximumDownloads}` : ""} download · berakhir {new Date(share.expiresAt).toLocaleString("id-ID")}</p></div><div className="flex flex-wrap items-center gap-2"><span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-bold">{share.isRevoked ? "Dicabut" : share.isPasswordProtected ? "Berpassword" : "Aktif"}</span>{!share.isRevoked && <button type="button" className="button-secondary" onClick={() => void revoke(share.id)}>Cabut</button>}</div></div></li>)}</ul>;
}
