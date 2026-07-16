# Share Link Security

URL shape is `/s/{publicIdentifier}/{secretToken}`. The identifier is not a
secret; the independent high-entropy URL-safe token is shown once and stored
only as a cryptographic hash. Passwords use ASP.NET Core PasswordHasher and
successful verification creates a short-lived share-bound authorization
session. Password and discovery endpoints receive layered rate limits.

```mermaid
sequenceDiagram
  participant B as Browser
  participant A as API
  participant D as Database
  B->>A: public ID + secret token
  A->>A: hash token
  A->>D: constant-shape lookup
  A->>A: validate start/expiry/revoke/limit
  A-->>B: generic prompt or short-lived session
```

