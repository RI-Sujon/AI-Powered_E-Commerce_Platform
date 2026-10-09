# 3 — Azure DevOps pipeline

Reads: [azure-pipelines.yml](../../azure-pipelines.yml) (299 lines, heavily commented). Goal: push
to the trigger branch → validate → build+push images → terraform apply dev → staging → prod.

> The Azure DevOps (ADO) setup below was done in the web UI originally and isn't recorded in git.
> Menu names change; the *concepts* are what to memorise.

## 3.0 The pipeline in one picture

```
git push (branch devops/terraform-main)
  └─ Validate         terraform fmt -check + validate (all 3 envs, no Azure login)
      └─ Build        az acr build  web & api → shared ACR, tags: $(Build.BuildId) and latest
          └─ Deploy_Dev        terraform apply -var image_tag=$(tag)     (environment: Development)
              └─ Deploy_Staging  same, + guarded import                  (environment: StagingSujon)
                  └─ Deploy_Production  same, + guarded import          (environment: ProductionSujon)
```
Terraform owns *everything* about the apps; the pipeline only supplies a fresh `image_tag`. The
Build stage runs `az acr build`, which builds the image **inside Azure Container Registry itself** —
the agent never needs Docker installed, and no Docker Hub (or any registry) service connection or
credential is needed. The only identity involved is the same `Azure-AppService-Connection` used by
every other Azure CLI/Terraform step.

## 3.1 Organisation, project, repo

1. <https://dev.azure.com> → sign in with the account → **New organization** → **New project**
   (private).
2. Get the code in: Repos → push this repository (`git remote add azure <url>`;
   `git push azure --all`). Or keep it on GitHub and choose GitHub when creating the pipeline.
3. The trigger is `devops/terraform-main` — create/push that branch, or change `trigger:` in the YAML.

## 3.2 Self-hosted agent (the pool is named `MyLocalAgent2`)

The YAML uses `pool: name: 'MyLocalAgent2'` — an agent running **on your own PC**, because
Microsoft-hosted parallel jobs on a new organisation need a request/approval. Self-hosted agents
get a free parallel job.

1. Project settings → Agent pools → **Add pool** → Self-hosted, name it (or reuse the name).
2. Pool → New agent → download the Windows agent zip → extract, e.g. `C:\agent`.
3. Create a **Personal Access Token** (User settings → Personal access tokens, scope *Agent Pools
   (Read & manage)*), then `.\config.cmd` (server URL `https://dev.azure.com/<org>`, PAT, pool,
   agent name) and `.\run.cmd` (or install as a service).
4. The agent machine needs on `PATH`: **az (recent, with `acr build` support), terraform (>= 1.5)**
   — the YAML says so at the top. Docker itself is **not** required; `az acr build` builds the
   image remotely inside ACR.

If you use another pool name, change `pool.name` in the YAML.

## 3.3 Service connections (Project settings → Service connections)

| Name in YAML | Type | Used by |
|---|---|---|
| `Azure-AppService-Connection` | Azure Resource Manager | `AzureCLI@2` tasks — logs the agent in as a service principal so `terraform` can use `ARM_USE_CLI`, **and** the `az acr build` step in the Build stage (no separate registry connection needed — ACR auth happens through this same Azure login) |

There used to be a `dockerhub-connection` (Docker Registry → Docker Hub) here, used by a `Docker@2`
build+push task. That's gone: the project moved to Azure Container Registry, and `az acr build`
builds the image remotely inside the registry, so Docker isn't needed on the agent at all and
there's no registry credential to manage — Terraform wires up the ACR pull credentials for
Container Apps automatically.

Azure connection: choose *App registration (automatic)* + *Subscription* scope. Then give that
service principal what Terraform needs (find its name in the connection's "Manage Service
Principal"):
```powershell
$sp = "<service principal appId>"
az role assignment create --assignee $sp --role Contributor --scope /subscriptions/<sub-id>/resourceGroups/ecommerce-rg
az role assignment create --assignee $sp --role "Storage Account Contributor" --scope /subscriptions/<sub-id>/resourceGroups/ecommerce-tfstate-rg
az role assignment create --assignee $sp --role "Storage Blob Data Contributor" --scope /subscriptions/<sub-id>/resourceGroups/ecommerce-tfstate-rg
```
Terraform also creates a **role assignment** (API app → OpenAI), which needs permission to write role
assignments: grant the SP **User Access Administrator** (or Owner) on `ecommerce-rg`, otherwise apply
fails with `AuthorizationFailed ... roleAssignments/write`.

Update in YAML: `azureSubscription`, `subscriptionId`, `acrName`, image repository names.

## 3.4 Variable group `ecommerce-secrets` (Pipelines → Library)

| Variable | Value | Secret? |
|---|---|---|
| `postgresAdminPassword` | the Postgres admin password | ✔ lock icon |
| `jwtKeyDev` / `jwtKeyStaging` / `jwtKeyProd` | a different random 32+ char string each | ✔ |

The YAML maps them to `TF_VAR_postgres_admin_password` / `TF_VAR_jwt_key` in each deploy step's `env:`.
*Why a group, not the YAML:* secrets must never be in git.

## 3.5 Environments (Pipelines → Environments)

Create three: `Development`, `StagingSujon`, `ProductionSujon` (must match the `environment:` in the
YAML). Adding an *Approval* check on `ProductionSujon` makes the pipeline pause for a human before
prod — a cheap, valuable thing to practise.

## 3.6 Fresh-account edit to the YAML

In `Deploy_Staging` and `Deploy_Production`, delete the `$inState = terraform state list` block and
the two `terraform import` `if` blocks. They only exist to adopt apps created by the *old* pipeline;
on a clean subscription the apps don't exist, so `import` errors. (Keep them only if you create the
apps imperatively first.)

## 3.7 Create and run the pipeline

Pipelines → **New pipeline** → Azure Repos Git (or GitHub) → *Existing Azure Pipelines YAML file* →
`/azure-pipelines.yml` → Run. First run: authorise the service connections/environments when
prompted ("Permit"). Watch each stage's log.

Common failures and what they mean:

| Symptom | Cause |
|---|---|
| Job waits forever "No agent found" | agent not running / wrong pool name |
| `terraform: command not found` | not on the agent's PATH (restart the agent after installing) |
| `AuthorizationFailed` on role assignment | SP lacks *User Access Administrator* (3.3) |
| `Error acquiring the state lock` | a previous run died; `terraform force-unlock <id>` |
| App created but crash-loops | image tag/repo wrong (doc 1.9) or DB schema missing (doc 2.4) |
| Stage skipped | previous stage failed (`dependsOn` + `condition: succeeded()`) |

## 3.8 Done when

A push to the trigger branch produces a green run, a new image tag appears in the ACR repository, and
`terraform plan` locally shows **No changes** *with* `-var image_tag=<that build id>`.

Next: [04-kubernetes-local-and-aks.md](04-kubernetes-local-and-aks.md).
