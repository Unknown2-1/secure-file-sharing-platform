"use client";

import { useCallback, useEffect, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/async-state";

type Notification = { id: string; type: string; title: string; message: string; createdAt: string; readAt?: string };

export function NotificationCenter() {
  const [items, setItems] = useState<Notification[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const load = useCallback(async () => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/notifications?take=100`, { credentials: "include" });
      if (!response.ok) { setError(true); return; }
      setItems(await response.json() as Notification[]);
      setError(false);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    void fetch(`${apiBaseUrl}/api/v1/notifications?take=100`, { credentials: "include" })
      .then(async response => {
        if (!active) return;
        if (!response.ok) { setError(true); return; }
        setItems(await response.json() as Notification[]);
        setError(false);
      })
      .catch(() => { if (active) setError(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  async function read(id: string) {
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/notifications/${id}/read`, { method: "POST", credentials: "include", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf }, body: "{}" });
      if (response.ok) setItems(current => current.map(item => item.id === id ? { ...item, readAt: new Date().toISOString() } : item));
      else setError(true);
    } catch {
      setError(true);
    }
  }

  if (loading) return <LoadingState title="Memuat notifikasi…" description="Aktivitas terbaru sedang dimuat." />;
  if (error) return <ErrorState title="Notifikasi tidak dapat dimuat" description="Pusat notifikasi sementara tidak tersedia." onRetry={() => { setLoading(true); void load(); }} />;
  if (items.length === 0) return <EmptyState title="Belum ada notifikasi" description="Aktivitas keamanan dan pemrosesan file akan muncul di sini." />;

  return <ul className="space-y-3">{items.map(item => <li key={item.id} className={`rounded-xl border p-5 ${item.readAt ? "border-slate-200 bg-white" : "border-blue-200 bg-blue-50"}`}><div className="flex flex-wrap justify-between gap-3"><div><p className="font-bold">{item.title}</p><p className="mt-2 text-sm leading-6 text-slate-700">{item.message}</p><p className="mt-2 text-xs text-slate-500">{new Date(item.createdAt).toLocaleString("id-ID")}</p></div>{!item.readAt && <button className="button-secondary" type="button" onClick={() => void read(item.id)}>Tandai dibaca</button>}</div></li>)}</ul>;
}
