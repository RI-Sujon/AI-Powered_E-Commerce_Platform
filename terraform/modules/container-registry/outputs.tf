output "id" {
  value       = azurerm_container_registry.this.id
  description = "Resource ID of the registry."
}

output "name" {
  value       = azurerm_container_registry.this.name
  description = "Name of the registry."
}

output "login_server" {
  value       = azurerm_container_registry.this.login_server
  description = "Hostname to docker login / push / pull against, e.g. ecommerceacrrabiuru.azurecr.io."
}
