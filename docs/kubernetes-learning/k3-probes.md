# K3 — Liveness, readiness, and startup probes

> **Goal:** stop Kubernetes from guessing whether a Pod is healthy. K2 ended with a real, honest
> failure — the first API call right after Postgres was replaced hit a dead pooled connection —
> and nothing in the cluster noticed or reacted. K3 closes that gap with three purpose-built
> health checks instead of one general-purpose one.

---

## 1. Three probes, three different questions

Kubernetes doesn't have one concept of "healthy" — it asks three separate questions, each with a
different consequence if the answer is "no":

| Probe | Question it answers | What happens on failure |
|---|---|---|
| `startupProbe` | Has this container finished booting? | Nothing yet — liveness/readiness are suspended until this passes. Protects a slow first boot from being killed before it had a chance. |
| `readinessProbe` | Should this Pod receive traffic *right now*? | Pod is pulled from the owning Service's endpoint list. Container keeps running. Cheap, reversible. |
| `livenessProbe` | Should this container be killed and restarted? | `kubectl` restarts the container. Expensive, disruptive — a last resort for "this process is stuck." |

The mistake this phase is built around avoiding: pointing liveness at the same dependency-checked
endpoint as readiness. If a downstream dependency (Postgres) goes down, a dependency-checked
liveness probe fails too — and Kubernetes' response to a liveness failure is to **restart the
container**. Restarting the API can't fix a dead Postgres. It just repeatedly kills a perfectly
healthy API process while the real problem sits untouched downstream, and the restart itself
resets any warm state (connection pools, in-memory caches) for no benefit. This is a well-known
real-world anti-pattern, not a hypothetical — it's exactly the class of self-inflicted outage
liveness probes are supposed to prevent, not cause.

The fix: liveness must only ever answer *"is my own process stuck"* — never *"is something I
depend on down."*

---

## 2. What we built

Two endpoints in the API now exist for two different jobs:

```csharp
app.MapHealthChecks("/health");              // checks Postgres connectivity (AddNpgSql)
app.MapGet("/health/live", () => Results.Ok("Alive"));  // no dependency at all
```

And three probes on the API Deployment, each pointed at the endpoint that answers its specific
question:

```yaml
startupProbe:
  httpGet: { path: /health, port: 8080 }
  periodSeconds: 5
  failureThreshold: 12        # 12 x 5s = 60s grace for boot + the EF migration check
readinessProbe:
  httpGet: { path: /health, port: 8080 }
  periodSeconds: 5
  timeoutSeconds: 5
  failureThreshold: 2         # quick to pull out of rotation - cheap, reversible action
livenessProbe:
  httpGet: { path: /health/live, port: 8080 }
  periodSeconds: 15
  timeoutSeconds: 5
  failureThreshold: 3         # slower to restart - expensive, disruptive action
```

Startup and readiness both check `/health` (DB-dependent) — deliberately. Startup shouldn't
declare the app "up" until it can actually reach Postgres at least once; readiness should pull
the Pod from traffic the moment that's no longer true. Only liveness is different, on purpose.

The other three workloads got probes too, each fitted to what "healthy" actually means for it:

- **Web** — readiness and liveness both point at its own `/health`, no `startupProbe`. Web's
  health check has no downstream dependency (it's a static "I'm up" check), so there's no
  anti-pattern risk in reusing it for both, and Web boots fast enough that a startup grace period
  isn't needed.
- **Postgres** and **Redis** — `exec` probes instead of `httpGet`, since neither speaks HTTP:
  ```yaml
  readinessProbe:
    exec:
      command: ["pg_isready", "-U", "postgres"]   # Postgres's own purpose-built readiness check
  ```
  ```yaml
  readinessProbe:
    exec:
      command: ["redis-cli", "ping"]
  ```
  An `exec` probe runs a command *inside* the container; exit code `0` means healthy. Using each
  tool's own official health command (rather than, say, checking if the port is open) means the
  probe result matches what the tool itself considers "ready to serve," not just "the process
  exists."

---

## 3. Verified — the actual break-and-recover test

Applying YAML with probes in it and never triggering a failure would prove nothing. So:

1. **Baseline** — confirmed the API Pod was in the `ecommerce-api` Service's endpoint list.
2. **Broke it on purpose** — `kubectl scale deployment/postgres --replicas=0`. Postgres Pod
   gone entirely, not just slow.
3. **Watched for ~30 seconds**:
   - API Pod: `READY: false`, `RESTARTS: 0` the entire time.
   - `kubectl get endpoints ecommerce-api`: the Pod's IP disappeared from the list — the Service
     stopped sending it traffic.
   - `kubectl describe pod`'s event history: **no** `Killing` / `BackOff` / liveness-failure
     events at any point — only the original scheduling/image-pull events from when the Pod
     first started. Liveness never even noticed, because `/health/live` doesn't touch Postgres.
4. **Restored it** — `kubectl scale deployment/postgres --replicas=1`, waited for the new
   Postgres Pod to become ready.
5. **Recovery, watched for ~24 seconds**: the *same* API Pod (same name, same IP,
   `restartCount` still `0`) flipped back to `READY: true` and reappeared in the Service's
   endpoint list automatically — no restart, no manual intervention, no lost warm state.

This is precisely the K2 connection-pool hiccup, except this time the cluster handled it: instead
of a client request silently hitting a Pod that couldn't serve it, the Pod was removed from
rotation until it genuinely could.

| Check | Result |
|---|---|
| Probes registered | `kubectl describe pod` shows all three (`Startup`, `Readiness`, `Liveness`) with the correct endpoints/thresholds |
| Postgres down → API readiness | `READY: false`, endpoint removed from Service, `RESTARTS: 0` |
| Postgres down → API liveness | no restart-triggering events the entire outage |
| Postgres restored → API | same Pod, same IP, automatically re-added to Service, no restart at any point |
| Postgres/Redis `exec` probes | both Pods rolled out clean with `pg_isready`/`redis-cli ping` as their probe commands |

---

## 4. A real gotcha hit along the way: Docker Desktop itself hung mid-build

Partway through this phase, the Docker daemon stopped responding to *any* CLI command —
`docker build`, `docker images`, even `docker version` all hung. Checking processes showed
`vmmemWSL` (Docker Desktop's backing WSL2 VM) pinned at very high CPU/memory, past the point of
being simple build contention. The fix was restarting Docker Desktop entirely, which also
restarted the `kind` cluster.

Worth noting what *didn't* need to happen afterward: `kubectl get pods` initially showed every
Pod as `Unknown` (kubelet had lost contact with the container runtime during the outage), but
once Docker came back, kubelet reconnected and every Pod — including Postgres — came back
`Running` **on its own**, with the original data intact on the PVC. Nothing had to be reapplied
or restored by hand. That's the PVC durability from K2 and Kubernetes' own reconciliation loop
doing exactly what they're designed to do, unprompted — a good real example of why "desired
state, continuously reconciled" is the model's actual value, not just a slogan.

---

## 5. Next — K4: Ingress

K2's doc flagged this gap and it's still open: Web's client-side JavaScript calls
`http://ecommerce-api:8080` directly from the browser, which only resolves *inside* the cluster.
K4 adds an Ingress controller to give both Web and the API a single externally-reachable address,
the same problem Terraform's Container Apps ingress already solves in Azure — just the
Kubernetes-native way of doing it locally.
