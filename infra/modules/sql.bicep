// modules/sql.bicep
// Deploys an Azure SQL logical server and a Serverless (auto-pause) database.
// The Function App Managed Identity is set as the AAD administrator so the
// application can connect without a SQL password using Managed Identity auth.
// SQL admin login/password are retained for emergency access and EF Core migrations.
//
// Database spec:
//   - SKU: General Purpose Serverless, Gen5, 1 vCore
//   - Auto-pause: 60 minutes idle
//   - Min capacity: 0.5 vCores (lowest billable unit)
//   - Max size: 2 GB
//   - No zone redundancy (not justified at this scale)

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Name of the SQL logical server.')
param name string

@description('Azure region for this resource.')
param location string

@description('Resource tags to apply.')
param tags object

@description('SQL administrator login name for emergency/migration access.')
param administratorLogin string

@description('SQL administrator password. Must be complex and kept in Key Vault — never commit to source control.')
@secure()
param administratorLoginPassword string

@description('Object (principal) ID of the Function App Managed Identity. Set as the Azure AD administrator on the SQL server.')
param functionAppPrincipalId string

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

resource sqlServer 'Microsoft.Sql/servers@2023-02-01-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    // Set the Function App Managed Identity as the Azure AD admin.
    // This allows the application to authenticate via Managed Identity
    // without a SQL password in app settings.
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: false
      login: 'golf-league-functions-mi'
      principalType: 'Application'
      sid: functionAppPrincipalId
      tenantId: subscription().tenantId
    }
  }
}

// Allow Azure-internal services (Functions, etc.) to reach the SQL server.
// 0.0.0.0 / 0.0.0.0 is the Azure services bypass rule — it does NOT open
// the server to the public internet.
resource allowAzureServicesFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-02-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Serverless database: auto-pauses after 60 minutes idle, resumes on first connection.
// Cold-start resume adds ~20-30s to the first query after idle — acceptable for this workload.
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-02-01-preview' = {
  parent: sqlServer
  name: 'golf-league-db'
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: json('0.5')
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('Fully qualified domain name of the SQL Server (e.g. golf-league-sql-abc123.database.windows.net).')
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('Name of the deployed database.')
output databaseName string = sqlDatabase.name
