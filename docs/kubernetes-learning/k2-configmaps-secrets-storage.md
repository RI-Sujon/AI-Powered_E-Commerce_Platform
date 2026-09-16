# K2 — ConfigMaps, Secrets as code, PersistentVolumeClaims, and a real multi-service stack

> **Goal:** stop pointing the local cluster at Azure. Bring Postgres (with pgvector) and Redis
> in-cluster, split configuration properly into a ConfigMap (non-secret) vs Secret (credentials)
> instead of K1's single ad-hoc Secret, give Postgres durable storage, and add Web so the full
> four-service stack is running together.

---

## 1. ConfigMap vs Secret — not just "where you put strings"

Both are key-value objects a Pod can consume as env vars. The difference is about **trust and
handling**, not the mechanism:

| | ConfigMap | Secret |
|---|---|---|
| Intended for | anything fine to be plaintext / visible | credentials |
| Storage | plaintext | base64-encoded (not encrypted by default - see §3) |
| Consumption pattern here | `envFrom.configMapRef` - dump *every* key at once | `env[].valueFrom.secretKeyRef` - hand-pick *one* key at a time |

That consumption difference is deliberate, not incidental: `app-config.yaml` has four settings
and every Pod that needs any of them just gets all of them (`ASPNETCORE_ENVIRONMENT`,
`Jwt__Issuer`, ...) — harmless, since none of it matters if someone reads it. `app-secrets.yaml`
has the DB and Redis connection strings and the JWT signing key, and each Pod's Deployment picks
out *only* the specific keys it needs, one at a time. A Pod that doesn't need the JWT key never
gets a copy of it sitting in its environment. That's real access-scoping, and it's the reason the
API Deployment now has both an `envFrom` block *and* three individual `secretKeyRef` entries
instead of just dumping both objects wholesale.

---

## 2. What we built

```
k8s/base/
├── app-config.yaml            # ConfigMap: ASPNETCORE_ENVIRONMENT, Jwt__Issuer/Audience/ExpiryMinutes
├── app-secrets.yaml            # Secret: postgres password, JWT key, DB + Redis connection strings
├── postgres-pvc.yaml           # 1Gi PersistentVolumeClaim
├── postgres-deployment.yaml    # pgvector/pgvector:pg16, mounts the PVC
├── postgres-service.yaml       # ClusterIP - other Pods reach it as "postgres"
├── redis-deployment.yaml       # redis:7-alpine, deliberately NO PVC
├── redis-service.yaml          # ClusterIP - "redis"
├── api-deployment.yaml         # updated: now points at in-cluster postgres/redis, not Azure
├── web-deployment.yaml         # new
└── web-service.yaml            # new
```

### A committed Secret this time — and why that's fine here but wasn't in K1

K1's Secret held the **real** Azure dev Postgres password, created imperatively so it never
touched a file. `app-secrets.yaml` this time is a committed **file**, because every value in it
is a password invented fresh for this local-only cluster, protecting nothing that exists anywhere
else. It uses `stringData` rather than `data` so the file is human-readable plaintext - Kubernetes
base64-encodes it automatically on `apply`. This is the one and only circumstance where checking
in a Secret is fine: when the "secret" has zero value outside the disposable thing it protects.
Never extend that reasoning to a credential that's true anywhere real (see K1's doc again if that
distinction isn't click yet - it's the same rule as `terraform.tfvars` never holding
`postgres_admin_password`).

### `pgvector/pgvector:pg16`, not plain `postgres:16`

The app's EF migrations include `CREATE EXTENSION vector` (Phase 5 of the AI integration). Plain
Postgres doesn't ship that extension; this image does, pre-compiled.

### The `PGDATA` subdirectory gotcha

```yaml
- name: PGDATA
  value: "/var/lib/postgresql/data/pgdata"
volumeMounts:
  - name: postgres-storage
    mountPath: /var/lib/postgresql/data
```
Mounting the PVC straight onto Postgres's data directory and leaving `PGDATA` at its default can
fail on some storage provisioners that pre-populate the mount with a `lost+found` directory -
Postgres refuses to initialize a non-empty directory. Pointing `PGDATA` at a subdirectory of the
mount sidesteps it entirely. Cheap to add, saves a confusing first failure.

