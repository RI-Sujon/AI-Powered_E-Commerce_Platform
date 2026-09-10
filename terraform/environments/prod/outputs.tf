output "resource_group_name" {
  value = module.resource_group.name
}

output "key_vault_uri" {
  value = module.key_vault.uri
}

output "postgres_fqdn" {
  value = module.postgresql.fqdn
}

output "container_app_environment_domain" {
  value = module.container_app_env.default_domain
}

output "api_url" {
  value = "https://${local.api_fqdn}"
}

output "web_url" {
  value = "https://${local.web_fqdn}"
}
