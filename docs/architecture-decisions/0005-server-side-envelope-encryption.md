# ADR 0005 — Server-side envelope encryption

Status: Accepted

## Context

Object storage tidak boleh menerima plaintext dan server tetap perlu melakukan
authorized streaming download. Satu nonce AES-GCM untuk file besar tidak
mendukung streaming output sebelum seluruh tag diverifikasi.

## Decision

Setiap file memperoleh random 256-bit DEK. Format versioned `VSH1` mengenkripsi
chunk dengan AES-256-GCM; nonce 96-bit dibentuk dari random 64-bit base nonce dan
32-bit chunk index. AAD memuat file ID, algorithm version, dan index. Setiap
chunk memiliki tag 128-bit. DEK di-wrap AES-GCM oleh KEK provider dan hanya
wrapped DEK disimpan. KEK tidak berada di database.

## Consequences

Memory bounded sesuai chunk, tampering per chunk gagal authentication, dan KEK
dapat dirotasi dengan rewrap tanpa mengenkripsi ulang file. Server dan KEK tetap
dapat mendekripsi, sehingga desain ini bukan E2EE/zero-knowledge. Perubahan
format wajib version bump dan backward-compatible reader/migration plan.
