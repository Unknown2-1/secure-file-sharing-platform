import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SecuritySettings } from "@/components/security-settings";

describe("SecuritySettings", () => {
  afterEach(() => vi.restoreAllMocks());

  it("announces session loading instead of rendering a blank section", () => {
    vi.stubGlobal("fetch", vi.fn().mockReturnValue(new Promise(() => undefined)));
    render(<SecuritySettings />);

    expect(screen.getByRole("status")).toHaveTextContent("Memuat sesi aktif");
  });
});
