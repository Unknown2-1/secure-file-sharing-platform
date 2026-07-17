import type { Metadata } from "next";
import { LocaleProvider } from "@/lib/i18n/locale-context";
import "./globals.css";

export const metadata: Metadata = {
  title: "VaultShare — Berbagi file dengan kendali",
  description: "Platform berbagi file dengan penyimpanan terenkripsi, tautan kedaluwarsa, pemindaian malware, dan audit aktivitas.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="id" className="h-full antialiased">
      <body className="min-h-full flex flex-col">
        <a
          href="#main-content"
          className="sr-only focus:not-sr-only focus:fixed focus:top-4 focus:left-4 focus:z-50 focus:rounded-md focus:bg-blue-600 focus:px-4 focus:py-2 focus:text-white focus:outline-none focus:ring-2 focus:ring-blue-400"
        >
          Langsung ke konten
        </a>
        <LocaleProvider>
          {children}
        </LocaleProvider>
      </body>
    </html>
  );
}
