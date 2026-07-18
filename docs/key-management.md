# Key Management

Each file receives a random 256-bit DEK. The DEK encrypts authenticated chunks;
a KEK wraps the DEK. Database backup plus ciphertext is insufficient for restore
without the KEK. V1 accepts a validated base64 32-byte key from the environment;
production must inject it through a protected secret manager and fail startup
on unsafe config. A cloud-vendor KMS adapter can implement
`IKeyEncryptionProvider` in the future, but is not part of V1.
