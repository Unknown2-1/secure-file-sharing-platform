import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { DashboardShell } from "@/components/dashboard-shell";

describe("DashboardShell", () => {
  afterEach(() => vi.restoreAllMocks());

  it("explains how to continue when the account has no workspace", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: string | URL) => String(input).endsWith("/auth/me")
      ? { ok: true, json: async () => ({ displayName: "Owner", email: "owner@example.com" }) }
      : { ok: true, json: async () => [] }));
    render(<DashboardShell />);

    expect(await screen.findByRole("heading", { name: "Belum ada workspace" })).toBeInTheDocument();
  });

  it("provides a tabular alternative for the activity chart", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: string | URL) => {
      const url = String(input);
      if (url.endsWith("/auth/me")) return { ok: true, json: async () => ({ displayName: "Owner", email: "owner@example.com" }) };
      if (url.endsWith("/workspaces")) return { ok: true, json: async () => [{ id: "workspace-1", name: "Tim", role: "Owner" }] };
      return { ok: true, json: async () => ({ storageBytes: 1, storageQuotaBytes: 100, totalFiles: 1, activeShares: 1, downloadsLastSevenDays: 3, processingFiles: 0, quarantinedFiles: 0, sharesExpiringSoon: 0, activity: [{ date: "2026-07-16", uploads: 2, downloads: 3 }] }) };
    }));
    render(<DashboardShell />);

    const table = await screen.findByRole("table", { name: "Data aktivitas 7 hari" });
    expect(table).toHaveTextContent("16 Jul 2026");
    expect(table).toHaveTextContent("2");
    expect(table).toHaveTextContent("3");
  });
});
