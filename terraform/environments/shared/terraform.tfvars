# Non-sensitive values for the shared stack. No secrets here - the OpenAI
# account uses Entra ID / managed identity; callers never need a key.

resource_group_name = "ecommerce-rg"
location            = "eastus"

openai_account_name       = "ecommerce-openai-sujon"
openai_local_auth_enabled = true
openai_chat_capacity      = 20
openai_embedding_capacity = 20
