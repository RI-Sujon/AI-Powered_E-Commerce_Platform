# K1 — Pods, Deployments, Services, and the core `kubectl` workflow

> **Goal:** get the existing API image running inside a real (local) Kubernetes cluster, reachable
> through a stable internal address, and learn the handful of `kubectl` commands you'll use
> constantly: `apply`, `get`, `describe`, `logs`, `exec`, `port-forward`.

---

## 1. The four objects, and why there are four of them

A beginner's first surprise: you don't "run a container" in Kubernetes. You describe **desired
state**, in layers, and let controllers reconcile reality to match it.

```
Deployment  →  manages  →  ReplicaSet  →  manages  →  Pod(s)  →  contain  →  Container(s)
                                                          ↑
Service  ───────────────────── routes to ────────────────┘
```

- **Pod** — the actual running unit (one or more containers sharing a network namespace). Pods
  are disposable: when one dies, it's gone, not restarted in place.
- **ReplicaSet** — keeps N copies of a Pod template running. You'll rarely touch this directly.
- **Deployment** — manages ReplicaSets for you and handles rollouts (new image → new ReplicaSet →
  old Pods drained, new Pods started). This is the object you actually write.
- **Service** — Pods get a new IP every time they restart, so nothing can hard-code a Pod's
  address. A Service watches for Pods matching a label selector and gives you one stable DNS
  name / IP that always routes to whichever Pods currently match, no matter how many times
  they've been replaced underneath it.

This layering is the whole reason Kubernetes can self-heal: you never told it to keep a Pod
alive — you told the Deployment "I want 1 of these," and it's still true after any failure.

---

## 2. What we built

