import Link from "next/link";
import { WorkspaceMembers } from "@/components/workspace-members";
export default async function MembersPage({ searchParams }: { searchParams: Promise<{ workspace?: string }> }) { const { workspace } = await searchParams; return <main className="mx-auto min-h-screen max-w-5xl px-5 py-10"><Link href="/dashboard" className="text-blue-800 underline">← Dashboard</Link><div className="mt-6">{workspace ? <WorkspaceMembers workspaceId={workspace} /> : <p role="alert">Workspace belum dipilih.</p>}</div></main>; }
