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
  tags                         = var.tags

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

      # HTTP probes are added only when the caller passes health_probe_path (the API passes
      # "/health"; the Web app leaves it null and keeps Container Apps' default TCP check).
      # - startup:   generous window so a slow first boot / cold DB isn't killed
      # - liveness:  restarts a hung container
      # - readiness: holds traffic off a replica that isn't ready yet (matters during a rollout)
      dynamic "startup_probe" {
        for_each = var.health_probe_path == null ? [] : [1]
        content {
          transport               = "HTTP"
          port                    = var.target_port
          path                    = var.health_probe_path
          interval_seconds        = 10
          timeout                 = 5
          failure_count_threshold = 10 # azurerm caps this at 10; 10 x 10s = ~100s startup grace
        }
      }

      dynamic "liveness_probe" {
        for_each = var.health_probe_path == null ? [] : [1]
        content {
          transport               = "HTTP"
          port                    = var.target_port
          path                    = var.health_probe_path
          initial_delay           = 15
          interval_seconds        = 20
          timeout                 = 5
          failure_count_threshold = 3
        }
      }

      dynamic "readiness_probe" {
        for_each = var.health_probe_path == null ? [] : [1]
        content {
          transport               = "HTTP"
          port                    = var.target_port
          path                    = var.health_probe_path
          interval_seconds        = 10
          timeout                 = 5
          failure_count_threshold = 3
          success_count_threshold = 1
        }
      }
    }
  }

  lifecycle {
    ignore_changes = [
      # azurerm 3.x reports `workload_profile_name = "Consumption"` on every refresh for apps in a
      # Consumption-only environment and then wants to null it - a provider quirk with no runtime
      # effect. Ignoring it keeps `terraform plan` clean.
      workload_profile_name,
      # `revision_suffix` is only ever set out-of-band (`az containerapp update --revision-suffix`)
      # to force a new revision after a secret-only change - Terraform doesn't manage it. A real
      # deploy rolls a revision via the image tag change instead. See terraform/README.md section 8.
      template[0].revision_suffix,
    ]
  }

  # NOTE: the image is deliberately NOT ignored. azure-pipelines.yml deploys by running
  # `terraform apply -var image_tag=<build id>`, so Terraform is the single source of truth for
  # the running image. Each environment's terraform.tfvars pins `image_tag` to the currently
  # deployed build so a no-arg `terraform apply` is a no-op; the pipeline overrides it per run.
}
