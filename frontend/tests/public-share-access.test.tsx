import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PublicShareAccess } from "@/components/public-share-access";

describe("PublicShareAccess", () => {
  it("uses a labeled password input and generic access copy", () => {
    render(<PublicShareAccess publicIdentifier="public" secretToken="secret" />);
    expect(screen.getByRole("heading", { name: "Buka file yang dibagikan" })).toBeInTheDocument();
    const password = screen.getByLabelText("Password share");
    fireEvent.change(password, { target: { value: "different-channel-password" } });
    expect(password).toHaveValue("different-channel-password");
    expect(screen.getByRole("button", { name: "Lanjutkan" })).toBeEnabled();
  });
});
