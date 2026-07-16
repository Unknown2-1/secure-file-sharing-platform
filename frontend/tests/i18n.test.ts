import { describe, expect, it } from "vitest";
import { messages, translate } from "@/lib/i18n/messages";

describe("message catalog", () => {
  it("keeps Indonesian and English catalogs structurally identical", () => {
    expect(Object.keys(messages.en).sort()).toEqual(Object.keys(messages.id).sort());
  });

  it("uses Indonesian as the safe fallback for an unsupported locale", () => {
    expect(translate("id", "common.retry")).toBe("Coba lagi");
    expect(translate("en", "common.retry")).toBe("Try again");
    expect(translate("fr", "common.retry")).toBe("Coba lagi");
  });

  it("does not return raw message keys to the interface", () => {
    expect(translate("id", "state.loading.title")).not.toBe("state.loading.title");
  });
});
