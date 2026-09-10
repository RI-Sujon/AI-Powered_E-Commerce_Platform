output "id" {
  value       = azurerm_container_app_environment.this.id
  description = "Resource ID of the Container Apps Environment."
}

output "name" {
  value       = azurerm_container_app_environment.this.name
  description = "Name of the Container Apps Environment."
}

output "default_domain" {
  value       = azurerm_container_app_environment.this.default_domain
  description = "Default domain suffix, e.g. <app-name>.<default_domain>, used to predict app FQDNs before they are created."
}

output "log_analytics_workspace_id" {
  value       = azurerm_log_analytics_workspace.this.id
  description = "Resource ID of the backing Log Analytics workspace."
}
