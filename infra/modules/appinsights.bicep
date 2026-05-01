// modules/appinsights.bicep
// Deploys a Log Analytics workspace and an Application Insights component
// backed by that workspace. Adaptive sampling is set to 10% to stay under
// the 5 GB/month free threshold at golf-league scale.

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Base name used for both the Log Analytics workspace and Application Insights component.')
param name string

@description('Azure region for these resources.')
param location string

@description('Resource tags to apply to all resources in this module.')
param tags object

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${name}-law'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    SamplingPercentage: 10
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('Application Insights instrumentation key (legacy; prefer connectionString for new workloads).')
output instrumentationKey string = appInsights.properties.InstrumentationKey

@description('Application Insights connection string for use in app settings.')
output connectionString string = appInsights.properties.ConnectionString
