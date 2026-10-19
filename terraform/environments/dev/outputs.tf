output "resource_group_name" {
  value = data.azurerm_resource_group.shared.name
}

output "key_vault_uri" {
  value = data.azurerm_key_vault.shared.vault_uri
}

output "postgres_fqdn" {
  value = data.azurerm_postgresql_flexible_server.shared.fqdn
}

output "postgres_database_name" {
  value = azurerm_postgresql_flexible_server_database.this.name
}

output "container_app_environment_domain" {
  value = data.azurerm_container_app_environment.shared.default_domain
}

output "api_url" {
  value = "https://${local.api_fqdn}"
}

output "web_url" {
  value = "https://${local.web_fqdn}"
}
