# User Guide

1. Daftar, buka kode verifikasi dari email/Mailpit, lalu login. Aktifkan TOTP
   dari **Keamanan akun** dan simpan recovery codes offline.
2. Pilih workspace di dashboard. Member dapat upload; Viewer hanya melihat file
   yang diberi akses. Status file harus **Available** sebelum share dibuat.
3. Upload dapat dibatalkan dan dicoba kembali. Setelah finalize, worker
   memvalidasi content, scan ClamAV, mengenkripsi, menyimpan ciphertext, dan
   menghapus plaintext sementara.
4. Pada **File saya**, download internal, soft-delete/restore, dan berikan
   `View` atau `Download` kepada anggota workspace. Akses dapat dicabut.
5. Buat public share dengan expiry, password, limit, one-time, dan safe preview.
   Link rahasia hanya muncul sekali; kirim password melalui kanal berbeda.
6. Slot download dihitung ketika download mulai dan tidak dikembalikan jika
   koneksi putus. Revoke menolak sesi baru; stream yang sudah mulai dapat selesai.
7. Undangan workspace dimasukkan melalui **Terima undangan** sebagai ID dan
   secret terpisah. Secret tidak dimasukkan ke URL.
8. Notification center menampilkan proses file/share. Export data pribadi ada
   di `GET /api/v1/users/me/export`; response tidak memuat object key atau hash.

Jangan upload data yang tidak berhak Anda bagikan. Malware scan mengurangi
risiko tetapi tidak menjamin file bebas seluruh ancaman.
