import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { FileList } from "@/components/file-list";
import { NotificationCenter } from "@/components/notification-center";
import { ShareList } from "@/components/share-list";

describe("collection states", () => {
  afterEach(() => vi.restoreAllMocks());

  it("announces share loading before the request completes", () => {
    vi.stubGlobal("fetch", vi.fn().mockReturnValue(new Promise(() => undefined)));
    render(<ShareList workspaceId="workspace-1" />);

    expect(screen.getByRole("status")).toHaveTextContent("Memuat share");
  });

  it("shows an explicit notification error instead of a blank list", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 503 }));
    render(<NotificationCenter />);

    expect(await screen.findByRole("alert")).toHaveTextContent("Notifikasi tidak dapat dimuat");
  });

  it("renders an actionable file empty state after loading", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ items: [] }) }));
    render(<FileList workspaceId="workspace-1" />);

    expect(await screen.findByRole("heading", { name: "Belum ada file yang cocok" })).toBeInTheDocument();
  });

  it("requires an in-page confirmation before soft-deleting a file", async () => {
    const mockedFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ items: [{ id: "file-1", filename: "laporan.txt", size: 42, availabilityStatus: "Available", detectedMimeType: "text/plain", createdAt: new Date().toISOString() }] }),
    });
    vi.stubGlobal("fetch", mockedFetch);
    vi.spyOn(window, "confirm").mockReturnValue(false);
    render(<FileList workspaceId="workspace-1" />);

    expect(await screen.findByText("laporan.txt")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Hapus" }));
    expect(screen.getByRole("alertdialog", { name: "Konfirmasi penghapusan file" })).toHaveTextContent("laporan.txt");
    fireEvent.click(screen.getByRole("button", { name: "Batal" }));
    expect(mockedFetch).toHaveBeenCalledTimes(1);
  });
});
