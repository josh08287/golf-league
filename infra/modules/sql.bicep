// modules/sql.bicep
// Deploys an Azure SQL logical server (Azure AD-only authentication) and a
// serverless General Purpose database. No SQL-auth admin login/password is
// used — the server trusts Azure AD exclusively, and the Function App's
// managed identity is expected to be granted DB access (db_owner or similar)
// out of band via a post-deploy T-SQL script, since Bicep cannot run
// arbitrary T-SQL against the database itself.

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Name of the SQL logical server. Must be globally unique.')
param name string

@description('Azure region for the SQL server and database.')
param location string

@description('Resource tags to apply.')
param tags object

@description('Azure AD administrator login name (e.g. a user UPN or group name) for the SQL server.')
param aadAdminLogin string

@description('Azure AD administrator object (principal) ID for the SQL server.')
param aadAdminObjectId string

@description('Azure AD tenant ID that the SQL server\'s AAD admin belongs to.')
param aadAdminTenantId string = subscription().tenantId

@description('Principal type of the AAD admin (User, Group, or Application).')
@allowed([
  'User'
  'Group'
  'Application'
])
param aadAdminPrincipalType string = 'User'

@description('Name of the application database.')
param databaseName string = 'capgolfleague'

@description('IP-range firewall rules to allow (e.g. developer workstations). Azure services are always allowed via a separate rule.')
param firewallRules array = []

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      login: aadAdminLogin
      sid: aadAdminObjectId
      tenantId: aadAdminTenantId
      principalType: aadAdminPrincipalType
      azureADOnlyAuthentication: true
    }
  }
}

// Allow Azure services (Function App, portal query editor, etc.) to reach the server.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource additionalFirewallRules 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = [for rule in firewallRules: {
  parent: sqlServer
  name: rule.name
  properties: {
    startIpAddress: rule.startIpAddress
    endIpAddress: rule.endIpAddress
  }
}]

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    maxSizeBytes: 2147483648 // 2 GB
    zoneRedundant: false
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('Fully qualified domain name of the SQL server.')
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('Name of the SQL server.')
output serverName string = sqlServer.name

@description('Name of the database.')
output databaseName string = database.name
