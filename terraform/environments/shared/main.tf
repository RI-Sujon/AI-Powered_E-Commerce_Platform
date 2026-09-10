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

# The shared PostgreSQL Flexible Server is created outside Terraform and is read-only in the
# per-environment stacks. This one server-level parameter is managed here because it's a single
# server-wide setting - not something any one environment owns.
data "azurerm_postgresql_flexible_server" "shared" {
  name                = var.postgres_server_name
  resource_group_name = data.azurerm_resource_group.shared.name
}

# Add extensions to the server's allowlist so `CREATE EXTENSION` is permitted for them.
# `azure.extensions` is a dynamic parameter (no server restart needed). Each database still needs
# `CREATE EXTENSION vector` run once - see docs/ai-integration/phase-2-pgvector.md.
resource "azurerm_postgresql_flexible_server_configuration" "azure_extensions" {
  name      = "azure.extensions"
  server_id = data.azurerm_postgresql_flexible_server.shared.id
  value     = join(",", var.postgres_allowlisted_extensions)
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
