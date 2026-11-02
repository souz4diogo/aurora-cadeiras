// Monitoramento (docs/09 §4): Log Analytics, Application Insights, grupo de ação e alertas.
// Alertas: erros 5xx acima de 2% em 5 min, p95 acima de 2 s por 10 min e CPU do banco acima de 80% por 10 min.
// A disponibilidade da API fica no módulo disponibilidade.bicep (precisa do endereço do Container App, criado depois).
// Pendente: alerta de segredo prestes a expirar (exige Event Grid do Key Vault).

param prefixo string
param location string
param retencaoDosLogsEmDias int = 30
@description('E-mail que recebe os alertas.')
param emailDosAlertas string
param nomeDoServidorSql string
param nomeDoBancoSql string
param idDoBancoSql string
param tags object = {}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${prefixo}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: retencaoDosLogsEmDias
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${prefixo}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
    IngestionMode: 'LogAnalytics'
    DisableLocalAuth: false
  }
}

resource grupoDeAcao 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-${prefixo}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'aurora'
    enabled: true
    emailReceivers: [
      {
        name: 'grupo'
        emailAddress: emailDosAlertas
        useCommonAlertSchema: true
      }
    ]
  }
}

// Auditoria do Azure SQL (RNF10 na infraestrutura): vai para o Log Analytics
resource servidor 'Microsoft.Sql/servers@2023-08-01-preview' existing = {
  name: nomeDoServidorSql
}

resource bancoMaster 'Microsoft.Sql/servers/databases@2023-08-01-preview' existing = {
  parent: servidor
  name: 'master'
}

resource auditoriaDoSql 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'auditoria-para-log-analytics'
  scope: bancoMaster
  properties: {
    workspaceId: logs.id
    logs: [
      { category: 'SQLSecurityAuditEvents', enabled: true }
    ]
  }
}

resource banco 'Microsoft.Sql/servers/databases@2023-08-01-preview' existing = {
  parent: servidor
  name: nomeDoBancoSql
}

resource metricasDoBanco 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'metricas-para-log-analytics'
  scope: banco
  properties: {
    workspaceId: logs.id
    logs: [
      { category: 'Deadlocks', enabled: true }
      { category: 'Errors', enabled: true }
      { category: 'Timeouts', enabled: true }
    ]
    metrics: [
      { category: 'Basic', enabled: true }
    ]
  }
}

resource alertaDeCpuDoBanco 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alerta-${prefixo}-cpu-do-banco'
  location: 'global'
  tags: tags
  properties: {
    description: 'CPU do Azure SQL acima de 80% por 10 minutos: avaliar escala (vCore/DTU).'
    severity: 2
    enabled: true
    scopes: [idDoBancoSql]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT10M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'cpu'
          criterionType: 'StaticThresholdCriterion'
          metricName: 'cpu_percent'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          operator: 'GreaterThan'
          threshold: 80
          timeAggregation: 'Average'
        }
      ]
    }
    actions: [{ actionGroupId: grupoDeAcao.id }]
  }
}

resource alertaDeErros5xx 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alerta-${prefixo}-erros-5xx'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    displayName: 'Erros 5xx acima de 2% em 5 minutos'
    description: 'Mais de 2% das requisições da API ou da Web responderam 5xx nos últimos 5 minutos.'
    severity: 1
    enabled: true
    scopes: [insights.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'requests | summarize total = count(), falhas = countif(toint(resultCode) >= 500) | where total >= 20 and falhas * 100.0 / total > 2'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: { numberOfEvaluationPeriods: 1, minFailingPeriodsToAlert: 1 }
        }
      ]
    }
    actions: { actionGroups: [grupoDeAcao.id] }
  }
}

resource alertaDeLatencia 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alerta-${prefixo}-latencia-p95'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    displayName: 'Latência p95 acima de 2 s por 10 minutos'
    description: 'O percentil 95 do tempo de resposta passou de 2 segundos nos últimos 10 minutos.'
    severity: 2
    enabled: true
    scopes: [insights.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT10M'
    criteria: {
      allOf: [
        {
          query: 'requests | summarize total = count(), p95 = percentile(duration, 95) | where total >= 20 and p95 > 2000'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: { numberOfEvaluationPeriods: 1, minFailingPeriodsToAlert: 1 }
        }
      ]
    }
    actions: { actionGroups: [grupoDeAcao.id] }
  }
}

output idDoLogAnalytics string = logs.id
output nomeDoLogAnalytics string = logs.name
output idDoInsights string = insights.id
output nomeDoInsights string = insights.name
output idDoGrupoDeAcao string = grupoDeAcao.id
#disable-next-line outputs-should-not-contain-secrets
output conexaoDoInsights string = insights.properties.ConnectionString
