import Link from "next/link";
import { DashboardShell } from "@/components/dashboard-shell";
export default function DashboardPage() { return <main className="mx-auto min-h-screen max-w-6xl px-5 py-8 md:px-10 md:py-12"><nav className="mb-10 flex justify-between"><Link className="font-bold text-blue-800" href="/">VaultShare</Link><div className="flex gap-2"><Link className="button-quiet" href="/notifications">Notifikasi</Link><Link className="button-quiet" href="/settings/security">Keamanan akun</Link></div></nav><DashboardShell /></main>; }
