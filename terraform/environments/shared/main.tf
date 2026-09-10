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

# ---------------------------------------------------------------------------
# The "shared" stack holds singletons that every environment consumes. Right
# now that is just the one Azure OpenAI account - this subscription's quota
# allows exactly one (OpenAI.S0.AccountCount = 1).
#
# dev/staging/prod read this account via a `data "azurerm_cognitive_account"`
# block and each grants its own API container app the "Cognitive Services
# OpenAI User" role on it (see environments/*/main.tf).
#
# Apply this stack ONCE - it has its own state (shared.terraform.tfstate) and
# changes rarely.
# ---------------------------------------------------------------------------

data "azurerm_resource_group" "shared" {
  name = var.resource_group_name
}

module "openai" {
  source              = "../../modules/openai"
  name                = var.openai_account_name
  resource_group_name = data.azurerm_resource_group.shared.name
  location            = var.location
  local_auth_enabled  = var.openai_local_auth_enabled
  chat_capacity       = var.openai_chat_capacity
  embedding_capacity  = var.openai_embedding_capacity

  tags = {
    project    = "ecommerce"
    managed_by = "terraform"
    stack      = "shared"
  }
}
