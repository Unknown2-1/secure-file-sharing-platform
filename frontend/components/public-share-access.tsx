"use client";

import { FormEvent, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";

type SharedFile = { id: string; filename: string; size: number; detectedMimeType?: string };
type AccessResponse = { expiresAt: string; name: string; description?: string; allowPreview: boolean; files: SharedFile[] };

export function PublicShareAccess({ publicIdentifier, secretToken }: { publicIdentifier: string; secretToken: string }) {
  const [password, setPassword] = useState("");
  const [share, setShare] = useState<AccessResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function access(event: FormEvent) {
    event.preventDefault(); setLoading(true); setError(null);
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/public/shares/access`, {
        method: "POST", credentials: "include",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
        body: JSON.stringify({ publicIdentifier, secretToken, password: password || null }),
      });
      if (!response.ok) throw new Error("access_denied");
      setShare((await response.json()) as AccessResponse);
      setPassword("");
    } catch {
      setError("Tautan, masa berlaku, atau password tidak dapat diverifikasi. Periksa kembali lalu coba lagi.");
    } finally { setLoading(false); }
  }

  if (!share) return (
    <form onSubmit={(event) => void access(event)} className="mx-auto max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm md:p-8">
      <p className="section-label">TAUTAN TERLINDUNGI</p>
      <h1 className="mt-2 text-2xl font-bold">Buka file yang dibagikan</h1>
      <p className="mt-3 text-sm leading-6 text-slate-600">Jika pengirim memberikan password, masukkan di bawah. Tautan tanpa password dapat dibuka dengan membiarkan kolom kosong.</p>
      <label className="mt-6 block text-sm font-semibold" htmlFor="share-password">Password share</label>
      <input id="share-password" type="password" autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} maxLength={128}
        className="mt-2 min-h-11 w-full rounded-xl border border-slate-300 bg-white px-3 focus:border-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-200" />
      {error && <p role="alert" className="mt-3 text-sm leading-6 text-red-700">{error}</p>}
      <button disabled={loading} className="button-primary mt-5 w-full disabled:cursor-wait disabled:opacity-60" type="submit">{loading ? "Memverifikasi…" : "Lanjutkan"}</button>
    </form>
  );

  return (
    <section className="mx-auto max-w-2xl rounded-2xl border border-slate-200 bg-white p-6 shadow-sm md:p-8">
      <p className="section-label">SIAP DIUNDUH</p>
      <h1 className="mt-2 text-2xl font-bold">{share.name}</h1>
      {share.description && <p className="mt-3 whitespace-pre-wrap text-slate-600">{share.description}</p>}
      <p className="mt-3 text-sm text-slate-500">Akses sesi berakhir {new Intl.DateTimeFormat("id-ID", { dateStyle: "medium", timeStyle: "short" }).format(new Date(share.expiresAt))}.</p>
      <ul className="mt-6 space-y-3" aria-label="File yang dibagikan">
        {share.files.map((file) => <li key={file.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 p-4">
          <div className="min-w-0"><p className="truncate font-semibold">{file.filename}</p><p className="mt-1 text-xs text-slate-500">{formatBytes(file.size)}</p></div>
          <div className="flex gap-2">
            {share.allowPreview && previewable(file) && <a className="button-secondary" target="_blank" rel="noreferrer" href={`${apiBaseUrl}/api/v1/previews/${file.id}`}>Preview</a>}
            <a className="button-primary" href={`${apiBaseUrl}/api/v1/downloads/${file.id}`}>Unduh</a>
          </div>
        </li>)}
      </ul>
      <p className="mt-5 text-xs leading-5 text-slate-500">Slot download dihitung saat unduhan dimulai dan tidak dikembalikan bila koneksi terputus.</p>
    </section>
  );
}

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function previewable(file: SharedFile) {
  return ["image/png", "image/jpeg", "image/webp", "application/pdf", "text/plain"].includes(file.detectedMimeType ?? "");
}
