# Key Management

Each file receives a random 256-bit DEK. The DEK encrypts authenticated chunks;
a KEK wraps the DEK. Database backup plus ciphertext is insufficient for restore
without the KEK. Local development accepts a validated base64 32-byte key;
production must use a KMS or secret manager and fail startup on unsafe config.

