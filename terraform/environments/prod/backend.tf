# Remote state storage. The storage account/container referenced here must already exist
# (create it once, outside Terraform, e.g. `az group create` + `az storage account create` +
# `az storage container create`). Values with real secrets are never stored in this file.
terraform {
  backend "azurerm" {
    resource_group_name  = "ecommerce-tfstate-rg"
    storage_account_name = "ecommercetfstateprod7"
    container_name       = "tfstate"
    key                  = "prod.terraform.tfstate"
  }
}