`k8s/base/namespace.yaml` — an `ecommerce` Namespace (a named partition in the cluster, keeps
this app's objects grouped and separate from anything else in the cluster).

`k8s/base/api-deployment.yaml` — the important bits:

```yaml
spec:
  replicas: 1
  selector:
    matchLabels: { app: ecommerce-api }   # MUST match template.metadata.labels below
  template:
    metadata:
      labels: { app: ecommerce-api }
    spec:
      containers:
        - name: api
          image: ecommerce-api:k1-local
          imagePullPolicy: IfNotPresent   # don't try to pull from a registry - use the local build
          env:
            - name: ConnectionStrings__DefaultConnection
              valueFrom:
                secretKeyRef: { name: api-secrets, key: db-connection-string }
          resources:
            requests: { cpu: "100m", memory: "192Mi" }
            limits:   { cpu: "500m", memory: "384Mi" }
```

- `selector.matchLabels` vs `template.metadata.labels` mismatches are a classic first bug — the
  Deployment literally can't find Pods it just created if these drift apart.
- `imagePullPolicy: IfNotPresent` matters specifically for local images: the default (`Always`)
  tries to pull from a registry and fails for something that only exists in the local Docker
  build cache. Docker Desktop's Kubernetes shares that cache directly, so `IfNotPresent` finds it.
- `resources.requests` is what the scheduler reserves when deciding which node a Pod fits on;
  `limits` is the hard ceiling (throttled for CPU, killed for memory) if exceeded.
- The connection string and JWT key come from a **Secret** referenced by name (`secretKeyRef`),
  not written inline — see §4.

`k8s/base/api-service.yaml` — a `ClusterIP` Service (the default type, only reachable from inside
the cluster - deliberate for now, K4 covers exposing it properly via Ingress):

```yaml
spec:
  type: ClusterIP
  selector: { app: ecommerce-api }   # this is how the Service finds the same Pods
  ports:
    - port: 8080        # what other things in the cluster connect to
      targetPort: 8080   # what port the container actually listens on
```

---

## 3. Build → apply → verify

```powershell
# Build the image so the cluster can see it (no push needed - see §2)
cd src/ProjectMainApp
docker build -f Project.Endpoint/Dockerfile -t ecommerce-api:k1-local .

# Namespace first
kubectl apply -f ../../k8s/base/namespace.yaml

# The Secret is created imperatively, NOT as a committed file - it holds the real dev DB
# password. See §4 for why and how.
kubectl create secret generic api-secrets -n ecommerce `
  --from-literal=db-connection-string="Host=...;Database=ecommercedb_dev;Username=postgres;Password=...;SSL Mode=Require;" `
  --from-literal=jwt-key="<any string >=32 chars for local learning>"

# Now the Deployment + Service
kubectl apply -f ../../k8s/base/api-deployment.yaml -f ../../k8s/base/api-service.yaml
```

**The `kubectl` tour that matters:**

| Command | What it showed us |
|---|---|
| `kubectl get all -n ecommerce` | Pod, ReplicaSet, Deployment, and Service all in one shot — and how they relate (`ecommerce-api-587b68cd85-cwnwg` — Deployment name + ReplicaSet hash + Pod suffix) |
| `kubectl describe pod -l app=ecommerce-api` | The **Events** section — scheduling decisions, pulls, restarts. This is the first place to look when a Pod won't come up |
| `kubectl logs -l app=ecommerce-api --tail=15` | The app's own stdout — same startup log lines you'd see running it any other way |
| `kubectl exec <pod> -- curl localhost:8080/health` | Runs a command *inside* the container's own network namespace — proves the app is alive from the inside |
| `kubectl port-forward svc/ecommerce-api 18080:8080` | Tunnels a local port to the **Service** (not the Pod directly) — proves Service→Pod routing actually works, without needing Ingress yet |

---

## 4. Why the Secret isn't a file in the repo

`k8s/base/` has no `secret.yaml`. A Kubernetes `Secret` object's values are only
**base64-encoded**, not encrypted, by default — committing one to git would be barely better than
committing the plaintext password. Same principle as `terraform.tfvars` never holding
`postgres_admin_password` (see the Terraform README) — real credentials get created out-of-band
(`kubectl create secret ...`, or a proper secret manager in a real cluster), and the manifests
only ever reference them **by name**.

`kubectl get secret api-secrets` confirms the object exists (`DATA: 2`) without printing values -
that's `kubectl` being sensible by default, not real encryption. Don't mistake "not printed" for
"secure at rest": a full production setup would layer on something like Sealed Secrets, an
external secret store, or etcd encryption at rest - out of scope for K1, worth knowing exists.

---

## 5. Verified

| Check | Result |
|---|---|
| `docker build` | ✅ image `ecommerce-api:k1-local` |
| Pod reaches `Running`, `1/1 Ready` | ✅ (~20s — mostly container start + EF migration check on boot) |
| `kubectl logs` | ✅ real startup log: connected to `ecommerce-postgres-sujon`, migrations applied, "Application started successfully" |
| `kubectl exec ... whoami` | ✅ `appuser` — confirms the Dockerfile's non-root user is what's actually running |
| `kubectl exec ... curl localhost:8080/health` | ✅ `Healthy`, from inside the container |
| `port-forward` + `curl localhost:18080/health` | ✅ `Healthy`, from the **host**, through the Service |
| `curl localhost:18080/ai/selftest` | ✅ `503 {"ok":false,"reason":"AzureOpenAI:Endpoint not configured"}` — the exact graceful-degradation behavior built in Phase 3, now proven on a second deployment target |

This is the real API image, talking to the real Azure dev Postgres, running inside a local
Kubernetes cluster instead of Azure Container Apps — same app, different orchestrator.

---

## 6. Next — K2: ConfigMaps, Secrets as code, PersistentVolumeClaims

Bring Postgres and Redis into the cluster too (instead of pointing at Azure), split config into
a proper `ConfigMap` (non-secret) vs `Secret` (credentials) rather than everything living in one
Secret like today, and give Postgres a `PersistentVolumeClaim` so its data survives a Pod restart.
