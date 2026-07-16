# Troubleshooting

- ClamAV readiness may take several minutes while signatures initialize.
- A missing/placeholder 32-byte KEK must stop production startup.
- MinIO access failures require checking private bucket initialization and
  endpoint/SSL settings; do not make the bucket public as a workaround.
- Correlation IDs may be shared with support; never send cookies or share tokens.
- On scanner outage, files stay unavailable by design.

