import Link from "next/link";
import { UploadDropzone } from "@/components/upload-dropzone";

export default async function UploadPage({ searchParams }: { searchParams: Promise<{ workspace?: string }> }) {
  const { workspace } = await searchParams;
  return (
    <main id="content" className="mx-auto min-h-screen max-w-4xl px-5 py-8 md:px-10 md:py-12">
      <a className="skip-link" href="#upload-title">Lewati ke upload</a>
      <nav className="mb-8 flex items-center justify-between" aria-label="Navigasi dashboard">
        <Link href="/" className="font-bold text-blue-800">VaultShare</Link>
        <Link href="/dashboard" className="button-quiet">Kembali ke dashboard</Link>
      </nav>
      {workspace ? (
        <UploadDropzone workspaceId={workspace} />
      ) : (
        <section className="rounded-2xl border border-amber-200 bg-amber-50 p-6" role="alert">
          <h1 className="text-xl font-bold">Workspace belum dipilih</h1>
          <p className="mt-2 text-amber-900">Buka halaman upload dari dashboard workspace agar tujuan penyimpanan dapat diverifikasi.</p>
        </section>
      )}
    </main>
  );
}
