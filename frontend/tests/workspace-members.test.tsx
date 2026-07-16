import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { WorkspaceMembers } from "@/components/workspace-members";

describe("WorkspaceMembers", () => {
  afterEach(() => vi.restoreAllMocks());

  it("announces member loading before the request completes", () => {
    vi.stubGlobal("fetch", vi.fn().mockReturnValue(new Promise(() => undefined)));
    render(<WorkspaceMembers workspaceId="workspace-1" />);

    expect(screen.getByRole("status")).toHaveTextContent("Memuat anggota");
  });

  it("requires an accessible in-page confirmation before removing a member", async () => {
    const mockedFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [{ userId: "member-1", email: "member@example.com", displayName: "Member Demo", role: "Member", joinedAt: new Date().toISOString() }],
    });
    vi.stubGlobal("fetch", mockedFetch);
    vi.spyOn(window, "confirm").mockReturnValue(false);
    render(<WorkspaceMembers workspaceId="workspace-1" />);

    expect(await screen.findByText("Member Demo")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Hapus" }));

    expect(screen.getByRole("alertdialog", { name: "Konfirmasi penghapusan anggota" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Batal" }));
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
    expect(mockedFetch).toHaveBeenCalledTimes(1);
  });
});
