# Remote state storage. The storage account/container referenced here must already exist
# (create it once, outside Terraform, e.g. `az group create` + `az storage account create` +
# `az storage container create`). Values with real secrets are never stored in this file.
#
# You can override any of these at `terraform init` time instead of hardcoding them, e.g.:
#   terraform init -backend-config="storage_account_name=<name>" -backend-config="access_key=<key>"
terraform {
  backend "azurerm" {
    resource_group_name  = "ecommerce-tfstate-rg"
    storage_account_name = "ecommercetfstatedev"
    container_name        = "tfstate"
    key                    = "dev.terraform.tfstate"
  }
}
