/*
  Golf League Manager — Main Bicep Deployment
  ============================================

  PREREQUISITES:
    1. Resource group must already exist:
         az group create --name golf-league-prod --location eastus2
    2. Before the first deployment, populate these Key Vault secrets after
       the first apply (the Function App needs them to start cleanly):
         - JwtSigningKey            (>= 32-char random string)
         - AdminBootstrapEmail      (the email of the first admin)
         - GoogleClientId / GoogleClientSecret    (optional, omit to disable)
         - FacebookAppId / FacebookAppSecret      (optional, omit to disable)
         - DocumentIntelligenceKey  (optional, omit to leave scorecard OCR
                                     disabled — falls back to a no-op service)
       The Function App's Managed Identity has Key Vault Secrets User access,
       so it will pick them up on next restart.
    3. Provide the Azure AD principal (user, group, or app) that will be the
       SQL server's Azure AD administrator via aadAdminLogin/aadAdminObjectId.
       The SQL server is deployed with azureADOnlyAuthentication: true — there
       is no SQL-auth admin login/password.

  DEPLOY COMMAND:
    az deployment group create \
      --resource-group golf-league-prod \
      --template-file main.bicep \
      --parameters prod.parameters.json

  POST-DEPLOY (manual steps Bicep cannot perform):
    - Grant the Function App's managed identity a database role in
      capgolfleague so it can run EF Core migrations and query data, e.g.
      connect as the AAD admin and run:
        CREATE USER [<functionAppName>] FROM EXTERNAL PROVIDER;
        ALTER ROLE db_owner ADD MEMBER [<functionAppName>];
    - Add the DNS records Azure emits for the Static Web App custom domains
      (capitalgolfleague.com, www.capitalgolfleague.com) and for the
      Communication Services custom email domain (Domain/SPF TXT, DKIM
      CNAMEs) — see the `emailDomainVerificationRecords` output. Azure
      cannot activate either until DNS validation completes.
    - Populate the Static Web App's GitHub deployment token / connect the
      GitHub Actions workflow (the `repositoryUrl`/`branch` here only records
      metadata; the SWA/GitHub OAuth connection itself must be completed
      once in the Portal or via `swa` CLI).
*/

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Azure region for most resources. SQL is deployed to sqlLocation instead (see below) to match the existing server region.')
param location string

@description('Azure region for the SQL server/database.')
param sqlLocation string = 'canadaeast'

@description('Environment name (e.g. prod). Used in tags and resource names.')
param environmentName string

@description('Base application name used to construct resource names.')
param appName string

@description('Public origin(s) of the web client, comma-separated. Used as WebAuthn allowed origins and the default WEB_BASE_URL for invite links.')
param webOrigin string

@description('WebAuthn relying-party ID (the registrable domain, e.g. "capitalgolfleague.com"). For local dev use "localhost".')
param fido2RpId string = 'localhost'

@description('Array of allowed CORS origins for the Function App (e.g., ["https://app1.com", "https://app2.com"]).')
param allowedOrigins array = []

@description('Azure AD administrator login name for the SQL server (a user UPN or group display name).')
param sqlAadAdminLogin string

@description('Azure AD administrator object (principal) ID for the SQL server.')
param sqlAadAdminObjectId string

@description('Principal type of the SQL AAD admin.')
@allowed([
  'User'
  'Group'
  'Application'
])
param sqlAadAdminPrincipalType string = 'User'

@description('GitHub repository URL backing the Static Web App.')
param webRepositoryUrl string

@description('Branch the Static Web App deploys from.')
param webBranch string = 'master'

@description('Custom domain names to attach to the Static Web App.')
param webCustomDomains array = []

@description('Custom email domain used for transactional email via Communication Services (e.g. "capitalgolfleague.com").')
param emailCustomDomainName string

@description('The "from" address used when sending transactional email, e.g. DoNotReply@<verified-domain>.')
param acsSenderAddress string

@description('Default league name used to seed the first league on a fresh deployment.')
param defaultLeagueName string = 'Capital Golf League'

