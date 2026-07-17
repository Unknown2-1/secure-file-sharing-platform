import { describe, expect, it } from "vitest";
import { messages, translate, translateWithParams, availableLocales, getLocaleDisplayName, isSupportedLocale } from "@/lib/i18n/messages";

describe("message catalog", () => {
  it("keeps Indonesian and English catalogs structurally identical", () => {
    const idKeys = Object.keys(messages.id).sort();
    const enKeys = Object.keys(messages.en).sort();
    expect(enKeys).toEqual(idKeys);
  });

  it("uses Indonesian as the safe fallback for an unsupported locale", () => {
    expect(translate("id", "common.retry")).toBe("Coba lagi");
    expect(translate("en", "common.retry")).toBe("Try again");
    expect(translate("fr", "common.retry")).toBe("Coba lagi");
  });

  it("does not return raw message keys to the interface", () => {
    expect(translate("id", "state.loading.title")).not.toBe("state.loading.title");
  });

  it("provides interpolation support", () => {
    expect(translateWithParams("id", "dashboard.welcome", { name: "John" })).toBe("Selamat datang, John");
    expect(translateWithParams("en", "dashboard.welcome", { name: "John" })).toBe("Welcome, John");
  });

  it("provides safe fallback for missing keys", () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    expect(translate("id", "common.missing" as any)).toBe("common.missing");
  });

  it("has all required domain keys", () => {
    const requiredKeys = [
      "common.cancel",
      "common.confirm",
      "common.retry",
      "state.loading.title",
      "state.empty.title",
      "state.error.title",
      "dashboard.title",
      "files.title",
      "files.search",
      "files.empty",
      "shares.title",
      "shares.empty",
    ];
    requiredKeys.forEach(key => {
      expect(messages.id[key as keyof typeof messages.id]).toBeDefined();
      expect(messages.en[key as keyof typeof messages.en]).toBeDefined();
    });
  });
});

describe("locale utilities", () => {
  it("provides available locales", () => {
    expect(availableLocales).toContain("id");
    expect(availableLocales).toContain("en");
    expect(availableLocales).toHaveLength(2);
  });

  it("validates supported locales", () => {
    expect(isSupportedLocale("id")).toBe(true);
    expect(isSupportedLocale("en")).toBe(true);
    expect(isSupportedLocale("fr")).toBe(false);
    expect(isSupportedLocale(null)).toBe(false);
    expect(isSupportedLocale(undefined)).toBe(false);
  });

  it("provides display names", () => {
    expect(getLocaleDisplayName("id")).toBe("Indonesia");
    expect(getLocaleDisplayName("en")).toBe("English");
  });
});
