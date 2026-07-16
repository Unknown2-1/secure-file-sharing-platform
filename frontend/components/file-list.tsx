"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/async-state";

type FileItem = {
  id: string;
  filename: string;
  size: number;
  availabilityStatus: string;
  detectedMimeType?: string;
  createdAt: string;
  deletedAt?: string;
};

export function FileList({ workspaceId }: { workspaceId: string }) {
  const [files, setFiles] = useState<FileItem[]>([]);
  const [search, setSearch] = useState("");
  const [error, setError] = useState(false);
  const [loading, setLoading] = useState(true);
  const [pendingDeletion, setPendingDeletion] = useState<FileItem | null>(null);
  const [lifecycleBusy, setLifecycleBusy] = useState(false);

  const load = useCallback(async (term: string) => {
    setLoading(true);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/files?workspaceId=${workspaceId}&search=${encodeURIComponent(term)}&page=1&pageSize=50`, { credentials: "include" });
      if (!response.ok) { setError(true); return; }
      const body = await response.json() as { items: FileItem[] };
      setFiles(body.items);
      setError(false);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, [workspaceId]);

  useEffect(() => {
    const timer = setTimeout(() => void load(search), 150);
    return () => clearTimeout(timer);
  }, [load, search]);

  async function changeLifecycle(file: FileItem, action: "delete" | "restore") {
    if (lifecycleBusy) return;
    setLifecycleBusy(true);
    try {
      const csrf = await getCsrfToken();
      const path = action === "delete" ? `/api/v1/files/${file.id}` : `/api/v1/files/${file.id}/restore`;
      const response = await fetch(`${apiBaseUrl}${path}`, {
        method: action === "delete" ? "DELETE" : "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf, "Idempotency-Key": crypto.randomUUID() },
        body: "{}",
      });
      if (!response.ok) { setError(true); return; }
      setPendingDeletion(null);
      await load(search);
    } catch {
      setError(true);
    } finally {
      setLifecycleBusy(false);
    }
  }

  return <section>
    <label className="text-sm font-semibold" htmlFor="file-search">Cari file</label>
    <input id="file-search" className="form-input max-w-md" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Nama file" />
    {pendingDeletion && <section className="mt-6 rounded-2xl border border-red-200 bg-red-50 p-5" role="alertdialog" aria-labelledby="delete-file-title" aria-describedby="delete-file-description" aria-modal="false"><h2 id="delete-file-title" className="font-bold text-red-950">Konfirmasi penghapusan file</h2><p id="delete-file-description" className="mt-2 text-sm leading-6 text-red-900">Pindahkan <strong>{pendingDeletion.filename}</strong> ke file terhapus? Share terkait akan ditolak sesuai lifecycle backend.</p><div className="mt-4 flex flex-wrap gap-3"><button className="button-secondary" type="button" disabled={lifecycleBusy} onClick={() => setPendingDeletion(null)}>Batal</button><button className="button-primary bg-red-700 disabled:opacity-60" type="button" disabled={lifecycleBusy} onClick={() => void changeLifecycle(pendingDeletion, "delete")}>{lifecycleBusy ? "Memproses…" : "Hapus file"}</button></div></section>}
    <div className="mt-6">{loading && <LoadingState title="Memuat file…" description="Metadata file sedang dimuat dari workspace." />}{error && !loading && <ErrorState title="File tidak dapat dimuat" description="Operasi file tidak dapat diproses. Periksa koneksi dan coba lagi." onRetry={() => void load(search)} />}{!loading && !error && files.length === 0 && <EmptyState title="Belum ada file yang cocok" description={search ? "Ubah kata pencarian atau hapus filter." : "Unggah file pertama agar dapat diproses dan dibagikan."} />}</div>
    {!loading && !error && files.length > 0 && <ul className="mt-6 divide-y divide-slate-200 overflow-hidden rounded-2xl border border-slate-200 bg-white">
      {files.map((file) => <li key={file.id} className="p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div><p className="font-semibold">{file.filename}</p><p className="mt-1 text-xs text-slate-500">{(file.size / 1024).toFixed(1)} KB · {new Date(file.createdAt).toLocaleDateString("id-ID")}</p></div>
          <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-bold">{file.availabilityStatus}</span>
        </div>
        <div className="mt-4 flex flex-wrap gap-2">
          {file.availabilityStatus === "Available" && <a className="button-secondary" href={`${apiBaseUrl}/api/v1/internal-files/${file.id}/download`}>Unduh</a>}
          {file.availabilityStatus === "Available" && previewable(file.detectedMimeType, file.size) && <a className="button-secondary" target="_blank" rel="noreferrer" href={`${apiBaseUrl}/api/v1/internal-files/${file.id}/preview`}>Preview</a>}
          {file.deletedAt
            ? <button className="button-secondary" type="button" onClick={() => void changeLifecycle(file, "restore")}>Pulihkan</button>
            : <button className="button-secondary" type="button" onClick={() => setPendingDeletion(file)}>Hapus</button>}
        </div>
        {file.availabilityStatus === "Available" && <InternalGrantForm fileId={file.id} />}
      </li>)}
    </ul>}
  </section>;
}

function previewable(mime: string | undefined, size: number) {
  return ["image/png", "image/jpeg", "image/webp", "application/pdf"].includes(mime ?? "") ||
    mime === "text/plain" && size <= 1024 * 1024;
}

function InternalGrantForm({ fileId }: { fileId: string }) {
  const [email, setEmail] = useState("");
  const [permission, setPermission] = useState("View");
  const [message, setMessage] = useState<string | null>(null);
  const [grants, setGrants] = useState<{ id: string; recipientEmail: string; permission: string; revokedAt?: string }[]>([]);

  async function loadGrants() {
    const response = await fetch(`${apiBaseUrl}/api/v1/files/${fileId}/internal-grants`, { credentials: "include" });
    if (response.ok) setGrants(await response.json() as typeof grants);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setMessage(null);
    const csrf = await getCsrfToken();
    const response = await fetch(`${apiBaseUrl}/api/v1/files/${fileId}/internal-grants`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
      body: JSON.stringify({ recipientEmail: email, permission, expiresAt: null }),
    });
    if (!response.ok) { setMessage("Akses tidak dapat diberikan. Pastikan penerima anggota workspace dan Anda memiliki izin."); return; }
    setEmail("");
    setMessage("Akses internal berhasil diberikan.");
    await loadGrants();
  }

  async function revoke(grantId: string) {
    const csrf = await getCsrfToken();
    const response = await fetch(`${apiBaseUrl}/api/v1/internal-file-grants/${grantId}`, {
      method: "DELETE", credentials: "include", headers: { "X-CSRF-TOKEN": csrf },
    });
    if (response.ok) await loadGrants();
  }

  return <details className="mt-4 rounded-xl bg-slate-50 p-3">
    <summary className="cursor-pointer text-sm font-semibold">Berikan akses internal</summary>
    <form className="mt-3 grid gap-3 sm:grid-cols-[1fr_auto_auto]" onSubmit={(event) => void submit(event)}>
      <label className="sr-only" htmlFor={`recipient-${fileId}`}>Email anggota</label>
      <input id={`recipient-${fileId}`} className="form-input mt-0" type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="anggota@example.com" required />
      <label className="sr-only" htmlFor={`permission-${fileId}`}>Izin</label>
      <select id={`permission-${fileId}`} className="form-input mt-0" value={permission} onChange={(event) => setPermission(event.target.value)}><option value="View">Lihat</option><option value="Download">Download</option></select>
      <button className="button-primary" type="submit">Berikan</button>
    </form>
    {message && <p role="status" className="mt-2 text-sm text-slate-700">{message}</p>}
    <button className="mt-3 text-sm font-semibold text-blue-800 underline" type="button" onClick={() => void loadGrants()}>Muat akses saat ini</button>
    {grants.length > 0 && <ul className="mt-3 space-y-2">{grants.map(grant => <li className="flex flex-wrap items-center justify-between gap-2 text-sm" key={grant.id}><span>{grant.recipientEmail} · {grant.permission} {grant.revokedAt ? "· Dicabut" : ""}</span>{!grant.revokedAt && <button className="font-semibold text-red-700 underline" type="button" onClick={() => void revoke(grant.id)}>Cabut</button>}</li>)}</ul>}
  </details>;
}
