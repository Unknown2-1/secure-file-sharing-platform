import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { UploadDropzone } from "@/components/upload-dropzone";

describe("UploadDropzone", () => {
  it("accepts multiple safe files and exposes accessible progress", () => {
    render(<UploadDropzone workspaceId="00000000-0000-0000-0000-000000000001" />);
    const input = screen.getByLabelText(/Tarik file ke sini/i);
    fireEvent.change(input, { target: { files: [new File(["hello"], "catatan.txt", { type: "text/plain" }), new File(["image"], "foto.png", { type: "image/png" })] } });

    expect(screen.getByText("2 file siap diunggah.")).toBeInTheDocument();
    expect(screen.getByText("catatan.txt")).toBeInTheDocument();
    expect(screen.getByRole("progressbar", { name: "Progress foto.png" })).toHaveAttribute("aria-valuenow", "0");
  });

  it("rejects unsafe extensions before contacting the API", () => {
    render(<UploadDropzone workspaceId="00000000-0000-0000-0000-000000000001" />);
    fireEvent.change(screen.getByLabelText(/Tarik file ke sini/i), { target: { files: [new File(["x"], "payload.html", { type: "text/html" })] } });
    expect(screen.getByText("0 file ditambahkan, 1 ditolak.")).toBeInTheDocument();
    expect(screen.queryByText("payload.html")).not.toBeInTheDocument();
  });
});
