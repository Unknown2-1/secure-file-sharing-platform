import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AccountCodeForm } from "@/components/account-code-form";

describe("AccountCodeForm", () => {
  afterEach(() => vi.restoreAllMocks());

  it("disables submission and announces progress while a request is active", async () => {
    let finishRequest: ((value: { ok: boolean; status: number }) => void) | undefined;
    const pendingRequest = new Promise<{ ok: boolean; status: number }>((resolve) => { finishRequest = resolve; });
    const mockedFetch = vi.fn()
      .mockResolvedValueOnce({ ok: true, json: async () => ({ requestToken: "csrf" }) })
      .mockReturnValueOnce(pendingRequest);
    vi.stubGlobal("fetch", mockedFetch);
    render(<AccountCodeForm mode="forgot" />);

    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "owner@example.com" } });
    fireEvent.click(screen.getByRole("button", { name: "Kirim" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "Memproses…" })).toBeDisabled());
    finishRequest?.({ ok: true, status: 202 });
    await screen.findByText("Jika akun memenuhi syarat, kode reset telah dikirim.");
  });
});
