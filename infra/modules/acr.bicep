// Container Registry: guarda as imagens (aurora-api, aurora-web, aurora-dbup) marcadas com o SHA do commit.
// Sem usuário administrador: quem puxa as imagens é a identidade gerenciada (papel AcrPull).

param nome string
param location string
param sku string
@description('Id (principalId) da identidade gerenciada que puxa as imagens.')
param principalIdDaIdentidade string
param tags object = {}

// AcrPull
var papelAcrPull = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource registro 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: nome
  location: location
  tags: tags
  sku: { name: sku }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource puxar 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registro.id, principalIdDaIdentidade, papelAcrPull)
  scope: registro
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', papelAcrPull)
    principalId: principalIdDaIdentidade
    principalType: 'ServicePrincipal'
  }
}

output loginServer string = registro.properties.loginServer
output nome string = registro.name
