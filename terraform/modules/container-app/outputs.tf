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

output "principal_id" {
  value       = one(azurerm_user_assigned_identity.this[*].principal_id)
  description = "Principal (object) ID of the user-assigned identity - use as an azurerm_role_assignment principal_id. Null unless assign_identity = true."
}

output "identity_client_id" {
  value       = one(azurerm_user_assigned_identity.this[*].client_id)
  description = "Client ID of the user-assigned identity - set as the AZURE_CLIENT_ID env var so DefaultAzureCredential picks this identity."
}
