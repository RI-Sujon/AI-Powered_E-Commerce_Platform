# Consumed by environments/*/main.tf (via a data source, not by output wiring -
# Terraform roots can't read each other's outputs directly) and handy for
# `terraform output` when configuring the app.

output "openai_account_name" {
  value = module.openai.account_name
}

output "openai_account_id" {
  value = module.openai.account_id
}

output "openai_endpoint" {
  description = "Value for the app's AzureOpenAI__Endpoint setting."
  value       = module.openai.endpoint
}

output "chat_deployment_name" {
  value = module.openai.chat_deployment_name
}

output "embedding_deployment_name" {
  value = module.openai.embedding_deployment_name
}

output "postgres_allowlisted_extensions" {
  description = "Extensions now permitted on the shared server (still need CREATE EXTENSION per database)."
  value       = azurerm_postgresql_flexible_server_configuration.azure_extensions.value
}
