"use client";

import { FormEvent, useEffect, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";

type Setting = {
  storageQuotaBytes: number;
  auditRetentionDays: number;
  deletedFileGraceDays: number;
  allowMemberPublicShares: boolean;
};

export function WorkspaceSettings({ workspaceId }: { workspaceId: string }) {
  const [setting, setSetting] = useState<Setting | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    void fetch(`${apiBaseUrl}/api/v1/workspaces/${workspaceId}/settings`, { credentials: "include" })
      .then(async response => { if (response.ok) setSetting(await response.json() as Setting); else setMessage("Pengaturan tidak dapat dimuat."); });
  }, [workspaceId]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!setting) return;
    const csrf = await getCsrfToken();
    const response = await fetch(`${apiBaseUrl}/api/v1/workspaces/${workspaceId}/settings`, {
      method: "PUT",
      credentials: "include",
      headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
      body: JSON.stringify(setting),
    });
    if (!response.ok) { setMessage("Hanya Owner yang dapat mengubah kebijakan keamanan workspace."); return; }
    setSetting(await response.json() as Setting);
    setMessage("Kebijakan workspace disimpan dan dicatat dalam audit log.");
  }

  if (!setting) return <p role="status">{message ?? "Memuat pengaturan…"}</p>;
  return <form className="rounded-2xl border border-slate-200 bg-white p-6" onSubmit={(event) => void submit(event)}>
    <h1 className="text-2xl font-bold">Kebijakan workspace</h1>
    <p className="mt-2 text-sm text-slate-600">Perubahan diterapkan di backend dan menghasilkan audit event.</p>
    <div className="mt-6 grid gap-4 sm:grid-cols-3">
      <label className="text-sm font-semibold">Quota (byte)<input className="form-input" type="number" min={1_048_576} value={setting.storageQuotaBytes} onChange={(event) => setSetting({ ...setting, storageQuotaBytes: Number(event.target.value) })} /></label>
      <label className="text-sm font-semibold">Retensi audit (hari)<input className="form-input" type="number" min={30} max={3650} value={setting.auditRetentionDays} onChange={(event) => setSetting({ ...setting, auditRetentionDays: Number(event.target.value) })} /></label>
      <label className="text-sm font-semibold">Grace delete (hari)<input className="form-input" type="number" min={0} max={365} value={setting.deletedFileGraceDays} onChange={(event) => setSetting({ ...setting, deletedFileGraceDays: Number(event.target.value) })} /></label>
    </div>
    <label className="mt-5 flex items-center gap-3"><input type="checkbox" checked={setting.allowMemberPublicShares} onChange={(event) => setSetting({ ...setting, allowMemberPublicShares: event.target.checked })} />Izinkan Member membuat public share miliknya</label>
    {message && <p className="mt-4 text-sm text-slate-700" role="status">{message}</p>}
    <button className="button-primary mt-5" type="submit">Simpan kebijakan</button>
  </form>;
}
