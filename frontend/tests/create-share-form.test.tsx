import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { CreateShareForm } from "@/components/create-share-form";

describe("CreateShareForm", () => {
  afterEach(() => vi.restoreAllMocks());

  it("memilih hanya file available dan menerapkan batas input keamanan", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ items: [{ id: "file-1", filename: "aman.txt", availabilityStatus: "Available" }] }),
    }));
    render(<CreateShareForm workspaceId="workspace-1" />);

    expect(await screen.findByText("aman.txt")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Buat share" })).toBeDisabled();
    expect(screen.getByLabelText("Kedaluwarsa")).toHaveAttribute("min");
    expect(screen.getByLabelText("Password opsional")).toHaveAttribute("minlength", "8");
    expect(screen.getByLabelText("Batas download")).toHaveAttribute("min", "1");
  });

  it("menampilkan state kosong saat API tidak mengembalikan file", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ items: [] }) }));
    render(<CreateShareForm workspaceId="workspace-1" />);
    await waitFor(() => expect(fetch).toHaveBeenCalled());
    expect(screen.queryByRole("checkbox", { name: /\.txt/ })).not.toBeInTheDocument();
    expect(await screen.findByRole("heading", { name: "Tidak ada file yang siap dibagikan" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Buat share" })).toBeDisabled();
  });
});
