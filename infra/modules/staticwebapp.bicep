// modules/staticwebapp.bicep
// Deploys the Azure Static Web App (Free tier) hosting the React web client,
// GitHub-connected for CI/CD. Custom domains are declared here but require
// manual DNS validation (TXT/CNAME records) before Azure will activate them —
// see the outputs for the validation token to add at the registrar.

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Name of the Static Web App resource.')
param name string

@description('Azure region for the Static Web App.')
param location string

@description('Resource tags to apply.')
param tags object

@description('GitHub repository URL backing this Static Web App.')
param repositoryUrl string

@description('Branch to deploy from.')
param branch string = 'master'

@description('Custom domain names to attach (e.g. ["capitalgolfleague.com", "www.capitalgolfleague.com"]). Each requires DNS validation before it becomes active.')
param customDomainNames array = []

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    repositoryUrl: repositoryUrl
    branch: branch
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
  }
}

resource customDomains 'Microsoft.Web/staticSites/customDomains@2023-01-01' = [for domainName in customDomainNames: {
  parent: staticWebApp
  name: domainName
}]

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('Default *.azurestaticapps.net hostname.')
output defaultHostname string = staticWebApp.properties.defaultHostname

@description('Name of the Static Web App.')
output name string = staticWebApp.name
