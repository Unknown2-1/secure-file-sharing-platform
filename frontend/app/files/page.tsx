import Link from "next/link";
import { FileList } from "@/components/file-list";
import { LanguageSwitcher } from "@/components/ui/language-switcher";

export default async function FilesPage({ searchParams }: { searchParams: Promise<{ workspace?: string }> }) {
  const { workspace } = await searchParams;

  return (
    <main id="main-content" className="mx-auto min-h-screen max-w-5xl px-5 py-10">
      <nav className="mb-6 flex flex-wrap items-center justify-between gap-4" aria-label="Navigasi file">
        <Link href="/dashboard" className="text-blue-800 underline">
          ← Kembali ke Dashboard
        </Link>
        <LanguageSwitcher />
      </nav>
      <h1 className="mb-8 mt-5 text-2xl font-bold sm:text-3xl">File saya</h1>
      {workspace ? (
        <FileList workspaceId={workspace} />
      ) : (
        <p role="alert">Workspace belum dipilih.</p>
      )}
    </main>
  );
}
