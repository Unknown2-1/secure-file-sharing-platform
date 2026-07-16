import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { NotificationCenter } from "@/components/notification-center";

describe("NotificationCenter", () => {
  afterEach(() => vi.restoreAllMocks());

  it("memuat notifikasi dan menandainya dibaca", async () => {
    const mockedFetch = vi.fn(async (input: string | URL) => {
      const url = String(input);
      if (url.endsWith("/auth/csrf")) return { ok: true, json: async () => ({ requestToken: "csrf" }) };
      if (url.endsWith("/read")) return { ok: true };
      return { ok: true, json: async () => [{ id: "n1", type: "FileAvailable", title: "File siap", message: "File terenkripsi tersedia.", createdAt: new Date().toISOString() }] };
    });
    vi.stubGlobal("fetch", mockedFetch);
    render(<NotificationCenter />);

    expect(await screen.findByText("File siap")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Tandai dibaca" }));
    await screen.findByText("File terenkripsi tersedia.");
    expect(mockedFetch).toHaveBeenCalledTimes(3);
  });
});
