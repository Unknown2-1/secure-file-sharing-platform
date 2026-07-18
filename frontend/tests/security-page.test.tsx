import { render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import SecurityInfoPage from "@/app/security/page";

test("security page publishes the repository owner's real contact", () => {
  render(<SecurityInfoPage />);

  expect(screen.getByText(/dnshadelio@gmail\.com/)).toBeInTheDocument();
  expect(screen.queryByText(/security@example\.com/)).not.toBeInTheDocument();
});
