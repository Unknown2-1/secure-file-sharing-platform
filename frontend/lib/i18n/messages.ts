// Localization messages for Indonesian (default) and English
// Each domain contains related message keys for a specific feature area

const indonesian = {
  // Common UI elements
  "common.cancel": "Batal",
  "common.confirm": "Konfirmasi",
  "common.processing": "Memproses…",
  "common.retry": "Coba lagi",
  "common.close": "Tutup",
  "common.save": "Simpan",
  "common.delete": "Hapus",
  "common.edit": "Edit",
  "common.view": "Lihat",
  "common.download": "Unduh",
  "common.back": "Kembali",

  // Navigation
  "nav.dashboard": "Dashboard",
  "nav.files": "File",
  "nav.shares": "Share",
  "nav.upload": "Upload",
  "nav.settings": "Pengaturan",
  "nav.notifications": "Notifikasi",
  "nav.security": "Keamanan akun",
  "nav.workspace": "Workspace",
  "nav.members": "Anggota",
  "nav.invitations": "Undangan",
  "nav.privacy": "Privasi",
  "nav.logout": "Keluar",

  // Async states
  "state.empty.description": "Belum ada data untuk ditampilkan.",
  "state.empty.title": "Belum ada data",
  "state.error.description": "Permintaan tidak dapat diselesaikan. Coba lagi atau gunakan correlation ID saat menghubungi dukungan.",
  "state.error.title": "Terjadi kendala",
  "state.loading.description": "Data sedang dimuat dengan aman.",
  "state.loading.title": "Memuat…",

  // Dashboard
  "dashboard.title": "Dashboard",
  "dashboard.welcome": "Selamat datang, {name}",
  "dashboard.activeWorkspace": "Workspace aktif",
  "dashboard.noWorkspace": "Belum ada workspace",
  "dashboard.noWorkspaceDesc": "Terima undangan workspace atau buat workspace melalui API untuk mulai mengelola file.",
  "dashboard.storage": "Penyimpanan",
  "dashboard.totalFiles": "Total file",
  "dashboard.activeShares": "Share aktif",
  "dashboard.downloads7Days": "Download 7 hari",
  "dashboard.activity7Days": "Aktivitas 7 hari",
  "dashboard.activityDesc": "Upload dan download harian.",
  "dashboard.processingFiles": "diproses",
  "dashboard.quarantinedFiles": "dikarantina",
  "dashboard.sharesExpiringSoon": "{count} share akan kedaluwarsa dalam 7 hari.",
  "dashboard.quickActions": "Aksi cepat",

  // Files
  "files.title": "File saya",
  "files.search": "Cari file",
  "files.searchPlaceholder": "Nama file",
  "files.empty": "Belum ada file",
  "files.emptyDesc": "Unggah file pertama agar dapat diproses dan dibagikan.",
  "files.emptySearch": "Belum ada file yang cocok",
  "files.emptySearchDesc": "Ubah kata pencarian atau hapus filter.",
  "files.loading": "Memuat file…",
  "files.loadingDesc": "Metadata file sedang dimuat dari workspace.",
  "files.error": "File tidak dapat dimuat",
  "files.errorDesc": "Operasi file tidak dapat diproses. Periksa koneksi dan coba lagi.",
  "files.confirmDelete": "Konfirmasi penghapusan file",
  "files.confirmDeleteDesc": "Pindahkan {filename} ke file terhapus? Share terkait akan ditolak sesuai lifecycle backend.",
  "files.deleteFile": "Hapus file",
  "files.restore": "Pulihkan",
  "files.preview": "Preview",
  "files.internalAccess": "Berikan akses internal",
  "files.emailMember": "Email anggota",
  "files.emailPlaceholder": "anggota@example.com",
  "files.permission": "Izin",
  "files.grant": "Berikan",
  "files.revoke": "Cabut",
  "files.revoked": "Dicabut",
  "files.granted": "Akses internal berhasil diberikan.",
  "files.grantError": "Akses tidak dapat diberikan. Pastikan penerima anggota workspace dan Anda memiliki izin.",
  "files.grantAction": "Muat akses saat ini",
  "files.status": "Status",

  // Shares
  "shares.title": "Share",
  "shares.create": "Buat share",
  "shares.search": "Cari share",
  "shares.empty": "Belum ada share",
  "shares.emptyDesc": "Buat share dari file yang sudah berstatus Available.",
  "shares.loading": "Memuat share…",
  "shares.loadingDesc": "Kebijakan dan statistik akses sedang dimuat.",
  "shares.error": "Share tidak dapat dimuat",
  "shares.errorDesc": "Daftar share tidak tersedia. Coba lagi tanpa membagikan informasi sensitif.",
  "shares.revoke": "Cabut",
  "shares.statusActive": "Aktif",
  "shares.statusRevoked": "Dicabut",
  "shares.statusPassword": "Berpassword",
  "shares.downloadCount": "{current}{max} download",
  "shares.expiresAt": "berakhir {date}",

  // Upload
  "upload.title": "Upload file",
  "upload.selectFiles": "Pilih file",
  "upload.dropHere": "Seret file ke sini",
  "upload.orClick": "atau klik untuk memilih",
  "upload.uploading": "Mengunggah…",
  "upload.complete": "Upload selesai",
  "upload.failed": "Upload gagal",
  "upload.scanning": "Memindai malware…",
  "upload.encrypting": "Mengenkripsi…",

  // Status
  "status.available": "Tersedia",
  "status.processing": "Diproses",
  "status.quarantined": "Dikarantina",
  "status.deleted": "Dihapus",

  // Actions
  "actions.upload": "Upload file",
  "actions.createShare": "Buat share",
  "actions.viewFiles": "File saya",
  "actions.viewShares": "Share",
  "actions.settings": "Kebijakan",
  "actions.members": "Anggota",
  "actions.acceptInvitation": "Terima undangan",
  "actions.copyLink": "Salin tautan",
  "actions.linkCopied": "Tautan berhasil disalin",

  // Validation
  "validation.required": "Kolom ini wajib diisi.",
  "validation.email": "Alamat email tidak valid.",
  "validation.passwordMin": "Password minimal 8 karakter.",
  "validation.passwordMatch": "Password tidak cocok.",

  // Errors
  "errors.sessionExpired": "Sesi tidak tersedia.",
  "errors.loginRequired": "Silakan masuk kembali.",
  "errors.workspaceRequired": "Workspace belum dipilih.",
  "errors.genericError": "Terjadi kesalahan. Silakan coba lagi.",
  "errors.networkError": "Tidak dapat terhubung ke server.",
  "errors.notFound": "Halaman tidak ditemukan.",
  "errors.forbidden": "Anda tidak memiliki izin untuk mengakses resource ini.",
  "errors.rateLimit": "Terlalu banyak permintaan. Silakan tunggu sebentar.",

  // Accessibility
  "accessibility.skipToContent": "Langsung ke konten",
  "accessibility.menu": "Menu navigasi",
  "accessibility.closeMenu": "Tutup menu",
  "accessibility.loading": "Memuat data, silakan tunggu.",
  "accessibility.error": "Terjadi kesalahan pada halaman.",
  "accessibility.emptyList": "Daftar kosong.",
  "accessibility.selected": "Dipilih",
  "accessibility.expanded": "Diperluas",
  "accessibility.collapsed": "Ditutup",

  // Notifications
  "notifications.title": "Notifikasi",
  "notifications.empty": "Tidak ada notifikasi",
  "notifications.emptyDesc": "Notifikasi akan muncul di sini.",
  "notifications.markAllRead": "Tandai semua dibaca",
  "notifications.unread": "Belum dibaca",

  // Workspace
  "workspace.members.empty": "Belum ada anggota tambahan di workspace ini.",
  "workspace.members.remove.cancel": "Batal",
  "workspace.members.remove.confirm": "Hapus anggota",
  "workspace.members.remove.description": "Akses anggota ke workspace akan langsung dicabut. Tindakan ini dicatat dalam audit log.",
  "workspace.members.remove.title": "Konfirmasi penghapusan anggota",
  "workspace.role.owner": "Pemilik",
  "workspace.role.admin": "Admin",
  "workspace.role.member": "Anggota",
  "workspace.role.viewer": "Penonton",

  // Language
  "language.switch": "Ganti bahasa",
  "language.indonesian": "Indonesia",
  "language.english": "English",
} as const;

