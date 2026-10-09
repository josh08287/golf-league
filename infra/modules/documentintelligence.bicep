// modules/documentintelligence.bicep
// Deploys an Azure AI Document Intelligence (Form Recognizer) account used
// for scorecard OCR. Free (F0) tier — one per subscription/region limit.

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Name of the Document Intelligence (Cognitive Services) account.')
param name string

@description('Azure region for this resource.')
param location string

@description('Resource tags to apply.')
param tags object

@description('SKU name. F0 is the free tier (one per subscription/region).')
param skuName string = 'F0'

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'FormRecognizer'
  sku: {
    name: skuName
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    customSubDomainName: name
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('Endpoint URL of the Document Intelligence account.')
output endpoint string = documentIntelligence.properties.endpoint

@description('Name of the Document Intelligence account.')
output name string = documentIntelligence.name
