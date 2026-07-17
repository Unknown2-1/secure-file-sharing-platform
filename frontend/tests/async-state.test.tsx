import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/async-state";
import { LocaleProvider, useLocale } from "@/lib/i18n/locale-context";
import { ReactNode } from "react";

// Wrapper for testing components that use locale context
function renderWithLocale(children: ReactNode) {
  return render(<LocaleProvider>{children}</LocaleProvider>);
}

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

  it("renders with custom titles", () => {
    render(<LoadingState title="Custom loading" description="Please wait..." />);
    expect(screen.getByRole("status")).toHaveTextContent("Custom loading");
  });
});

describe("locale context", () => {
  it("provides default Indonesian locale", () => {
    function TestComponent() {
      const { locale } = useLocale();
      return <span data-testid="locale">{locale}</span>;
    }
    renderWithLocale(<TestComponent />);
    expect(screen.getByTestId("locale")).toHaveTextContent("id");
  });

  it("loads locale from cookie", () => {
    // Set cookie before rendering
    document.cookie = "vaultshare_locale=en; path=/; max-age=31536000";

    function TestComponent() {
      const { locale } = useLocale();
      return <span data-testid="locale">{locale}</span>;
    }
    renderWithLocale(<TestComponent />);

    // Wait for hydration
    return new Promise(resolve => setTimeout(() => {
      expect(screen.getByTestId("locale")).toHaveTextContent("en");
      // Clean up cookie
      document.cookie = "vaultshare_locale=; path=/; max-age=0";
      resolve(undefined);
    }, 100));
  });
});
