import Link from "next/link";
import { ShareList } from "@/components/share-list";
import { LanguageSwitcher } from "@/components/ui/language-switcher";
import { translate } from "@/lib/i18n/messages";

export default async function SharesPage({ searchParams }: { searchParams: Promise<{ workspace?: string }> }) {
  const { workspace } = await searchParams;
  const locale = "id"; // Server component default

  return (
    <main id="main-content" className="mx-auto min-h-screen max-w-5xl px-5 py-10">
      <nav className="mb-6 flex flex-wrap items-center justify-between gap-4" aria-label="Navigasi share">
        <Link href="/dashboard" className="text-blue-800 underline">
          ← Kembali ke Dashboard
        </Link>
        <LanguageSwitcher />
      </nav>
      <div className="mb-8 mt-5 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-bold sm:text-3xl">{translate(locale, "shares.title")}</h1>
        {workspace && (
          <Link className="button-primary" href={`/shares/new?workspace=${workspace}`}>
            {translate(locale, "shares.create")}
          </Link>
        )}
      </div>
      {workspace ? (
        <ShareList workspaceId={workspace} />
      ) : (
        <p>Workspace belum dipilih.</p>
      )}
    </main>
  );
}
