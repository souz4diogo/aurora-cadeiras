// Compartilhamento de arquivos para as chaves de proteção de dados da Web (cookie de sessão e token anti-CSRF).
// Sem chaves compartilhadas, cada instância do Aurora.Web geraria as próprias e o login não valeria entre elas.

param nome string
param location string
param nomeDoCompartilhamento string = 'chaves-web'
param tags object = {}

resource conta 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: nome
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true // o Container Apps monta o compartilhamento com a chave da conta
    publicNetworkAccess: 'Enabled'
  }
}

resource servicoDeArquivos 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: conta
  name: 'default'
}

resource compartilhamento 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: servicoDeArquivos
  name: nomeDoCompartilhamento
  properties: {
    shareQuota: 1
    enabledProtocols: 'SMB'
  }
}

output nomeDaConta string = conta.name
output nomeDoCompartilhamento string = compartilhamento.name