@description('Default league slug (subdomain-safe identifier) used to seed the first league.')
param defaultLeagueSlug string = 'capital'

// ---------------------------------------------------------------------------
// Variables
// ---------------------------------------------------------------------------

var uniqueSuffix = take(uniqueString(resourceGroup().id), 6)

var tags = {
  application: 'golf-league'
  environment: environmentName
  managedBy: 'bicep'
}

var appInsightsName       = '${appName}-ai-${uniqueSuffix}'
var storageAccountName    = 'glfstr${uniqueSuffix}'
var functionAppName       = '${appName}-fn-${uniqueSuffix}'
var keyVaultName          = '${appName}-kv-${uniqueSuffix}'
var sqlServerName         = 'capgolfleague-eca'
var staticWebAppName      = '${appName}-web'
var communicationName     = '${appName}-com'
var emailServiceName      = 'default-domain-${appName}-${environmentName}'
var docIntelligenceName   = 'golfleague-card-reader'

// Key Vault secret reference syntax for app settings. The Function App's
// system-assigned MI resolves these at runtime — the actual secret values
// are stored only in Key Vault, never in app settings.
var kvBaseUri = 'https://${keyVaultName}${az.environment().suffixes.keyvaultDns}/secrets'

// ---------------------------------------------------------------------------
// Modules
// ---------------------------------------------------------------------------

// 1. Application Insights (Log Analytics workspace + AI component)
module appInsightsModule 'modules/appinsights.bicep' = {
  name: 'appinsights-deploy'
  params: {
    name: appInsightsName
    location: location
    tags: tags
  }
}

// 2. Blob Storage — player photos.
module storageModule 'modules/storage.bicep' = {
  name: 'storage-deploy'
  params: {
    name: storageAccountName
    location: location
    tags: tags
  }
}

// 3. Azure SQL — Azure AD-only authentication, serverless General Purpose DB.
module sqlModule 'modules/sql.bicep' = {
  name: 'sql-deploy'
  params: {
    name: sqlServerName
    location: sqlLocation
    tags: tags
    aadAdminLogin: sqlAadAdminLogin
    aadAdminObjectId: sqlAadAdminObjectId
    aadAdminPrincipalType: sqlAadAdminPrincipalType
  }
}

// 4. Azure AI Document Intelligence — scorecard OCR.
module documentIntelligenceModule 'modules/documentintelligence.bicep' = {
  name: 'docintelligence-deploy'
  params: {
    name: docIntelligenceName
    location: location
    tags: tags
  }
}

// 5. Communication Services + Email (transactional email).
module communicationModule 'modules/communication.bicep' = {
  name: 'communication-deploy'
  params: {
    name: communicationName
    tags: tags
    emailServiceName: emailServiceName
    customDomainName: emailCustomDomainName
  }
}

// 6. Static Web App — React client, GitHub-connected.
module staticWebAppModule 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp-deploy'
  params: {
    name: staticWebAppName
    location: location
    tags: tags
    repositoryUrl: webRepositoryUrl
    branch: webBranch
    customDomainNames: webCustomDomains
  }
}

// 7. Azure Functions (API). Deployed after the resources whose connection
//    info feeds its app settings, so CORS/allowed origins can include the
//    Static Web App's own hostname if desired.
module functionsModule 'modules/functions.bicep' = {
  name: 'functions-deploy'
  params: {
    name: functionAppName
    location: location
    tags: tags
    uniqueSuffix: uniqueSuffix
    appInsightsConnectionString: appInsightsModule.outputs.connectionString
    allowedOrigins: allowedOrigins
  }
}

// 8. Key Vault — grants the Function App Managed Identity the Secrets User role.
module keyVaultModule 'modules/keyvault.bicep' = {
  name: 'keyvault-deploy'
  params: {
    name: keyVaultName
    location: location
    tags: tags
    functionAppPrincipalId: functionsModule.outputs.principalId
  }
}

