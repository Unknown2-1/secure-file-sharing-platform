import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/async-state";

describe("async states", () => {
  it("announces loading without taking focus", () => {
    render(<LoadingState />);

    expect(screen.getByRole("status")).toHaveTextContent("Memuat…");
    expect(screen.getByRole("status")).toHaveAttribute("aria-live", "polite");
  });

  it("offers a useful empty-state action", () => {
    const onAction = vi.fn();
    render(<EmptyState title="Belum ada file" description="Unggah file pertama Anda." actionLabel="Unggah file" onAction={onAction} />);

    fireEvent.click(screen.getByRole("button", { name: "Unggah file" }));
    expect(onAction).toHaveBeenCalledOnce();
  });

  it("announces failures and exposes a retry control", () => {
    const onRetry = vi.fn();
    render(<ErrorState correlationId="corr-123" onRetry={onRetry} />);

    expect(screen.getByRole("alert")).toHaveTextContent("Correlation ID: corr-123");
    fireEvent.click(screen.getByRole("button", { name: "Coba lagi" }));
    expect(onRetry).toHaveBeenCalledOnce();
  });
});
