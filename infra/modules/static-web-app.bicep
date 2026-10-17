// Copyright © 2026 myNOC LLC. All rights reserved.
targetScope = 'resourceGroup'

@description('Region supporting Azure Static Web Apps.')
param location string

@description('Static Web App resource name.')
param name string

@description('Static Web Apps hosting plan.')
@allowed([
  'Free'
  'Standard'
])
param sku string

@description('Resource ownership and lifecycle tags.')
param tags object

resource staticWebApp 'Microsoft.Web/staticSites@2025-03-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
    tier: sku
  }
  properties: {
    provider: 'Custom'
    publicNetworkAccess: 'Enabled'
    stagingEnvironmentPolicy: 'Disabled'
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
    }
  }
}

output name string = staticWebApp.name
output resourceId string = staticWebApp.id
output url string = 'https://${staticWebApp.properties.defaultHostname}'
