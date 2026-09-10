# Remote state for the "shared" stack. Its own storage account + blob, isolated
# from dev/staging/prod state. Created once, outside Terraform:
#   az storage account create -n ecommercetfstateshared -g ecommerce-tfstate-rg -l eastus --sku Standard_LRS
#   az storage container create -n tfstate --account-name ecommercetfstateshared
terraform {
  backend "azurerm" {
    resource_group_name  = "ecommerce-tfstate-rg"
    storage_account_name = "ecommercetfstateshared"
    container_name       = "tfstate"
    key                  = "shared.terraform.tfstate"
  }
}
