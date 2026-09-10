locals {
  registry_secret_name = "registry-password"

  # Automatically add the registry password as a secret when private-registry credentials are supplied.
  all_secrets = var.registry_password != "" ? concat(
    var.secrets,
    [{ name = local.registry_secret_name, value = var.registry_password }]
  ) : var.secrets
}

resource "azurerm_container_app" "this" {
  name                         = var.name
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_app_environment_id
  revision_mode                = "Single"
  tags                          = var.tags

  dynamic "secret" {
    for_each = local.all_secrets
    content {
      name  = secret.value.name
      value = secret.value.value
    }
  }

  dynamic "registry" {
    for_each = var.registry_server != "" ? [1] : []
    content {
      server               = var.registry_server
      username             = var.registry_username
      password_secret_name = local.registry_secret_name
    }
  }

  ingress {
    external_enabled = var.external_ingress
    target_port      = var.target_port
    transport        = "auto"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name   = var.name
      image  = var.image
      cpu    = var.cpu
      memory = var.memory

      dynamic "env" {
        for_each = var.env_vars
        content {
          name        = env.value.name
          value       = lookup(env.value, "value", null)
          secret_name = lookup(env.value, "secret_name", null)
        }
      }
    }
  }

  lifecycle {
    # The azure-pipelines.yml CI/CD flow runs `az containerapp update --image ...` on every
    # deployment. Ignore drift on the image tag so Terraform doesn't fight the pipeline between
    # infrastructure applies.
    ignore_changes = [
      template[0].container[0].image,
    ]
  }
}
