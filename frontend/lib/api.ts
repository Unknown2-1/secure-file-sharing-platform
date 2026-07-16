export const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";

export async function getCsrfToken(signal?: AbortSignal): Promise<string> {
  const response = await fetch(`${apiBaseUrl}/api/v1/auth/csrf`, {
    credentials: "include",
    signal,
  });
  if (!response.ok) throw new Error("csrf_unavailable");
  const body = (await response.json()) as { requestToken: string };
  return body.requestToken;
}
