// Key Vault: guarda os segredos (chave do JWT, connection strings, senha do administrador inicial).
// Acesso por RBAC: a identidade gerenciada dos Container Apps recebe só o papel "Key Vault Secrets User".
// Quem implanta este módulo precisa poder gravar segredos (papel "Key Vault Secrets Officer" no cofre ou Owner).

param nome string
param location string
param principalIdDaIdentidade string
@description('Purge protection impede apagar o cofre de vez durante a retenção; ligar em produção.')
param protecaoContraRemocao bool = false
@secure()
param chaveDoJwt string
@secure()
param conexaoDaAplicacao string
@secure()
param conexaoDoAdministrador string
@secure()
param senhaDoAdministradorInicial string = ''
param tags object = {}

// Key Vault Secrets User
var papelLeitorDeSegredos = '4633458b-17de-408a-b874-0f2cd2d4c0b0'

resource cofre 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: nome
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 30
    enablePurgeProtection: protecaoContraRemocao ? true : null
    publicNetworkAccess: 'Enabled'
  }
}

resource ler 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cofre.id, principalIdDaIdentidade, papelLeitorDeSegredos)
  scope: cofre
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', papelLeitorDeSegredos)
    principalId: principalIdDaIdentidade
    principalType: 'ServicePrincipal'
  }
}

resource jwt 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: cofre
  name: 'jwt-key'
  properties: { value: chaveDoJwt }
}

resource conexaoApp 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: cofre
  name: 'sql-connection'
  properties: { value: conexaoDaAplicacao }
}

resource conexaoAdmin 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: cofre
  name: 'sql-admin-connection'
  properties: { value: conexaoDoAdministrador }
}

resource senhaInicial 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(senhaDoAdministradorInicial)) {
  parent: cofre
  name: 'seed-admin-password'
  properties: { value: senhaDoAdministradorInicial }
}

output uri string = cofre.properties.vaultUri
output nome string = cofre.name
