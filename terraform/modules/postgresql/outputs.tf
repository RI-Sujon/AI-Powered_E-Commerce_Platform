output "id" {
  value       = azurerm_postgresql_flexible_server.this.id
  description = "Resource ID of the PostgreSQL Flexible Server."
}

output "fqdn" {
  value       = azurerm_postgresql_flexible_server.this.fqdn
  description = "Fully qualified domain name of the server."
}

output "database_name" {
  value       = azurerm_postgresql_flexible_server_database.this.name
  description = "Name of the application database."
}

output "connection_string" {
  value       = "Host=${azurerm_postgresql_flexible_server.this.fqdn};Database=${azurerm_postgresql_flexible_server_database.this.name};Username=${var.administrator_login};Password=${var.administrator_password};SSL Mode=Require;"
  description = "ADO.NET-style connection string for Project.Endpoint's ConnectionStrings__DefaultConnection."
  sensitive   = true
}
