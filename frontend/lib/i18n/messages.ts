const indonesian = {
  "common.cancel": "Batal",
  "common.confirm": "Konfirmasi",
  "common.processing": "Memproses…",
  "common.retry": "Coba lagi",
  "state.empty.description": "Belum ada data untuk ditampilkan.",
  "state.empty.title": "Belum ada data",
  "state.error.description": "Permintaan tidak dapat diselesaikan. Coba lagi atau gunakan correlation ID saat menghubungi dukungan.",
  "state.error.title": "Terjadi kendala",
  "state.loading.description": "Data sedang dimuat dengan aman.",
  "state.loading.title": "Memuat…",
  "workspace.members.empty": "Belum ada anggota tambahan di workspace ini.",
  "workspace.members.remove.cancel": "Batal",
  "workspace.members.remove.confirm": "Hapus anggota",
  "workspace.members.remove.description": "Akses anggota ke workspace akan langsung dicabut. Tindakan ini dicatat dalam audit log.",
  "workspace.members.remove.title": "Konfirmasi penghapusan anggota",
} as const;

export type MessageKey = keyof typeof indonesian;
export type Locale = "id" | "en";

const english: Record<MessageKey, string> = {
  "common.cancel": "Cancel",
  "common.confirm": "Confirm",
  "common.processing": "Processing…",
  "common.retry": "Try again",
  "state.empty.description": "There is no data to display yet.",
  "state.empty.title": "No data yet",
  "state.error.description": "The request could not be completed. Try again or provide the correlation ID when contacting support.",
  "state.error.title": "Something went wrong",
  "state.loading.description": "Data is loading securely.",
  "state.loading.title": "Loading…",
  "workspace.members.empty": "There are no additional members in this workspace yet.",
  "workspace.members.remove.cancel": "Cancel",
  "workspace.members.remove.confirm": "Remove member",
  "workspace.members.remove.description": "The member's workspace access will be revoked immediately. This action is recorded in the audit log.",
  "workspace.members.remove.title": "Confirm member removal",
};

export const messages = Object.freeze({ id: indonesian, en: Object.freeze(english) });

export function translate(locale: string | null | undefined, key: MessageKey): string {
  const supportedLocale: Locale = locale === "en" ? "en" : "id";
  return messages[supportedLocale][key];
}
