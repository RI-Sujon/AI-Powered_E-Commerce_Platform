# Remote state storage, added 2026-10-13 so this environment can be driven from a CI pipeline
# (azure-pipelines-aks.yml) instead of only from a developer's machine. Previously this
# environment deliberately used Terraform's default local state (see k7-aks.md §0.b) - that's
# fine for a human running `terraform apply`/`destroy` back-to-back in one sitting, but a CI
# agent starts from a fresh checkout every run and would never see a local .tfstate file, so it
# would lose track of whatever the last run created. Reuses the same shared tfstate storage
# account the `shared` Terraform environment already created - no new storage account needed,
# just a new blob key so it doesn't collide with any other environment's state.
terraform {
  backend "azurerm" {
    resource_group_name  = "ecommerce-tfstate-rg"
    storage_account_name = "ecommercetfstateshared7"
    container_name       = "tfstate"
    key                  = "aks-learning.terraform.tfstate"
  }
}
