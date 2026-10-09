# Azure Container Registry - a private Docker registry. Basic SKU is the cheapest tier
# (~$0.167/day) and is enough for a learning project's two small images. admin_enabled = true
# turns on the simple username/password login (shown via `admin_username`/`admin_password`
# outputs) so the `container-app` module's existing registry_username/registry_password inputs
# work unchanged - no extra Azure RBAC wiring needed to get started. A managed-identity-based
# pull (no stored credentials at all) is a natural next step once you're comfortable with this.
resource "azurerm_container_registry" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  admin_enabled       = var.admin_enabled
  tags                = var.tags
}
