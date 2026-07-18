# Security Policy

## Supported versions

Hanya release stabil terbaru yang didukung. Operator tetap bertanggung jawab
menjalankan checklist hardening dan menggunakan secret production sendiri.

## Reporting a vulnerability

Jangan buka public issue untuk vulnerability sensitif. Kirim laporan ke
`dnshadelio@gmail.com` dengan versi/commit, dampak, langkah reproduksi aman,
dan saran mitigasi. Jangan
sertakan data pribadi, secret, atau file pihak lain.

Kami akan mengakui laporan, melakukan triage, berkoordinasi mengenai perbaikan,
dan menyepakati disclosure setelah patch tersedia. Scope mencakup aplikasi,
authorization, cryptography integration, file processing, containers, dan CI.
Social engineering, denial-of-service terhadap layanan pihak ketiga, serta
testing pada deployment yang bukan milik pelapor berada di luar scope.

Good-faith research pada instance milik sendiri, dengan data sintetis dan tanpa
merusak layanan, akan diperlakukan dengan itikad baik. Coordinated disclosure
diharapkan; jangan mempublikasikan detail eksploit sebelum mitigasi tersedia.

Jika secret terekspos: revoke/rotate segera, periksa audit dan log, hapus dari
distribution channels, dan evaluasi history rewrite. Jika KEK terkompromi:
isolasi layanan, hentikan dekripsi baru, rotasi KEK, rewrap seluruh DEK,
investigasi akses ciphertext/database, dan ikuti incident response plan.
