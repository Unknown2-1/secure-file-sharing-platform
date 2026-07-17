"use client";

import { createContext, useCallback, useContext, useState } from "react";
import { translate as translateMessage, Locale, isSupportedLocale } from "./messages";

const LOCALE_COOKIE = "vaultshare_locale";

type LocaleContextType = {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: (key: Parameters<typeof translateMessage>[1]) => string;
};

const LocaleContext = createContext<LocaleContextType | null>(null);

function getLocaleFromCookie(): Locale | null {
  if (typeof document === "undefined") return null;
  const match = document.cookie.match(new RegExp(`(^| )${LOCALE_COOKIE}=([^;]+)`));
  const value = match?.[2];
  return isSupportedLocale(value) ? value : null;
}

function setLocaleCookie(locale: Locale) {
  document.cookie = `${LOCALE_COOKIE}=${locale}; path=/; max-age=31536000; SameSite=Lax`;
}

// Initialize from cookie synchronously to avoid hydration mismatch
function getInitialLocale(): Locale {
  const saved = getLocaleFromCookie();
  return saved ?? "id";
}

export function LocaleProvider({ children }: { children: React.ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(getInitialLocale);

  const setLocale = useCallback((newLocale: Locale) => {
    setLocaleState(newLocale);
    setLocaleCookie(newLocale);
  }, []);

  const t = useCallback(
    (key: Parameters<typeof translateMessage>[1]) => {
      return translateMessage(locale, key);
    },
    [locale]
  );

  return (
    <LocaleContext.Provider value={{ locale, setLocale, t }}>
      {children}
    </LocaleContext.Provider>
  );
}

export function useLocale() {
  const context = useContext(LocaleContext);
  if (!context) {
    // Return default values if used outside provider
    return { locale: "id" as Locale, setLocale: () => {}, t: () => "" };
  }
  return context;
}
