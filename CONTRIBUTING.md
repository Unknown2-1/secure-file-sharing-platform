# Contributing

Gunakan branch pendek dengan commit Conventional Commits. Jangan commit `.env`,
key, file upload, log, database dump, atau artifact test besar.

Sebelum pull request, jalankan backend format/build/test dan frontend
lint/typecheck/test/build. Sertakan migration impact, security impact, bukti
test, dan screenshot untuk perubahan UI. Perubahan cryptography, authorization,
retention, atau logging membutuhkan threat-model update dan security review.

Laporkan vulnerability melalui proses privat di `SECURITY.md`.

