# Azure OpenAI = a Cognitive Services account of kind "OpenAI" plus one
# `azurerm_cognitive_deployment` per model the app uses. custom_subdomain_name
# is required for Entra ID / managed-identity token auth (and gives a clean
# https://<name>.openai.azure.com endpoint).

resource "azurerm_cognitive_account" "this" {
  name                  = var.name
  resource_group_name   = var.resource_group_name
  location              = var.location
  kind                  = "OpenAI"
  sku_name              = "S0"
  custom_subdomain_name = var.name
  local_auth_enabled    = var.local_auth_enabled
  tags                  = var.tags
}

resource "azurerm_cognitive_deployment" "chat" {
  name                   = var.chat_deployment_name
  cognitive_account_id   = azurerm_cognitive_account.this.id
  version_upgrade_option = "NoAutoUpgrade" # pin the model version; bump it deliberately
  rai_policy_name        = var.rai_policy_name

  model {
    format  = "OpenAI"
    name    = var.chat_model_name
    version = var.chat_model_version
  }

  # azurerm 3.x uses `scale { type, capacity }` here (4.x renames it to `sku`).
  # This subscription has 0 quota on regional "Standard" for the mini models,
  # so the chat deployment must be "GlobalStandard".
  scale {
    type     = var.chat_scale_type
    capacity = var.chat_capacity
  }
}

resource "azurerm_cognitive_deployment" "embeddings" {
  name                   = var.embedding_deployment_name
  cognitive_account_id   = azurerm_cognitive_account.this.id
  version_upgrade_option = "NoAutoUpgrade"
  rai_policy_name        = var.rai_policy_name

  model {
    format  = "OpenAI"
    name    = var.embedding_model_name
    version = var.embedding_model_version
  }

  scale {
    type     = var.embedding_scale_type
    capacity = var.embedding_capacity
  }
}
