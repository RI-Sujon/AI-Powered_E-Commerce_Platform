# K4 — Ingress: one entrypoint instead of separate ports

> **Goal:** close the gap K2's doc flagged and left open on purpose. Web's client-side JavaScript
> calls `window.API_BASE_URL` directly from the browser, and `http://ecommerce-api:8080` — the
> value that pointed at — only resolves *inside* the cluster. A real browser on your own laptop
> can't reach it. Give Web and the API a single externally-reachable address instead.

---

## 1. Why an Ingress, not just a second `type: LoadBalancer` Service

Every Service so far has been `ClusterIP` - internal only, on purpose (see `api-service.yaml`'s
own comment from K1). The obvious-looking fix is making Web's Service a `LoadBalancer` too, same
as the API's. That solves reachability but not the actual problem: Web and the API would then
sit behind **two different externally-reachable addresses**, and the browser's own same-origin
model doesn't care that both happen to live in the same cluster - it just sees two origins.

An Ingress is an L7 (HTTP-aware) router that sits in front of Services and picks *which* Service
handles a request based on the request's own path or host - not by exposing every Service
individually. One address, routed by path:

```
http://localhost/api/*  →  ecommerce-api Service (8080)
http://localhost/*      →  ecommerce-web Service (8080)
```

That's the actual fix: Web and its API calls now share one origin, because they're now the same
origin as far as the browser is concerned.

---

## 2. What we built

```
k8s/base/
└── ingress.yaml          # new - routes /api → ecommerce-api, / → ecommerce-web
```

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: ecommerce-ingress
  namespace: ecommerce
spec:
  ingressClassName: nginx
  rules:
    - http:
        paths:
          - path: /api
            pathType: Prefix
            backend:
              service: { name: ecommerce-api, port: { number: 8080 } }
          - path: /
            pathType: Prefix
            backend:
              service: { name: ecommerce-web, port: { number: 8080 } }
```

No path rewriting anywhere. Every API controller is already rooted at `api/[controller]`
(`[Route("api/[controller]")]` in `Project.Endpoint`), so `/api/product/get-product-list` hitting
the Ingress forwards through to the exact same path on the API Service - the Ingress path and the
app's own route just happen to already agree.

### The controller itself isn't in `k8s/base/` - it's a Helm release

The Ingress *resource* (the routing rules above) is ours to own and commit. The Ingress
*controller* (the actual nginx process that reads those rules and does the routing) is a
third-party component - same category as Postgres or Redis's images, not something to hand-write
YAML for. Installed via Helm, the standard package manager for exactly this kind of "give me a
known-good, configurable deployment of someone else's software" job:

```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx --create-namespace \
  --set controller.service.type=LoadBalancer
```

`--set controller.service.type=LoadBalancer` is the one thing tuned away from the chart's
default. Docker Desktop's Kubernetes auto-binds a `LoadBalancer` Service to `localhost` on the
host - no MetalLB, no cloud load balancer needed locally. That's what makes `http://localhost/`
work at all on a laptop.

### Web's `ApiBaseUrl` goes from a cluster DNS name to empty

```yaml
# k8s/base/web-deployment.yaml
- name: ApiBaseUrl
  value: ""   # was "http://ecommerce-api:8080" in K2
```

`window.API_BASE_URL = '@Configuration["ApiBaseUrl"]'` (`_Layout.cshtml`) injects this straight
into the page, and every client-side service file builds its request URL as
`` `${window.API_BASE_URL}/api/product` ``. An empty string collapses that to a **relative** URL:
the browser resolves `/api/product/...` against whatever origin actually served the page. Once
that page came from the Ingress, `/api/...` from the same browser tab reaches the Ingress too,
which routes it to `ecommerce-api` - no hardcoded host, no cluster-internal DNS the browser could
never have resolved in the first place.

---

## 3. Verified

| Check | Result |
|---|---|
| ingress-nginx controller | `helm install` → controller Pod `1/1 Running`, `ingress-nginx-controller` Service `type: LoadBalancer` |
| Ingress resource | `kubectl get ingress -n ecommerce` shows `ecommerce-ingress`, class `nginx`, port 80 |
| Web through the Ingress | `curl http://localhost/Product` → `200`, page HTML contains `window.API_BASE_URL = ''` |
| API through the Ingress, same relative path the browser's JS actually calls | `curl http://localhost/api/product/get-product-list` → `200`, real JSON data back - the exact request Web's `ProductService.js` makes, now succeeding through the same origin that failed before K4 |
| Data continuity | the product added during K2's PVC durability test is still there, still served correctly - through several Docker Desktop restarts since (see §4) |

The middle two rows are the actual fix, not incidental: before K4, a real browser's JS console
would have thrown a network error trying to reach `ecommerce-api:8080` directly. After K4, the
same call that JS makes (`GET /api/product/get-product-list`, relative) succeeds because it never
leaves the page's own origin.

---

## 4. A recurring gotcha, not a one-off: Docker Desktop's backend needs the WSL layer genuinely idle

K3's doc already covered one Docker Desktop hang mid-build. This phase hit a worse variant twice
in a row: `com.docker.backend.exe` crashing silently within ~1 second of every launch, no error
logged - traced to a **stale `vmmemWSL` process left over from the previous session** (days old,
thousands of CPU-seconds) that a fresh Docker Desktop launch couldn't cleanly take over from.
`wsl --shutdown` to force every WSL-backed VM down before relaunching Docker Desktop is the
reliable fix - a plain app restart isn't enough if the old VM process never actually exited.

Same as K3: no manual recovery was needed once Docker Desktop was actually healthy again.
`kubectl get pods` showed every Pod as `Unknown` mid-outage; the moment kubelet reconnected, all
four came back `Running` with Postgres's data intact on the PVC, entirely on its own.

---

## 5. Next — K5: Kustomize

Everything in `k8s/base/` so far is one flat set of manifests for one environment. K5 introduces
Kustomize overlays (`overlays/{dev,staging,prod}/`) so the same base manifests can be adjusted per
environment - different replica counts, different resource limits, different config - without
duplicating or hand-editing the base files. Same idea as the Terraform environments, in a
Kubernetes-native tool that needs no separate install (it's built into `kubectl` since 1.14).
