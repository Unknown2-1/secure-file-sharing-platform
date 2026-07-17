"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { apiBaseUrl } from "@/lib/api";
import { EmptyState, LoadingState } from "@/components/ui/async-state";
import { useLocale } from "@/lib/i18n/locale-context";
import { translate, translateWithParams } from "@/lib/i18n/messages";

type Workspace = { id: string; name: string; role: string };
type Profile = { displayName: string; email: string };
type Dashboard = {
  storageBytes: number;
  storageQuotaBytes: number;
  totalFiles: number;
  activeShares: number;
  downloadsLastSevenDays: number;
  processingFiles: number;
  quarantinedFiles: number;
  sharesExpiringSoon: number;
  activity: { date: string; uploads: number; downloads: number }[];
};

export function DashboardShell() {
  const { locale } = useLocale();
  const [profile, setProfile] = useState<Profile | null>(null);
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [dashboard, setDashboard] = useState<Dashboard | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    const load = async () => {
      const [me, ws] = await Promise.all([
        fetch(`${apiBaseUrl}/api/v1/auth/me`, { credentials: "include" }),
        fetch(`${apiBaseUrl}/api/v1/workspaces`, { credentials: "include" }),
      ]);
      if (!me.ok || !ws.ok) {
        setError(true);
        return;
      }
      const workspaceRows = (await ws.json()) as Workspace[];
      setProfile(await me.json() as Profile);
      setWorkspaces(workspaceRows);
      if (workspaceRows[0]) {
        const summary = await fetch(
          `${apiBaseUrl}/api/v1/dashboard?workspaceId=${workspaceRows[0].id}`,
          { credentials: "include" }
        );
        if (summary.ok) setDashboard(await summary.json() as Dashboard);
      }
    };
    void load();
  }, []);

  if (error) {
    return (
      <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-5">
        {translate(locale, "errors.sessionExpired")}{" "}
        <Link className="underline" href="/login">
          {translate(locale, "errors.loginRequired")}
        </Link>
      </div>
    );
  }

  if (!profile) {
    return (
      <LoadingState
        title={translate(locale, "state.loading.title")}
        description={translate(locale, "state.loading.description")}
      />
    );
  }

  const workspace = workspaces[0];

  return (
    <div>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="section-label">{translate(locale, "nav.dashboard").toUpperCase()}</p>
          <h1 className="mt-2 text-2xl font-bold sm:text-3xl">
            {translateWithParams(locale, "dashboard.welcome", { name: profile.displayName })}
          </h1>
          <p className="mt-2 text-slate-600">
            {workspace ? `${workspace.name} · ${workspace.role}` : translate(locale, "dashboard.activeWorkspace")}
          </p>
        </div>
        {workspace && workspace.role !== "Viewer" && (
          <Link className="button-primary" href={`/upload?workspace=${workspace.id}`}>
            {translate(locale, "actions.upload")}
          </Link>
        )}
      </header>

      {!workspace && (
        <div className="mt-8">
          <EmptyState
            title={translate(locale, "dashboard.noWorkspace")}
            description={translate(locale, "dashboard.noWorkspaceDesc")}
          />
        </div>
      )}

      {dashboard && (
        <>
          <section aria-label={translate(locale, "dashboard.title")} className="mt-8 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Metric label={translate(locale, "dashboard.storage")} value={`${formatBytes(dashboard.storageBytes)} / ${formatBytes(dashboard.storageQuotaBytes)}`} />
            <Metric label={translate(locale, "dashboard.totalFiles")} value={dashboard.totalFiles.toLocaleString("id-ID")} />
            <Metric label={translate(locale, "dashboard.activeShares")} value={dashboard.activeShares.toLocaleString("id-ID")} />
            <Metric label={translate(locale, "dashboard.downloads7Days")} value={dashboard.downloadsLastSevenDays.toLocaleString("id-ID")} />
          </section>

          <section className="mt-5 rounded-2xl border border-slate-200 bg-white p-5">
            <div className="flex flex-wrap justify-between gap-2">
              <div>
                <h2 className="font-bold">{translate(locale, "dashboard.activity7Days")}</h2>
                <p className="text-sm text-slate-600">{translate(locale, "dashboard.activityDesc")}</p>
              </div>
              {(dashboard.quarantinedFiles > 0 || dashboard.processingFiles > 0) && (
                <p className="text-sm font-semibold text-amber-800">
                  {dashboard.processingFiles} {translate(locale, "dashboard.processingFiles")} · {dashboard.quarantinedFiles} {translate(locale, "dashboard.quarantinedFiles")}
                </p>
              )}
            </div>

            <div className="mt-5 grid grid-cols-7 gap-2" aria-hidden="true">
              {dashboard.activity.map(day => {
                const height = Math.max(day.uploads + day.downloads, 1) * 12;
                return (
                  <div className="flex flex-col items-center gap-2" key={day.date}>
                    <div className="flex h-24 items-end">
                      <span className="w-5 rounded-t bg-blue-700" style={{ height: `${Math.min(height, 96)}px` }} />
                    </div>
                    <span className="text-[10px] text-slate-500">
                      {new Date(day.date).toLocaleDateString("id-ID", { weekday: "short" })}
                    </span>
                  </div>
                );
              })}
            </div>

            <table className="sr-only" aria-label={translate(locale, "dashboard.activity7Days")}>
              <thead>
                <tr>
                  <th>{translate(locale, "nav.dashboard")}</th>
                  <th>Upload</th>
                  <th>Download</th>
                </tr>
              </thead>
              <tbody>
                {dashboard.activity.map(day => (
                  <tr key={day.date}>
                    <td>{new Date(day.date).toLocaleDateString("id-ID", { day: "2-digit", month: "short", year: "numeric" })}</td>
                    <td>{day.uploads}</td>
                    <td>{day.downloads}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            {dashboard.sharesExpiringSoon > 0 && (
              <p className="mt-4 rounded-lg bg-amber-50 p-3 text-sm text-amber-900">
                {translateWithParams(locale, "dashboard.sharesExpiringSoon", { count: dashboard.sharesExpiringSoon })}
              </p>
            )}
          </section>
        </>
      )}

      {workspace && (
        <section aria-label={translate(locale, "dashboard.quickActions")} className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Action href={`/files?workspace=${workspace.id}`} title={translate(locale, "actions.viewFiles")} copy={translate(locale, "files.title") + " - " + translate(locale, "actions.viewFiles")} />
          <Action href={`/shares?workspace=${workspace.id}`} title={translate(locale, "actions.viewShares")} copy={translate(locale, "shares.title")} />
          <Action href={`/shares/new?workspace=${workspace.id}`} title={translate(locale, "actions.createShare")} copy={translate(locale, "shares.emptyDesc")} />
          {workspace.role === "Owner" && <Action href={`/settings/workspace?workspace=${workspace.id}`} title={translate(locale, "actions.settings")} copy={translate(locale, "nav.workspace")} />}
          {(workspace.role === "Owner" || workspace.role === "Admin") && <Action href={`/workspace/members?workspace=${workspace.id}`} title={translate(locale, "actions.members")} copy={translate(locale, "nav.members")} />}
          <Action href="/workspace/invitations/accept" title={translate(locale, "actions.acceptInvitation")} copy={translate(locale, "actions.acceptInvitation")} />
        </section>
      )}
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <article className="rounded-2xl border border-slate-200 bg-white p-4">
      <p className="text-xs font-bold uppercase tracking-wide text-slate-500">{label}</p>
      <p className="mt-2 text-xl font-bold">{value}</p>
    </article>
  );
}

function Action({ href, title, copy }: { href: string; title: string; copy: string }) {
  return (
    <Link href={href} className="rounded-2xl border border-slate-200 bg-white p-5 transition hover:border-blue-300 hover:shadow-sm">
      <strong className="text-lg">{title}</strong>
      <span className="mt-2 block text-sm leading-6 text-slate-600">{copy}</span>
    </Link>
  );
}

function formatBytes(value: number) {
  if (value < 1024) return `${value} B`;
  if (value < 1024 ** 2) return `${(value / 1024).toFixed(1)} KB`;
  if (value < 1024 ** 3) return `${(value / 1024 ** 2).toFixed(1)} MB`;
  return `${(value / 1024 ** 3).toFixed(1)} GB`;
}
