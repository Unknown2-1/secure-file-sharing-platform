# Architecture

VaultShare adalah modular monolith dengan deployment API, worker, dan frontend
terpisah. Domain bebas dari infrastructure; Application mendefinisikan use case
dan port; Infrastructure mengimplementasikan persistence, storage, crypto,
scanner, email, dan jobs; API/Worker hanya composition roots.

```mermaid
flowchart TB
  subgraph Browser["Trust boundary: browser"]
    UI[Next.js UI]
  end
  subgraph Application["Application boundary"]
    API[API] --> APP[Application]
    WORKER[Worker] --> APP
    APP --> DOMAIN[Domain]
    INFRA[Infrastructure] --> APP
  end
  UI --> API
  INFRA --> DB[(PostgreSQL)]
  INFRA --> OBJ[(Encrypted object storage)]
  INFRA --> AV[ClamAV]
```

Public identifiers use UUID; secrets use independent random tokens. Workspace
scope is enforced in database queries and authorization policies, never only in
the frontend.
