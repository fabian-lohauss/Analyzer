targetScope = 'resourceGroup'

@description('Globally unique Azure App Service name.')
@minLength(3)
param appName string

param location string = resourceGroup().location

@allowed([
  'B1'
  'S1'
])
param appServiceSku string = 'S1'

var storageName = 'wdsf${take(toLower(replace('${appName}${uniqueString(resourceGroup().id)}', '-', '')), 20)}'
var blobContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
)

resource storage 'Microsoft.Storage/storageAccounts@2025-06-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: true
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2025-06-01' = {
  parent: storage
  name: 'default'
}

resource cacheContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-06-01' = {
  parent: blobService
  name: 'cache'
  properties: {
    publicAccess: 'Blob'
  }
}

resource plan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: appServiceSku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource app 'Microsoft.Web/sites@2024-11-01' = {
  name: appName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: appServiceSku != 'B1'
      ftpsState: 'Disabled'
      healthCheckPath: '/health'
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'Storage__ServiceUri'
          value: storage.properties.primaryEndpoints.blob
        }
        {
          name: 'Storage__ContainerName'
          value: cacheContainer.name
        }
      ]
    }
  }
}

resource staging 'Microsoft.Web/sites/slots@2024-11-01' = if (appServiceSku != 'B1') {
  parent: app
  name: 'staging'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      healthCheckPath: '/health'
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'Storage__ServiceUri'
          value: storage.properties.primaryEndpoints.blob
        }
        {
          name: 'Storage__ContainerName'
          value: cacheContainer.name
        }
      ]
    }
  }
}

module appBlobAccess './blob-role-assignment.bicep' = {
  params: {
    storageName: storage.name
    principalId: app.identity.principalId
    roleDefinitionId: blobContributorRoleId
  }
}

module stagingBlobAccess './blob-role-assignment.bicep' = if (appServiceSku != 'B1') {
  params: {
    storageName: storage.name
    principalId: staging!.identity.principalId
    roleDefinitionId: blobContributorRoleId
  }
}

output appUrl string = 'https://${app.properties.defaultHostName}'
output blobCacheUrl string = '${storage.properties.primaryEndpoints.blob}${cacheContainer.name}/'