// English translations
const english: Record<string, string> = {
  // Common UI elements
  "common.cancel": "Cancel",
  "common.confirm": "Confirm",
  "common.processing": "Processing…",
  "common.retry": "Try again",
  "common.close": "Close",
  "common.save": "Save",
  "common.delete": "Delete",
  "common.edit": "Edit",
  "common.view": "View",
  "common.download": "Download",
  "common.back": "Back",

  // Navigation
  "nav.dashboard": "Dashboard",
  "nav.files": "Files",
  "nav.shares": "Shares",
  "nav.upload": "Upload",
  "nav.settings": "Settings",
  "nav.notifications": "Notifications",
  "nav.security": "Account Security",
  "nav.workspace": "Workspace",
  "nav.members": "Members",
  "nav.invitations": "Invitations",
  "nav.privacy": "Privacy",
  "nav.logout": "Logout",

  // Async states
  "state.empty.description": "There is no data to display yet.",
  "state.empty.title": "No data yet",
  "state.error.description": "The request could not be completed. Try again or provide the correlation ID when contacting support.",
  "state.error.title": "Something went wrong",
  "state.loading.description": "Data is loading securely.",
  "state.loading.title": "Loading…",

  // Dashboard
  "dashboard.title": "Dashboard",
  "dashboard.welcome": "Welcome, {name}",
  "dashboard.activeWorkspace": "Active workspace",
  "dashboard.noWorkspace": "No workspace yet",
  "dashboard.noWorkspaceDesc": "Accept a workspace invitation or create one via the API to start managing files.",
  "dashboard.storage": "Storage",
  "dashboard.totalFiles": "Total files",
  "dashboard.activeShares": "Active shares",
  "dashboard.downloads7Days": "Downloads (7 days)",
  "dashboard.activity7Days": "Activity (7 days)",
  "dashboard.activityDesc": "Daily uploads and downloads.",
  "dashboard.processingFiles": "processing",
  "dashboard.quarantinedFiles": "quarantined",
  "dashboard.sharesExpiringSoon": "{count} share(s) will expire within 7 days.",
  "dashboard.quickActions": "Quick Actions",

  // Files
  "files.title": "My Files",
  "files.search": "Search files",
  "files.searchPlaceholder": "File name",
  "files.empty": "No files yet",
  "files.emptyDesc": "Upload your first file to process and share.",
  "files.emptySearch": "No matching files",
  "files.emptySearchDesc": "Change your search term or clear filters.",
  "files.loading": "Loading files…",
  "files.loadingDesc": "File metadata is being loaded from workspace.",
  "files.error": "Unable to load files",
  "files.errorDesc": "File operations cannot be processed. Check your connection and try again.",
  "files.confirmDelete": "Confirm file deletion",
  "files.confirmDeleteDesc": "Move {filename} to deleted files? Related shares will be rejected according to backend lifecycle.",
  "files.deleteFile": "Delete file",
  "files.restore": "Restore",
  "files.preview": "Preview",
  "files.internalAccess": "Grant internal access",
  "files.emailMember": "Member email",
  "files.emailPlaceholder": "member@example.com",
  "files.permission": "Permission",
  "files.grant": "Grant",
  "files.revoke": "Revoke",
  "files.revoked": "Revoked",
  "files.granted": "Internal access granted successfully.",
  "files.grantError": "Access could not be granted. Ensure the recipient is a workspace member and you have permission.",
  "files.grantAction": "Load current access",
  "files.status": "Status",

  // Shares
  "shares.title": "Shares",
  "shares.create": "Create share",
  "shares.search": "Search shares",
  "shares.empty": "No shares yet",
  "shares.emptyDesc": "Create a share from files with Available status.",
  "shares.loading": "Loading shares…",
  "shares.loadingDesc": "Access policies and statistics are being loaded.",
  "shares.error": "Unable to load shares",
  "shares.errorDesc": "Share list is not available. Try again without sharing sensitive information.",
  "shares.revoke": "Revoke",
  "shares.statusActive": "Active",
  "shares.statusRevoked": "Revoked",
  "shares.statusPassword": "Password protected",
  "shares.downloadCount": "{current}{max} downloads",
  "shares.expiresAt": "expires {date}",

  // Upload
  "upload.title": "Upload file",
  "upload.selectFiles": "Select files",
  "upload.dropHere": "Drop files here",
  "upload.orClick": "or click to select",
  "upload.uploading": "Uploading…",
  "upload.complete": "Upload complete",
  "upload.failed": "Upload failed",
  "upload.scanning": "Scanning for malware…",
  "upload.encrypting": "Encrypting…",

  // Status
  "status.available": "Available",
  "status.processing": "Processing",
  "status.quarantined": "Quarantined",
  "status.deleted": "Deleted",

  // Actions
  "actions.upload": "Upload file",
  "actions.createShare": "Create share",
  "actions.viewFiles": "My Files",
  "actions.viewShares": "Shares",
  "actions.settings": "Policies",
  "actions.members": "Members",
  "actions.acceptInvitation": "Accept invitation",
  "actions.copyLink": "Copy link",
  "actions.linkCopied": "Link copied successfully",

  // Validation
  "validation.required": "This field is required.",
  "validation.email": "Invalid email address.",
  "validation.passwordMin": "Password must be at least 8 characters.",
  "validation.passwordMatch": "Passwords do not match.",

  // Errors
  "errors.sessionExpired": "Session unavailable.",
  "errors.loginRequired": "Please log in again.",
  "errors.workspaceRequired": "No workspace selected.",
  "errors.genericError": "An error occurred. Please try again.",
  "errors.networkError": "Unable to connect to server.",
  "errors.notFound": "Page not found.",
  "errors.forbidden": "You do not have permission to access this resource.",
  "errors.rateLimit": "Too many requests. Please wait a moment.",

  // Accessibility
  "accessibility.skipToContent": "Skip to content",
  "accessibility.menu": "Navigation menu",
  "accessibility.closeMenu": "Close menu",
  "accessibility.loading": "Loading data, please wait.",
  "accessibility.error": "An error occurred on the page.",
  "accessibility.emptyList": "List is empty.",
  "accessibility.selected": "Selected",
  "accessibility.expanded": "Expanded",
  "accessibility.collapsed": "Collapsed",

  // Notifications
  "notifications.title": "Notifications",
  "notifications.empty": "No notifications",
  "notifications.emptyDesc": "Notifications will appear here.",
  "notifications.markAllRead": "Mark all as read",
  "notifications.unread": "Unread",

  // Workspace
  "workspace.members.empty": "There are no additional members in this workspace yet.",
  "workspace.members.remove.cancel": "Cancel",
  "workspace.members.remove.confirm": "Remove member",
  "workspace.members.remove.description": "The member's workspace access will be revoked immediately. This action is recorded in the audit log.",
  "workspace.members.remove.title": "Confirm member removal",
  "workspace.role.owner": "Owner",
  "workspace.role.admin": "Admin",
  "workspace.role.member": "Member",
  "workspace.role.viewer": "Viewer",

  // Language
  "language.switch": "Switch language",
  "language.indonesian": "Indonesia",
  "language.english": "English",
};

