import type { Metadata } from "next";
import { PublicShareAccess } from "@/components/public-share-access";

export const metadata: Metadata = {
  title: "File dibagikan · VaultShare",
  robots: { index: false, follow: false, nocache: true },
  referrer: "no-referrer",
};

export default async function PublicSharePage({ params }: { params: Promise<{ publicIdentifier: string; secretToken: string }> }) {
  const { publicIdentifier, secretToken } = await params;
  return <main className="min-h-screen bg-slate-50 px-5 py-16 md:py-24"><PublicShareAccess publicIdentifier={publicIdentifier} secretToken={secretToken} /></main>;
}
