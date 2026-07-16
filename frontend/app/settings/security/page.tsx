import Link from "next/link"; import { SecuritySettings } from "@/components/security-settings";
export default function SecurityPage() { return <main className="mx-auto min-h-screen max-w-5xl px-5 py-10"><Link href="/dashboard" className="text-blue-800 underline">← Dashboard</Link><h1 className="mb-8 mt-5 text-3xl font-bold">Keamanan akun</h1><SecuritySettings /></main>; }
