data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "this" {
  name                       = var.name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = var.sku_name
  purge_protection_enabled   = var.purge_protection_enabled
  soft_delete_retention_days = var.soft_delete_retention_days

  # RBAC-based access (Key Vault Secrets Officer / Reader roles) instead of legacy access policies.
  enable_rbac_authorization = true

  public_network_access_enabled = var.public_network_access_enabled

  tags = var.tags
}

# Grants the identity running `terraform apply` permission to manage secrets.
# Container Apps should instead use a system-assigned managed identity + a
# "Key Vault Secrets User" role assignment (not created here) to read secrets at runtime.
resource "azurerm_role_assignment" "deployer_secrets_officer" {
  count                = var.grant_deployer_access ? 1 : 0
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}
