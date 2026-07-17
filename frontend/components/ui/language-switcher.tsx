"use client";

import { useLocale } from "@/lib/i18n/locale-context";
import { Locale, translate } from "@/lib/i18n/messages";

export function LanguageSwitcher() {
  const { locale, setLocale } = useLocale();

  function handleChange(event: React.ChangeEvent<HTMLSelectElement>) {
    setLocale(event.target.value as Locale);
  }

  return (
    <label className="inline-flex items-center gap-2">
      <span className="sr-only">{translate(locale, "language.switch")}</span>
      <select
        value={locale}
        onChange={handleChange}
        className="rounded-md border border-slate-300 bg-white px-2 py-1 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500"
        aria-label={translate(locale, "language.switch")}
      >
        <option value="id">{translate(locale, "language.indonesian")}</option>
        <option value="en">{translate(locale, "language.english")}</option>
      </select>
    </label>
  );
}
