// modules/keyvault.bicep
// Deploys an Azure Key Vault and grants the Function App Managed Identity
// the Key Vault Secrets User role via RBAC so it can read secrets at runtime.
// RBAC authorization is used (not legacy access policies) — enableRbacAuthorization: true.

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Name of the Key Vault resource.')
param name string

@description('Azure region for this resource.')
param location string

@description('Resource tags to apply.')
param tags object

@description('Object (principal) ID of the Function App system-assigned Managed Identity. This principal is granted the Key Vault Secrets User role.')
param functionAppPrincipalId string

// ---------------------------------------------------------------------------
// Variables
// ---------------------------------------------------------------------------

// Key Vault Secrets User built-in role definition ID (subscription-scoped GUID)
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
    enableRbacAuthorization: true
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    publicNetworkAccess: 'Enabled'
  }
}

// Grant the Function App Managed Identity the Key Vault Secrets User role
// so it can read secrets (e.g. SqlConnectionString) at runtime.
resource functionAppSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  // Role assignment name must be a deterministic GUID derived from the scope + role + principal
  name: guid(keyVault.id, functionAppPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('URI of the Key Vault (e.g. https://my-kv-abc123.vault.azure.net/).')
output vaultUri string = keyVault.properties.vaultUri

@description('Name of the deployed Key Vault.')
output name string = keyVault.name
