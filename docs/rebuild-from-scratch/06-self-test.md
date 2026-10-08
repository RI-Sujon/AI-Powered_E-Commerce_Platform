# 6 — Self-test (answer WITHOUT looking; then check)

Do a section the day after you study it, again after a week. Write answers on paper or in a scratch
file first. A wrong answer is useful — it tells you what to re-read.

## A. Concepts (answer in one or two sentences)

1. Why must the Terraform state storage account be created *outside* Terraform?
2. What is the difference between `resource` and `data` in Terraform? Which one can `destroy` delete?
3. What does `terraform plan` compare, and what does it change?
4. Why does each environment folder have its own state file / storage account?
5. What is a module? Which two modules does this project actually use?
6. Where do secrets live (a) locally, (b) in CI, (c) at runtime in the container app?
7. Why does the API need *no key* for Azure OpenAI? What role does it hold, and on what scope?
8. Why did the pipeline stop creating infrastructure with `az containerapp create`?
9. What exactly does the pipeline pass to Terraform, and why is `image_tag` also in `terraform.tfvars`?
10. Why do staging/prod need a manual `dotnet ef database update` but dev doesn't?
11. Why does a Terraform change to only a secret not roll a new Container App revision? Fix?
12. Pod vs ReplicaSet vs Deployment vs Service — what does each one do?
13. ConfigMap vs Secret vs PVC — what problem does each solve?
14. Liveness vs readiness vs startup probe — what does Kubernetes do when each fails?
15. What does an Ingress give you that a Service doesn't? Why `LoadBalancer` on Docker Desktop?
16. What is a Kustomize overlay? Why is `images:` needed for the `aks` overlay?
17. Why does the HPA need metrics-server, and why did it need `--kubelet-insecure-tls` locally but not on AKS?
18. Why does a disabled subscription still let you delete but not create? How do you test it for real?
19. Name four resources that cost money while idle in this project.
20. Why is an AKS cluster kept for only one sitting?

## B. "Type it blind" drills (no doc open)

1. Create `ecommerce-tfstate-rg` and one state storage account + `tfstate` container with `az`.
2. Create the Container Apps environment and print its default domain.
3. Create a Postgres flexible server (Burstable B1ms, v16, Azure-services firewall rule) and a database.
4. Export the two Terraform secrets for the shell, run init → plan → apply in `environments/dev`.
5. List what Terraform manages in a folder; destroy only that folder.
6. Build and push the API image to Docker Hub with two tags.
7. Write the 5 lines of YAML for one pipeline stage that runs `terraform apply -var image_tag=…`.
8. Get AKS credentials, confirm the context, list nodes, show node CPU/RAM usage.
9. Apply `k8s/overlays/aks`, watch pods, port-forward the web Service, delete the namespace.
10. Debug a pod stuck in `ImagePullBackOff`: the 3 commands, in order.
11. Tear everything down and prove nothing billable is left.

## C. Break-it exercises (best for real understanding)

Do these on the **cheap local cluster / dev env**, on purpose, then fix:
- Point the API probe at a path that doesn't exist → predict what you'll see in `get pods -w` first.
- Delete the Postgres pod → predict whether data survives (PVC) and then check.
- Change `image_tag` to a non-existent tag in dev, `terraform apply` → read the failure → fix.
- Run `terraform plan` after changing a setting in the Azure Portal → observe drift → decide: revert
  in portal, or update the `.tf`.
- Remove the role assignment; call an AI endpoint → read the 401/403 → restore it.

## D. Score yourself

| Section | Last attempt | Weak spots |
|---|---|---|
| A concepts | | |
| B drills | | |
| C break-it | | |

If you can do B1–B11 blind and explain A1–A20 out loud, you can rebuild this project without help.
