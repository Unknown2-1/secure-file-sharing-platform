import Link from "next/link"; import { NotificationCenter } from "@/components/notification-center";
export default function NotificationsPage() { return <main className="mx-auto min-h-screen max-w-4xl px-5 py-10"><Link href="/dashboard" className="text-blue-800 underline">← Dashboard</Link><h1 className="mb-8 mt-5 text-3xl font-bold">Notifikasi</h1><NotificationCenter /></main>; }
