output "id" {
  value       = azurerm_resource_group.this.id
  description = "Resource ID of the resource group."
}

output "name" {
  value       = azurerm_resource_group.this.name
  description = "Name of the resource group."
}

output "location" {
  value       = azurerm_resource_group.this.location
  description = "Location of the resource group."
}