export type MessageKey = keyof typeof indonesian;
export type Locale = "id" | "en";

export const messages = Object.freeze({ id: indonesian, en: Object.freeze(english) });

// Get all available locales
export const availableLocales: Locale[] = ["id", "en"];

// Fallback-safe translation function
// Returns the message for the given locale, falling back to Indonesian
export function translate(locale: string | null | undefined, key: MessageKey): string {
  const supportedLocale: Locale = locale === "en" ? "en" : "id";
  const message = messages[supportedLocale][key];
  if (message !== undefined) {
    return message;
  }
  // Fallback to Indonesian if key not found
  return messages.id[key] ?? key;
}

// Translation function with interpolation support
export function translateWithParams(locale: string | null | undefined, key: MessageKey, params: Record<string, string | number>): string {
  let message = translate(locale, key);
  for (const [paramKey, value] of Object.entries(params)) {
    message = message.replace(new RegExp(`\\{${paramKey}\\}`, "g"), String(value));
  }
  return message;
}

// Get locale display name
export function getLocaleDisplayName(locale: Locale): string {
  return locale === "id" ? "Indonesia" : "English";
}

// Validate if a string is a supported locale
export function isSupportedLocale(locale: string | null | undefined): locale is Locale {
  return locale === "id" || locale === "en";
}
