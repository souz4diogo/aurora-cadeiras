// Disponibilidade da API (docs/09 §4): teste de disponibilidade em /health/ready a cada 5 minutos e alerta quando
// falha em mais de uma região por 3 minutos. Fica separado do monitor.bicep porque precisa do endereço do Container
// App, que só existe depois de ele ser criado.

param prefixo string
param location string
param nomeDoInsights string
param idDoInsights string
param idDoGrupoDeAcao string
@description('Endereço público da API, sem https://.')
param fqdnDaApi string
param tags object = {}

var nomeDoTeste = 'disp-${prefixo}-api'

resource teste 'Microsoft.Insights/webtests@2022-06-15' = {
  name: nomeDoTeste
  location: location
  tags: union(tags, { 'hidden-link:${idDoInsights}': 'Resource' })
  kind: 'standard'
  properties: {
    SyntheticMonitorId: nomeDoTeste
    Name: 'API pronta (/health/ready)'
    Enabled: true
    Frequency: 300
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [
      { Id: 'emea-nl-ams-azr' }
      { Id: 'us-fl-mia-edge' }
      { Id: 'latam-br-gru-edge' }
    ]
    Request: {
      RequestUrl: 'https://${fqdnDaApi}/health/ready'
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

resource alerta 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alerta-${prefixo}-api-indisponivel'
  location: 'global'
  tags: tags
  properties: {
    description: 'A API não respondeu 200 em /health/ready, em mais de uma região, por 3 minutos.'
    severity: 0
    enabled: true
    scopes: [teste.id, idDoInsights]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: teste.id
      componentId: idDoInsights
      failedLocationCount: 2
    }
    actions: [{ actionGroupId: idDoGrupoDeAcao }]
  }
}

output nomeDoInsights string = nomeDoInsights
