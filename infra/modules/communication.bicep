// modules/communication.bicep
// Deploys Azure Communication Services with an Email Communication Service
// and a customer-managed email domain for transactional email (invites,
// notifications). Domain ownership (DKIM/SPF/DMARC/Domain TXT records) must
// be verified manually at the DNS registrar after deploy — Bicep can create
// the domain resource but cannot complete third-party DNS verification.

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Name of the Communication Services resource.')
param name string

@description('Resource tags to apply.')
param tags object

@description('Data residency location for Communication/Email services (not an Azure region — one of the ACS-supported data locations, e.g. "United States").')
param dataLocation string = 'United States'

@description('Name of the Email Communication Service resource.')
param emailServiceName string

@description('Custom domain name to send email from (e.g. "capitalgolfleague.com").')
param customDomainName string

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: emailServiceName
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
  }
}

// Azure-managed subdomain (*.azurecomm.net) — usable immediately, no DNS setup.
resource azureManagedDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
  }
}

// Customer-managed custom domain — requires manual DNS verification
// (Domain TXT, SPF TXT, DKIM CNAMEs) at the registrar post-deploy.
resource customDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: customDomainName
  location: 'global'
  properties: {
    domainManagement: 'CustomerManaged'
    userEngagementTracking: 'Disabled'
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: name
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
    linkedDomains: [
      azureManagedDomain.id
      customDomain.id
    ]
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('Name of the Communication Services resource.')
output name string = communicationService.name

@description('DNS records required to verify the custom email domain (add these at the registrar).')
output customDomainVerificationRecords object = customDomain.properties.verificationRecords
