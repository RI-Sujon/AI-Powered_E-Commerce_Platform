# Non-sensitive values for the shared stack. No secrets here - the OpenAI
# account uses Entra ID / managed identity; callers never need a key.

resource_group_name = "ecommerce-rg"
location            = "eastus"

postgres_server_name            = "ecommerce-postgres-rabiuru"
postgres_allowlisted_extensions = ["VECTOR"]

openai_account_name       = "ecommerce-openai-rabiuru"
openai_local_auth_enabled = true
openai_chat_capacity      = 20
openai_embedding_capacity = 20

# Azure Container Registry - alphanumeric only, no hyphens, must be globally unique.
acr_name = "ecommerceacrrabiuru"
acr_sku  = "Basic"
