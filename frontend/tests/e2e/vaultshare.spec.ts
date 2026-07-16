import { expect, test, type Page } from "@playwright/test";

const demoPassword = "ChangeMe123!";

test.describe.configure({ mode: "serial" });

test("registrasi menampilkan instruksi verifikasi tanpa enumerasi akun", async ({ page }) => {
  await page.goto("/register");
  await page.getByLabel("Nama tampilan").fill("Pengguna E2E");
  await page.getByLabel("Email").fill(`e2e-${Date.now()}@example.com`);
  await page.getByLabel("Password").fill("ValidPass123!");
  await page.getByRole("button", { name: "Daftar" }).click();
  await expect(page.getByRole("status")).toContainText("Akun dibuat");
});

test("upload, password share, batas download, dan revoke bekerja lintas browser", async ({ page, browser }) => {
  await login(page, "owner@example.com", demoPassword);
  const workspaceId = await firstWorkspaceId(page);
  const filename = `e2e-${Date.now()}.txt`;
  const original = `VaultShare Playwright fixture ${Date.now()}\n`;

  await page.goto(`/upload?workspace=${workspaceId}`);
  await page.locator('input[type="file"]').setInputFiles({
    name: filename,
    mimeType: "text/plain",
    buffer: Buffer.from(original),
  });
  await page.getByRole("button", { name: "Unggah" }).click();
  await expect(page.getByText(/menunggu pemeriksaan keamanan/)).toBeVisible();
  await waitUntilAvailable(page, workspaceId, filename);

  const shareName = `Share E2E ${Date.now()}`;
  await page.goto(`/shares/new?workspace=${workspaceId}`);
  await page.getByText(filename, { exact: true }).click();
  await page.getByLabel("2. Nama share").fill(shareName);
  await page.getByLabel("Password opsional").fill("SharePass123!");
  await page.getByLabel("Batas download").fill("2");
  await page.getByRole("button", { name: "Buat share" }).click();
  const shareUrl = await page.getByLabel("Link share").inputValue();

  const recipient = await browser.newContext();
  const publicPage = await recipient.newPage();
  await publicPage.goto(shareUrl);
  await publicPage.getByLabel("Password share").fill("password-salah");
  await publicPage.getByRole("button", { name: "Lanjutkan" }).click();
  await expect(publicPage.getByRole("alert")).toBeVisible();
  await publicPage.getByLabel("Password share").fill("SharePass123!");
  await publicPage.getByRole("button", { name: "Lanjutkan" }).click();
  await expect(publicPage.getByText(filename)).toBeVisible();

  const downloadUrl = expectString(await publicPage.getByRole("link", { name: "Unduh" }).getAttribute("href"));
  const first = await recipient.request.get(downloadUrl);
  expect(first.status()).toBe(200);
  expect((await first.body()).toString()).toBe(original);
  const second = await recipient.request.get(downloadUrl);
  expect(second.status()).toBe(200);
  const third = await recipient.request.get(downloadUrl);
  expect(third.status()).toBe(403);

  await page.goto(`/shares?workspace=${workspaceId}`);
  const shareRow = page.getByRole("listitem").filter({ hasText: shareName });
  await shareRow.getByRole("button", { name: "Cabut" }).click();
  await expect(shareRow).toContainText("Dicabut");
  const afterRevoke = await browser.newContext();
  const revokedPage = await afterRevoke.newPage();
  await revokedPage.goto(shareUrl);
  await revokedPage.getByLabel("Password share").fill("SharePass123!");
  await revokedPage.getByRole("button", { name: "Lanjutkan" }).click();
  await expect(revokedPage.getByRole("alert")).toBeVisible();
  await afterRevoke.close();
  await recipient.close();
});

test("viewer ditolak ketika mencoba upload", async ({ page }) => {
  await login(page, "viewer@example.com", demoPassword);
  const workspaceId = await firstWorkspaceId(page);
  await page.goto(`/upload?workspace=${workspaceId}`);
  await page.locator('input[type="file"]').setInputFiles({
    name: "viewer-denied.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("viewer cannot upload"),
  });
  await page.getByRole("button", { name: "Unggah" }).click();
  await expect(page.getByText("Upload terhenti. Coba lanjutkan kembali.")).toBeVisible();
});

test("dashboard tetap usable pada viewport mobile", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page, "member@example.com", demoPassword);
  await expect(page.getByRole("heading", { name: /Selamat datang/ })).toBeVisible();
  await expect(page.getByRole("link", { name: "Notifikasi" })).toBeVisible();
});

async function login(page: Page, email: string, password: string) {
  await page.goto("/login");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Masuk" }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}

async function firstWorkspaceId(page: Page): Promise<string> {
  return page.evaluate(async () => {
    const response = await fetch("/api/v1/workspaces", { credentials: "include" });
    if (!response.ok) throw new Error("workspace_unavailable");
    const rows = await response.json() as { id: string }[];
    if (!rows[0]) throw new Error("workspace_missing");
    return rows[0].id;
  });
}

async function waitUntilAvailable(page: Page, workspaceId: string, filename: string) {
  await expect.poll(async () => page.evaluate(async ({ id, name }) => {
    const response = await fetch(`/api/v1/files?workspaceId=${id}&search=${encodeURIComponent(name)}`, { credentials: "include" });
    if (!response.ok) return "Unavailable";
    const body = await response.json() as { items: { filename: string; availabilityStatus: string }[] };
    return body.items.find(item => item.filename === name)?.availabilityStatus ?? "Missing";
  }, { id: workspaceId, name: filename }), { timeout: 90_000 }).toBe("Available");
}

function expectString(value: string | null): string {
  expect(value).not.toBeNull();
  return value as string;
}
