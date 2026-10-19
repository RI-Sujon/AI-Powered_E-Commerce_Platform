# Reference-only file.
#
# Terraform has no native way to "include" a shared `terraform {}`/`provider {}` block across
# independent root modules (each environment under terraform/environments/* is its own root
# module with its own state). To avoid drift between environments, copy this block verbatim
# into each environment's main.tf whenever you bump the provider version, rather than editing
# each environment separately.
#
# If you want true DRY sharing across environments (single source of truth, no copy/paste),
# consider adopting Terragrunt, which layers on top of Terraform specifically to solve this.

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">= 3.90.0, < 4.0.0"
    }
  }
}

provider "azurerm" {
  features {}
}
