import Link from "next/link";
import { AcceptInvitationForm } from "@/components/accept-invitation-form";
export default function AcceptInvitationPage() { return <main className="mx-auto min-h-screen max-w-xl px-5 py-10"><Link href="/dashboard" className="text-blue-800 underline">← Dashboard</Link><div className="mt-6"><AcceptInvitationForm /></div></main>; }
