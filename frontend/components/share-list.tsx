"use client";

import { useCallback, useEffect, useState } from "react";
import { apiBaseUrl, getCsrfToken } from "@/lib/api";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/async-state";
import { useLocale } from "@/lib/i18n/locale-context";
import { translate } from "@/lib/i18n/messages";

type Share = {
  id: string;
  name: string;
  expiresAt: string;
  downloadCount: number;
  maximumDownloads?: number;
  isRevoked: boolean;
  isPasswordProtected: boolean;
};

export function ShareList({ workspaceId }: { workspaceId: string }) {
  const { locale } = useLocale();
  const [shares, setShares] = useState<Share[]>([]);
  const [error, setError] = useState(false);
  const [loading, setLoading] = useState(true);
  const [revokingId, setRevokingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const response = await fetch(
        `${apiBaseUrl}/api/v1/shares?workspaceId=${workspaceId}`,
        { credentials: "include" }
      );
      if (!response.ok) { setError(true); return; }
      setShares(await response.json() as Share[]);
      setError(false);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, [workspaceId]);

  useEffect(() => {
    let active = true;
    void fetch(`${apiBaseUrl}/api/v1/shares?workspaceId=${workspaceId}`, { credentials: "include" })
      .then(async response => {
        if (!active) return;
        if (!response.ok) { setError(true); return; }
        setShares(await response.json() as Share[]);
        setError(false);
      })
      .catch(() => { if (active) setError(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [workspaceId]);

  async function revoke(id: string) {
    setRevokingId(id);
    try {
      const csrf = await getCsrfToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/shares/${id}/revoke`, {
        method: "POST",
        credentials: "include",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf,
          "Idempotency-Key": crypto.randomUUID(),
        },
        body: "{}",
      });
      if (!response.ok) { setError(true); return; }
      await load();
    } catch {
      setError(true);
    } finally {
      setRevokingId(null);
    }
  }

  if (loading) {
    return (
      <LoadingState
        title={translate(locale, "shares.loading")}
        description={translate(locale, "shares.loadingDesc")}
      />
    );
  }

  if (error) {
    return (
      <ErrorState
        title={translate(locale, "shares.error")}
        description={translate(locale, "shares.errorDesc")}
        onRetry={() => { setLoading(true); void load(); }}
      />
    );
  }

  if (shares.length === 0) {
    return (
      <EmptyState
        title={translate(locale, "shares.empty")}
        description={translate(locale, "shares.emptyDesc")}
      />
    );
  }

  return (
    <ul className="space-y-3">
      {shares.map((share) => (
        <li className="rounded-xl border border-slate-200 bg-white p-5" key={share.id}>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0 flex-1">
              <h2 className="font-bold">{share.name}</h2>
              <p className="mt-1 text-sm text-slate-500">
                {share.downloadCount}
                {share.maximumDownloads ? ` / ${share.maximumDownloads}` : ""}{" "}
                {translate(locale, "shares.downloadCount").replace("{current}", "").replace("{max}", "").trim()} ·{" "}
                {translate(locale, "shares.expiresAt").replace("{date}", "")}{" "}
                {new Date(share.expiresAt).toLocaleString("id-ID")}
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-bold whitespace-nowrap">
                {share.isRevoked
                  ? translate(locale, "shares.statusRevoked")
                  : share.isPasswordProtected
                    ? translate(locale, "shares.statusPassword")
                    : translate(locale, "shares.statusActive")}
              </span>
              {!share.isRevoked && (
                <button
                  type="button"
                  className="button-secondary"
                  disabled={revokingId === share.id}
                  onClick={() => void revoke(share.id)}
                >
                  {revokingId === share.id
                    ? translate(locale, "common.processing")
                    : translate(locale, "shares.revoke")}
                </button>
              )}
            </div>
          </div>
        </li>
      ))}
    </ul>
  );
}