// ---------------------------------------------------------------------------
// Post-module resource: inject final app settings into the Function App
//
// This pattern breaks any circular dependency: the Function App is deployed
// first with bootstrap settings only, and this resource patches in settings
// that depend on other modules (SQL, Key Vault, Communication Services, etc.)
// once they all exist.
// ---------------------------------------------------------------------------

resource functionAppSettings 'Microsoft.Web/sites/config@2023-01-01' = {
  name: '${functionAppName}/appsettings'
  properties: {
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsModule.outputs.connectionString
    AzureWebJobsStorage__accountName: 'fnstore${uniqueSuffix}'
    WEBSITE_RUN_FROM_PACKAGE: '1'
    WEB_BASE_URL: webOrigin

    // Azure AD-only auth — the connection string carries no credentials.
    // The Function App's managed identity must be granted a database role
    // out of band (see POST-DEPLOY note at the top of this file).
    SQL_CONNECTION_STRING: 'Server=tcp:${sqlModule.outputs.serverFqdn},1433;Initial Catalog=${sqlModule.outputs.databaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication="Active Directory Default";'

    // Transactional email via Azure Communication Services.
    ACS_CONNECTION_STRING: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/AcsConnectionString)'
    ACS_SENDER_ADDRESS: acsSenderAddress

    // Default league seed data for a fresh deployment.
    DEFAULT_LEAGUE_NAME: defaultLeagueName
    DEFAULT_LEAGUE_SLUG: defaultLeagueSlug

    // Local-auth configuration — all sensitive values come from Key Vault.
    // The KV secrets must be created manually after the first apply; see the
    // PREREQUISITES note at the top of this file.
    JWT_SIGNING_KEY: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/JwtSigningKey)'
    ADMIN_BOOTSTRAP_EMAIL: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/AdminBootstrapEmail)'
    GOOGLE_CLIENT_ID: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/GoogleClientId)'
    GOOGLE_CLIENT_SECRET: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/GoogleClientSecret)'
    FACEBOOK_APP_ID: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/FacebookAppId)'
    FACEBOOK_APP_SECRET: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/FacebookAppSecret)'

    // Scorecard OCR (Azure AI Document Intelligence). The endpoint isn't
    // sensitive, but the key is — same Key Vault reference pattern as the
    // other secrets above. Omit the DocumentIntelligenceKey secret entirely
    // to leave the feature disabled (falls back to a no-op OCR service).
    DOCUMENT_INTELLIGENCE_ENDPOINT: documentIntelligenceModule.outputs.endpoint
    DOCUMENT_INTELLIGENCE_KEY: '@Microsoft.KeyVault(SecretUri=${kvBaseUri}/DocumentIntelligenceKey)'

    // Passkey relying-party config — RP ID is the registrable domain
    // (no scheme, no path). Origins must be the full origin(s) clients
    // connect from. Comma-separated for multiple origins (e.g. web + mobile).
    FIDO2_RP_ID: fido2RpId
    FIDO2_RP_NAME: 'Golf League'
    FIDO2_RP_ORIGINS: webOrigin
  }
  dependsOn: [
    keyVaultModule
    communicationModule
  ]
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('HTTPS URL of the deployed Function App.')
output functionAppUrl string = 'https://${functionsModule.outputs.functionAppUrl}'

@description('Name of the deployed Function App.')
output functionAppName string = functionsModule.outputs.functionAppName

@description('Name of the deployed Key Vault.')
output keyVaultName string = keyVaultModule.outputs.name

@description('Name of the storage account.')
output storageAccountName string = storageModule.outputs.storageAccountName

@description('Object (principal) ID of the Function App system-assigned managed identity. Use this when granting the MI access to the SQL database.')
output functionAppPrincipalId string = functionsModule.outputs.principalId

@description('Fully qualified domain name of the SQL server.')
output sqlServerFqdn string = sqlModule.outputs.serverFqdn

@description('Default hostname of the Static Web App.')
output staticWebAppHostname string = staticWebAppModule.outputs.defaultHostname

@description('DNS records required to verify the custom email domain — add these at the registrar before ACS email will send from it.')
output emailDomainVerificationRecords object = communicationModule.outputs.customDomainVerificationRecords
