output "id" {
  value       = azurerm_container_app.this.id
  description = "Resource ID of the Container App."
}

output "name" {
  value       = azurerm_container_app.this.name
  description = "Name of the Container App."
}

output "fqdn" {
  value       = try(azurerm_container_app.this.ingress[0].fqdn, null)
  description = "Public FQDN of the Container App (null when ingress is disabled)."
}

output "latest_revision_name" {
  value       = azurerm_container_app.this.latest_revision_name
  description = "Name of the latest revision."
}
