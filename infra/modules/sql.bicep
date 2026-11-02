// Azure SQL: servidor e banco, com TDE, TLS 1.2, backup com restauração a qualquer ponto (PITR) e auditoria.
// O firewall libera só os serviços do Azure (regra 0.0.0.0); o ideal, como evolução, é um private endpoint.

param nomeDoServidor string
param nomeDoBanco string
param location string
param administrador string
@secure()
param senhaDoAdministrador string
@description('Camada do banco, ex.: { name: "Basic", tier: "Basic" } ou { name: "S1", tier: "Standard" }.')
param sku object
@description('Dias de retenção do PITR (de 1 a 35).')
@minValue(1)
@maxValue(35)
param retencaoDoPitrEmDias int = 7
param tags object = {}

resource servidor 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: nomeDoServidor
  location: location
  tags: tags
  properties: {
    administratorLogin: administrador
    administratorLoginPassword: senhaDoAdministrador
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource liberarServicosDoAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: servidor
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource banco 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: servidor
  name: nomeDoBanco
  location: location
  tags: tags
  sku: sku
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    requestedBackupStorageRedundancy: 'Local'
  }
}

resource criptografia 'Microsoft.Sql/servers/databases/transparentDataEncryption@2023-08-01-preview' = {
  parent: banco
  name: 'current'
  properties: { state: 'Enabled' }
}

resource backup 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-08-01-preview' = {
  parent: banco
  name: 'default'
  properties: {
    retentionDays: retencaoDoPitrEmDias
    diffBackupIntervalInHours: 12
  }
}

// Auditoria do servidor enviada ao Azure Monitor (o destino é configurado no módulo monitor)
resource auditoria 'Microsoft.Sql/servers/auditingSettings@2023-08-01-preview' = {
  parent: servidor
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
    auditActionsAndGroups: [
      'SUCCESSFUL_DATABASE_AUTHENTICATION_GROUP'
      'FAILED_DATABASE_AUTHENTICATION_GROUP'
      'BATCH_COMPLETED_GROUP'
    ]
  }
}

output nomeDoServidor string = servidor.name
output fqdnDoServidor string = servidor.properties.fullyQualifiedDomainName
output nomeDoBanco string = banco.name
output idDoBanco string = banco.id
