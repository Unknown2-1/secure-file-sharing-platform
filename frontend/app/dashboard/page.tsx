"use client";
import Link from "next/link";
import { LanguageSwitcher } from "@/components/ui/language-switcher";
import { useLocale } from "@/lib/i18n/locale-context";
import { translate } from "@/lib/i18n/messages";
import { DashboardShell } from "@/components/dashboard-shell";

export default function DashboardPage() {
  const { locale } = useLocale();

  return (
    <main id="main-content" className="mx-auto min-h-screen max-w-6xl px-5 py-8 md:px-10 md:py-12">
      <nav className="mb-10 flex flex-wrap items-center justify-between gap-4" aria-label={translate(locale, "nav.dashboard")}>
        <Link className="font-bold text-blue-800" href="/">
          VaultShare
        </Link>
        <div className="flex flex-wrap items-center gap-4">
          <div className="flex gap-2">
            <Link className="button-quiet" href="/notifications">
              {translate(locale, "nav.notifications")}
            </Link>
            <Link className="button-quiet" href="/settings/security">
              {translate(locale, "nav.security")}
            </Link>
          </div>
          <LanguageSwitcher />
        </div>
      </nav>
      <DashboardShell />
    </main>
  );
}
