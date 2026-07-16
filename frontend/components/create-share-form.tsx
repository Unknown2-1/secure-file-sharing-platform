"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/async-state";

type FileItem = { id: string; filename: string; availabilityStatus: string };

export function CreateShareForm({ workspaceId }: { workspaceId: string }) {
  const [files, setFiles] = useState<FileItem[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [password, setPassword] = useState("");
  const [limit, setLimit] = useState("");
  const [oneTime, setOneTime] = useState(false);
  const [preview, setPreview] = useState(false);
  const [minimumExpiration] = useState(() => new Date(Date.now() + 60_000).toISOString().slice(0, 16));
  const [expires, setExpires] = useState(() => new Date(Date.now() + 7 * 86_400_000).toISOString().slice(0, 16));
  const [link, setLink] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loadingFiles, setLoadingFiles] = useState(true);
  const [fileLoadError, setFileLoadError] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const loadFiles = useCallback(async () => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/files?workspaceId=${workspaceId}&status=Available&page=1&pageSize=100`, { credentials: "include" });
      if (!response.ok) { setFileLoadError(true); return; }
      setFiles(((await response.json()) as { items: FileItem[] }).items);
      setFileLoadError(false);
    } catch {
      setFileLoadError(true);
    } finally {
      setLoadingFiles(false);
    }
  }, [workspaceId]);

  useEffect(() => {
    let active = true;
    void fetch(`${apiBaseUrl}/api/v1/files?workspaceId=${workspaceId}&status=Available&page=1&pageSize=100`, { credentials: "include" })
      .then(async response => {
        if (!active) return;
        if (!response.ok) { setFileLoadError(true); return; }
        setFiles(((await response.json()) as { items: FileItem[] }).items);
        setFileLoadError(false);
      })
      .catch(() => { if (active) setFileLoadError(true); })
      .finally(() => { if (active) setLoadingFiles(false); });
    return () => { active = false; };
  }, [workspaceId]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (submitting || selected.length === 0) return;
    setSubmitting(true);
    setError(null);
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/shares`, {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf, "Idempotency-Key": crypto.randomUUID() },
        body: JSON.stringify({ workspaceId, fileIds: selected, name, description: description || null, password: password || null, startsAt: null, expiresAt: new Date(expires).toISOString(), maximumDownloads: oneTime || !limit ? null : Number(limit), isOneTime: oneTime, allowPreview: preview }),
      });
      if (!response.ok) { setError("Share tidak dapat dibuat. Periksa file dan kebijakan akses."); return; }
      const result = await response.json() as { publicIdentifier: string; secretToken: string };
      setLink(`${window.location.origin}/s/${result.publicIdentifier}/${result.secretToken}`);
      setPassword("");
    } catch {
      setError("Share tidak dapat dibuat. Periksa koneksi dan coba lagi.");
    } finally {
      setSubmitting(false);
    }
  }

  if (link) return <section className="rounded-2xl border border-emerald-200 bg-emerald-50 p-6"><h1 className="text-2xl font-bold">Share berhasil dibuat</h1><p className="mt-2 text-sm">Link rahasia ditampilkan satu kali. Simpan sekarang dan kirim password melalui kanal berbeda.</p><input aria-label="Link share" className="form-input font-mono text-sm" readOnly value={link} /><button className="button-primary mt-4" type="button" onClick={() => void navigator.clipboard.writeText(link)}>Salin link</button></section>;

  return <form className="rounded-2xl border border-slate-200 bg-white p-6" aria-busy={submitting} onSubmit={(event) => void submit(event)}><h1 className="text-2xl font-bold">Buat share</h1><fieldset className="mt-6"><legend className="font-semibold">1. Pilih file</legend><div className="mt-3">{loadingFiles && <LoadingState title="Memuat file…" description="Mencari file yang sudah tersedia dan aman dibagikan." />}{!loadingFiles && fileLoadError && <ErrorState title="File tidak dapat dimuat" onRetry={() => { setLoadingFiles(true); void loadFiles(); }} />}{!loadingFiles && !fileLoadError && files.length === 0 && <EmptyState title="Tidak ada file yang siap dibagikan" description="Tunggu pemindaian dan enkripsi selesai, atau unggah file baru." />}{!loadingFiles && !fileLoadError && files.length > 0 && <div className="space-y-2">{files.map(file => <label className="flex min-h-11 items-center gap-3 rounded-lg border border-slate-200 px-3" key={file.id}><input type="checkbox" checked={selected.includes(file.id)} onChange={event => setSelected(current => event.target.checked ? [...current, file.id] : current.filter(id => id !== file.id))} />{file.filename}</label>)}</div>}</div></fieldset><label className="mt-6 block font-semibold" htmlFor="share-name">2. Nama share</label><input id="share-name" className="form-input" value={name} onChange={event => setName(event.target.value)} required maxLength={120} /><label className="mt-4 block text-sm font-semibold" htmlFor="share-description">Deskripsi</label><textarea id="share-description" className="form-input min-h-24" value={description} onChange={event => setDescription(event.target.value)} maxLength={1000} /><div className="mt-6 grid gap-4 sm:grid-cols-3"><div><label className="text-sm font-semibold" htmlFor="expires">Kedaluwarsa</label><input id="expires" className="form-input" type="datetime-local" min={minimumExpiration} value={expires} onChange={event => setExpires(event.target.value)} required /></div><div><label className="text-sm font-semibold" htmlFor="share-pass">Password opsional</label><input id="share-pass" className="form-input" type="password" minLength={8} maxLength={128} value={password} onChange={event => setPassword(event.target.value)} /></div><div><label className="text-sm font-semibold" htmlFor="limit">Batas download</label><input id="limit" className="form-input" type="number" min={1} disabled={oneTime} value={limit} onChange={event => setLimit(event.target.value)} /></div></div><div className="mt-5 flex flex-wrap gap-5"><label className="flex min-h-11 items-center"><input type="checkbox" checked={oneTime} onChange={event => setOneTime(event.target.checked)} /> <span className="ml-2">Sekali download</span></label><label className="flex min-h-11 items-center"><input type="checkbox" checked={preview} onChange={event => setPreview(event.target.checked)} /> <span className="ml-2">Izinkan preview aman</span></label></div>{error && <p role="alert" className="mt-4 text-red-700">{error}</p>}<button className="button-primary mt-6 disabled:opacity-60" type="submit" disabled={submitting || loadingFiles || fileLoadError || selected.length === 0}>{submitting ? "Memproses…" : "Buat share"}</button></form>;
}
