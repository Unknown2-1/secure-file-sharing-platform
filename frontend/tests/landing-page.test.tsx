import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import Home from "@/app/page";

describe("landing page", () => {
  it("offers clear registration and login paths in Indonesian", () => {
    render(<Home />);

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Bagikan file");
    expect(screen.getByRole("link", { name: "Mulai berbagi aman" })).toHaveAttribute("href", "/register");
    expect(screen.getByRole("link", { name: "Masuk" })).toHaveAttribute("href", "/login");
  });

  it("has a keyboard skip link and meaningful processing status", () => {
    render(<Home />);

    expect(screen.getByRole("link", { name: "Lewati ke konten utama" })).toHaveAttribute("href", "#content");
    expect(screen.getByLabelText("Ilustrasi alur pemrosesan file aman")).toBeInTheDocument();
    expect(screen.getAllByLabelText("Selesai")).toHaveLength(4);
  });
});
