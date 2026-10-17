// Copyright © 2026 myNOC LLC. All rights reserved.
targetScope = 'subscription'

@description('Azure region for the resource group and Static Web App.')
param location string = 'eastus2'

@description('Dedicated resource group for the web application.')
param resourceGroupName string = 'rg-morse-code-trainer-prod'

@description('Static Web App resource name, unique within the resource group.')
@minLength(1)
@maxLength(40)
param staticWebAppName string = 'stapp-morse-code-trainer-prod'

@description('Hosting plan. Free is the default; select Standard explicitly if needed.')
@allowed([
  'Free'
  'Standard'
])
param sku string = 'Free'

var tags = {
  application: 'MorseCodeTrainer'
  environment: 'production'
  owner: 'myNOC LLC'
  managedBy: 'Bicep'
}

resource appResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module webApp './modules/static-web-app.bicep' = {
  name: 'morse-code-trainer-web'
  scope: appResourceGroup
  params: {
    location: location
    name: staticWebAppName
    sku: sku
    tags: tags
  }
}

// Deployment credentials must never be ARM outputs.
output resourceGroupName string = appResourceGroup.name
output staticWebAppName string = webApp.outputs.name
output staticWebAppResourceId string = webApp.outputs.resourceId
output staticWebAppUrl string = webApp.outputs.url
