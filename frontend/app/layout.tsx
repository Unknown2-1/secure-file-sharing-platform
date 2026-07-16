import type { Metadata } from "next";
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
      <body className="min-h-full flex flex-col">{children}</body>
    </html>
  );
}
