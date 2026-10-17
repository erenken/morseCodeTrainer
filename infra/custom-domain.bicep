// Copyright © 2026 myNOC LLC. All rights reserved.
targetScope = 'resourceGroup'

@description('Existing Static Web App resource name.')
param staticWebAppName string = 'stapp-morse-code-trainer-prod'

@description('Custom subdomain whose public CNAME already points to the Static Web App default hostname.')
param hostname string = 'morsecode.mynoc.com'

resource staticWebApp 'Microsoft.Web/staticSites@2025-03-01' existing = {
  name: staticWebAppName
}

// Azure provisions and renews the TLS certificate after CNAME validation succeeds.
resource customDomain 'Microsoft.Web/staticSites/customDomains@2025-03-01' = {
  parent: staticWebApp
  name: hostname
  properties: {
    validationMethod: 'cname-delegation'
  }
}

output hostname string = customDomain.name
output url string = 'https://${customDomain.name}'