### Redis gets no PVC — on purpose

Redis is used as a cache (see `docs/redis-integration/README.md`), and the app already falls back
to in-memory caching if Redis is unreachable. Losing cached data on a restart is the expected,
correct behavior for a cache - giving it durable storage would be solving a problem that doesn't
exist. Not everything that looks stateful needs a PersistentVolumeClaim.

### Why a Deployment, not a StatefulSet, for Postgres

StatefulSets exist for workloads that need **stable, ordered identity** across multiple
replicas - a database cluster where "which replica is the primary" matters, for instance. This is
a single Postgres replica; a Deployment + PVC gives it durable storage with far less complexity.
Worth knowing StatefulSet exists and why - not worth using here.

### Web reaches the API by Service name, not a URL

```yaml
- name: ApiBaseUrl
  value: "http://ecommerce-api:8080"
```
`ecommerce-api` resolves via Kubernetes' built-in cluster DNS because both Pods live in the
`ecommerce` namespace and there's a Service of that name. No IP, no public hostname - the Service
*is* the stable address. This is the same idea as K1's port-forward test, just Pod-to-Pod instead
of host-to-Pod.

---

## 3. `kubectl get secret` doesn't print values - that's not encryption

```
kubectl get secret app-secrets -n ecommerce
NAME          TYPE     DATA   AGE
app-secrets   Opaque   4      118s
```
Confirms the object and how many keys it has, nothing more - that's `kubectl` being sensible by
default, not real protection. The values are sitting in etcd as base64, which anyone with read
access to the cluster's storage (or `kubectl get secret -o yaml`, which **does** print them,
base64-decoded with one more step) can retrieve. A real cluster layers on something more - a
sealed-secrets controller, an external secret store (Azure Key Vault, HashiCorp Vault), or etcd
encryption at rest. Out of scope for a learning cluster; worth knowing it's a real gap.

---

## 4. Verified

| Check | Result |
|---|---|
| PVC | `postgres-data` → `Bound`, 1Gi, StorageClass `standard` |
| Postgres | fresh `ecommercedb`, all 8 app tables + `vector` extension `0.8.6` created by the app's own EF migrations on first boot |
| Redis | `✅ Redis cache configured` in the API's own startup log (was `in-memory cache` before K2) |
| API → Postgres | `📊 Database: Host=postgres` - in-cluster Service DNS resolved, no Azure involved |
| Rolling update | changing the API's env vars triggered a real rollout: old ReplicaSet (`...587b68cd85`) drained to 0, new one (`...9996949cd`) came up - visible in `kubectl get all` |
| Web | serves `HTTP 200`, injects `window.API_BASE_URL = 'http://ecommerce-api:8080'` into the page correctly |
| **PVC durability** | added a product through the real API → `kubectl delete pod -l app=postgres` → Deployment scheduled a brand-new Pod (different name/IP) → product still there, confirmed both directly in `psql` and through the live API |
| Bonus, unplanned | the *first* API call right after the Postgres Pod was replaced failed (a pooled Npgsql connection pointing at the now-dead old Pod); the next one succeeded once the pool recovered. Real, honest preview of exactly why K3's probes matter - nothing currently tells Kubernetes "don't send traffic to a Pod whose dependencies just changed out from under it" |

---

## 5. One thing this phase surfaced that it doesn't fix

Loading the Web page over `port-forward` works fine (server-side rendering), but the page's
**client-side JavaScript** calls `window.API_BASE_URL` directly from the browser — and
`http://ecommerce-api:8080` only resolves *inside* the cluster. A real browser on your own laptop
can't reach it. That's not a bug to patch now — it's exactly the gap **K4 (Ingress)** exists to
close: one externally-reachable address for both the page and its API calls.

---

## 6. Next — K3: probes

Add `liveness`/`readiness`/`startup` probes to the API (reusing the existing `/health` endpoint,
same concept as the Container Apps probes from the Terraform work) so Kubernetes actually holds
traffic off a Pod that isn't ready and restarts one that's hung - directly motivated by the
connection-pool hiccup this phase just produced.
