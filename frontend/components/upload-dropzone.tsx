"use client";

import { useRef, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";

const chunkSize = 4 * 1024 * 1024;
const maximumFileSize = 1024 * 1024 * 1024;
const maximumFiles = 10;
const allowedExtensions = new Set(["txt", "pdf", "png", "jpg", "jpeg", "webp", "bin"]);

type QueueStatus = "menunggu" | "mengunggah" | "selesai" | "dibatalkan" | "gagal";
type QueueItem = { key: string; file: File; progress: number; status: QueueStatus; uploadId?: string; error?: string };
type UploadSession = { id: string; uploadOffset: number };

export function UploadDropzone({ workspaceId }: { workspaceId: string }) {
  const [queue, setQueue] = useState<QueueItem[]>([]);
  const [message, setMessage] = useState("Belum ada file dipilih.");
  const [dragging, setDragging] = useState(false);
  const controllers = useRef(new Map<string, AbortController>());

  function addFiles(files: File[]) {
    const remaining = maximumFiles - queue.length;
    const accepted: QueueItem[] = [];
    let rejected = 0;
    for (const file of files.slice(0, Math.max(remaining, 0))) {
      const extension = file.name.split(".").pop()?.toLowerCase() ?? "";
      if (file.size <= 0 || file.size > maximumFileSize || !allowedExtensions.has(extension) || file.name.includes("\0")) {
        rejected += 1;
        continue;
      }
      accepted.push({ key: crypto.randomUUID(), file, progress: 0, status: "menunggu" });
    }
    rejected += Math.max(0, files.length - remaining);
    setQueue((current) => [...current, ...accepted]);
    setMessage(rejected > 0 ? `${accepted.length} file ditambahkan, ${rejected} ditolak.` : `${accepted.length} file siap diunggah.`);
  }

  async function upload(item: QueueItem) {
    const controller = new AbortController();
    controllers.current.set(item.key, controller);
    update(item.key, { status: "mengunggah", error: undefined });
    try {
      const csrf = await getCsrfToken(controller.signal);
      let session: UploadSession;
      if (item.uploadId) {
        const statusResponse = await fetch(`${apiBaseUrl}/api/v1/uploads/${item.uploadId}`, { credentials: "include", signal: controller.signal });
        if (!statusResponse.ok) throw new Error("resume_failed");
        session = (await statusResponse.json()) as UploadSession;
      } else {
        const response = await fetch(`${apiBaseUrl}/api/v1/uploads`, {
          method: "POST",
          credentials: "include",
          signal: controller.signal,
          headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf, "Idempotency-Key": item.key },
          body: JSON.stringify({ workspaceId, filename: item.file.name, fileSize: item.file.size, clientMimeType: item.file.type || "application/octet-stream" }),
        });
        if (!response.ok) throw new Error("create_failed");
        session = (await response.json()) as UploadSession;
        update(item.key, { uploadId: session.id });
      }

      let offset = session.uploadOffset;
      while (offset < item.file.size) {
        const body = item.file.slice(offset, Math.min(offset + chunkSize, item.file.size));
        const response = await fetch(`${apiBaseUrl}/api/v1/uploads/${session.id}`, {
          method: "PATCH",
          credentials: "include",
          signal: controller.signal,
          headers: { "Content-Type": "application/offset+octet-stream", "Upload-Offset": String(offset), "X-CSRF-TOKEN": csrf },
          body,
        });
        if (!response.ok) throw new Error("chunk_failed");
        offset = Number(response.headers.get("Upload-Offset"));
        if (!Number.isFinite(offset)) throw new Error("invalid_offset");
        update(item.key, { progress: Math.round((offset / item.file.size) * 100) });
      }

      const finalized = await fetch(`${apiBaseUrl}/api/v1/uploads/${session.id}/finalize`, {
        method: "POST",
        credentials: "include",
        signal: controller.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf, "Idempotency-Key": `${item.key}-finalize` },
        body: "{}",
      });
      if (!finalized.ok) throw new Error("finalize_failed");
      update(item.key, { progress: 100, status: "selesai" });
      setMessage(`${item.file.name} selesai diunggah dan menunggu pemeriksaan keamanan.`);
    } catch (error) {
      if ((error as Error).name === "AbortError") update(item.key, { status: "dibatalkan" });
      else update(item.key, { status: "gagal", error: "Upload terhenti. Coba lanjutkan kembali." });
    } finally {
      controllers.current.delete(item.key);
    }
  }

  async function cancel(item: QueueItem) {
    controllers.current.get(item.key)?.abort();
    if (item.uploadId) {
      try {
        const csrf = await getCsrfToken();
        await fetch(`${apiBaseUrl}/api/v1/uploads/${item.uploadId}`, {
          method: "DELETE",
          credentials: "include",
          headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf, "Idempotency-Key": `${item.key}-cancel` },
          body: "{}",
        });
      } catch {
        // The server cleanup job remains the fallback for an interrupted cancel request.
      }
    }
    update(item.key, { status: "dibatalkan" });
  }

  function update(key: string, changes: Partial<QueueItem>) {
    setQueue((current) => current.map((item) => (item.key === key ? { ...item, ...changes } : item)));
  }

  return (
    <section aria-labelledby="upload-title" className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm md:p-7">
      <div>
        <p className="section-label">UPLOAD TERLINDUNGI</p>
        <h1 id="upload-title" className="mt-2 text-2xl font-bold tracking-tight">Unggah file</h1>
        <p className="mt-2 text-sm leading-6 text-slate-600">Maksimum 10 file, masing-masing 1 GB. File belum dapat dibagikan sebelum pemindaian dan enkripsi selesai.</p>
      </div>

      <label
        className={`mt-6 flex min-h-52 cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed px-6 text-center transition ${dragging ? "border-blue-600 bg-blue-50" : "border-slate-300 bg-slate-50 hover:border-blue-500"}`}
        onDragEnter={(event) => { event.preventDefault(); setDragging(true); }}
        onDragOver={(event) => event.preventDefault()}
        onDragLeave={() => setDragging(false)}
        onDrop={(event) => { event.preventDefault(); setDragging(false); addFiles(Array.from(event.dataTransfer.files)); }}
      >
        <span aria-hidden="true" className="grid size-12 place-items-center rounded-xl bg-blue-700 text-2xl text-white">↑</span>
        <span className="mt-4 font-bold">Tarik file ke sini atau pilih dari perangkat</span>
        <span className="mt-2 text-sm text-slate-500">TXT, PDF, PNG, JPEG, WebP, atau BIN</span>
        <input className="sr-only" type="file" multiple onChange={(event) => addFiles(Array.from(event.target.files ?? []))} />
      </label>
      <p className="mt-3 text-sm text-slate-600" role="status" aria-live="polite">{message}</p>

      {queue.length > 0 && (
        <ul className="mt-6 space-y-3" aria-label="Antrean upload">
          {queue.map((item) => (
            <li key={item.key} className="rounded-xl border border-slate-200 p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0"><p className="truncate font-semibold">{item.file.name}</p><p className="mt-1 text-xs text-slate-500">{formatBytes(item.file.size)} · {item.status}</p></div>
                <div className="flex gap-2">
                  {(item.status === "menunggu" || item.status === "gagal" || item.status === "dibatalkan") && <button className="button-secondary" type="button" onClick={() => void upload(item)}>{item.status === "menunggu" ? "Unggah" : "Coba lagi"}</button>}
                  {item.status === "mengunggah" && <button className="button-secondary" type="button" onClick={() => void cancel(item)}>Batalkan</button>}
                </div>
              </div>
              <div className="mt-3 h-2 overflow-hidden rounded-full bg-slate-200" role="progressbar" aria-label={`Progress ${item.file.name}`} aria-valuemin={0} aria-valuemax={100} aria-valuenow={item.progress}>
                <div className="h-full rounded-full bg-blue-700 transition-[width]" style={{ width: `${item.progress}%` }} />
              </div>
              {item.error && <p className="mt-2 text-sm text-red-700">{item.error}</p>}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
