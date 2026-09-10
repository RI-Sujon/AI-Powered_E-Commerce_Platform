output "account_id" {
  description = "Resource ID of the Azure OpenAI account (use as the scope for role assignments)."
  value       = azurerm_cognitive_account.this.id
}

output "account_name" {
  value = azurerm_cognitive_account.this.name
}

output "endpoint" {
  description = "https://<name>.openai.azure.com/ - the value for AzureOpenAI__Endpoint."
  value       = azurerm_cognitive_account.this.endpoint
}

output "chat_deployment_name" {
  value = azurerm_cognitive_deployment.chat.name
}

output "embedding_deployment_name" {
  value = azurerm_cognitive_deployment.embeddings.name
}
