# Rebuild from scratch — hands-on runbook (no AI, no agent)

Goal: on a **fresh Azure account**, rebuild this whole project yourself — Azure foundation →
Terraform → Azure DevOps pipeline → Kubernetes (local) → AKS — using only these docs.

The existing docs explain *what was built and why*. This runbook is different: it is the **order of
operations** and **the exact commands**, with a "why" next to each, and a self-test at the end.

| # | Doc | You will have, when done |
|---|---|---|
| 0 | [00-cost-and-account-rules.md](00-cost-and-account-rules.md) | A budget plan so the 30-day credit isn't burned before you finish |
| 1 | [01-azure-foundation.md](01-azure-foundation.md) | Resource groups, Postgres, Container Apps env, Key Vault, tfstate storage |
| 2 | [02-terraform.md](02-terraform.md) | Azure OpenAI + dev/staging/prod container apps, all from Terraform |
| 3 | [03-azure-devops-pipeline.md](03-azure-devops-pipeline.md) | ADO project, service connections, variable group, agent, green pipeline |
| 4 | [04-kubernetes-local-and-aks.md](04-kubernetes-local-and-aks.md) | The stack on a local cluster (K1–K6), then on AKS (K7) |
| 5 | [05-teardown.md](05-teardown.md) | Everything deleted, verified, $0 running |
| 6 | [06-self-test.md](06-self-test.md) | Questions + "do it blind" drills to check you really remember |

## How to use this so it actually sticks

Reading is the weakest way to learn. Use this loop for every doc:

1. **Read the "why" lines first**, once, slowly.
2. **Close the doc. Type the commands from memory** into a scratch file. Wrong is fine.
3. **Open the doc, diff your attempt against it.** The gaps are exactly what you don't know yet.
4. **Run it for real.** When something fails, read the error *before* looking anything up. Write the
   error and the fix in your own words in a `my-notes.md` (not in this repo's docs).
5. **Next day, redo the step blind** (see doc 6). Spaced repetition beats a single long session.

Never copy-paste a command you haven't typed once. Typing is the memory.

## The 6 ideas that explain almost everything here

1. **Terraform = desired state in files; state file = its memory of what it made.** `plan` diffs the
   two, `apply` fixes the difference. `data` blocks *read* things Terraform doesn't own.
2. **Backend state lives in Azure Storage** — so that storage must exist *before* `terraform init`
   (the one manual step, doc 1).
3. **A module is a template, an environment folder is what you apply.** Each env has its own state.
4. **The pipeline only feeds Terraform an image tag.** Build → push to Docker Hub → `terraform apply
   -var image_tag=…`.
5. **No secrets in git** (except the deliberately fake local Kubernetes ones): they come from
   `TF_VAR_*` env vars locally and an ADO variable group in CI. Azure OpenAI uses managed identity
   (no key at all).
6. **Kubernetes = you declare desired state, controllers reconcile.** Deployment → ReplicaSet → Pod;
   a Service gives pods a stable name; kustomize overlays vary the base per environment.

## Honest warnings (read before you start)

- Commands for creating the *shared platform* (doc 1) and the ADO setup (doc 3) were **not recorded
  in this repo** — the originals were created by hand/by an older pipeline that was later replaced.
  They are written here from general Azure knowledge. Flags change: if one errors, run the same
  command with `--help`. That debugging **is** the learning; note what you changed.
- Everything here costs real money on a paid subscription. Doc 0 first.
