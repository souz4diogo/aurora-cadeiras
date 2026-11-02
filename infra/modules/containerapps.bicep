// Container Apps: ambiente, API, Web e o job do DbUp.
// - Identidade gerenciada: puxa imagens do ACR e lê segredos do Key Vault (nada de senha no template).
// - Revisões múltiplas: o deploy cria a revisão nova e o pipeline só mantém o tráfego nela se o smoke test passar.
// - Escala por requisições simultâneas e por CPU; API sem estado, então pode ter várias instâncias.

param prefixo string
param location string
param idDaIdentidade string
param loginServerDoAcr string
param nomeDoLogAnalytics string
@secure()
param conexaoDoInsights string
param uriDoKeyVault string
param temSenhaInicial bool
param nomeDaContaDeArquivos string
param nomeDoCompartilhamento string
@description('Tag das imagens (SHA do commit).')
param tagDasImagens string
param replicasMinimas int
param replicasMaximas int
@description('Origens liberadas no CORS da API (clientes que rodam em navegador). O app mobile e a Web não precisam.')
param origensCors array = []
param tags object = {}

var portaDaAplicacao = 8080

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: nomeDoLogAnalytics
}

resource conta 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: nomeDaContaDeArquivos
}

resource ambiente 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${prefixo}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource armazenamento 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: ambiente
  name: 'chaves-web'
  properties: {
    azureFile: {
      accountName: conta.name
      accountKey: conta.listKeys().keys[0].value
      shareName: nomeDoCompartilhamento
      accessMode: 'ReadWrite'
    }
  }
}

var identidade = {
  type: 'UserAssigned'
  userAssignedIdentities: { '${idDaIdentidade}': {} }
}

var registros = [
  {
    server: loginServerDoAcr
    identity: idDaIdentidade
  }
]

var segredoDoInsights = [
  { name: 'appinsights-connection', value: conexaoDoInsights }
]

var variaveisComuns = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'ASPNETCORE_URLS', value: 'http://+:${portaDaAplicacao}' }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'appinsights-connection' }
]

var segredosDaApi = concat(
  [
    { name: 'jwt-key', keyVaultUrl: '${uriDoKeyVault}secrets/jwt-key', identity: idDaIdentidade }
    { name: 'sql-connection', keyVaultUrl: '${uriDoKeyVault}secrets/sql-connection', identity: idDaIdentidade }
  ],
  temSenhaInicial
    ? [{ name: 'seed-admin-password', keyVaultUrl: '${uriDoKeyVault}secrets/seed-admin-password', identity: idDaIdentidade }]
    : [],
  segredoDoInsights
)

var variaveisDaApi = concat(
  variaveisComuns,
  [
    { name: 'ConnectionStrings__Aurora', secretRef: 'sql-connection' }
    { name: 'Jwt__Key', secretRef: 'jwt-key' }
    { name: 'Jwt__Issuer', value: 'aurora-api' }
    { name: 'Jwt__Audience', value: 'aurora-clientes' }
    // Um só proxy confiável antes da API: o ingress do Container Apps
    { name: 'ForwardedHeaders__Habilitado', value: 'true' }
    { name: 'ForwardedHeaders__LimiteDeSaltos', value: '1' }
    { name: 'Swagger__Habilitado', value: 'false' }
  ],
  temSenhaInicial ? [{ name: 'SEED_ADMIN_PASSWORD', secretRef: 'seed-admin-password' }] : [],
  map(range(0, length(origensCors)), i => { name: 'Cors__Origens__${i}', value: origensCors[i] })
)

var regrasDeEscala = [
  {
    name: 'requisicoes-simultaneas'
    http: { metadata: { concurrentRequests: '50' } }
  }
  {
    name: 'cpu'
    custom: {
      type: 'cpu'
      metadata: { type: 'Utilization', value: '70' }
    }
  }
]

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${prefixo}-api'
  location: location
  tags: union(tags, { 'azd-service-name': 'api' })
  identity: identidade
  properties: {
    managedEnvironmentId: ambiente.id
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: {
        external: true // o app mobile chama a API pela internet
        targetPort: portaDaAplicacao
        transport: 'auto'
        allowInsecure: false
        traffic: [{ latestRevision: true, weight: 100 }]
      }
      registries: registros
      secrets: segredosDaApi
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${loginServerDoAcr}/aurora-api:${tagDasImagens}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: variaveisDaApi
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: portaDaAplicacao }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: portaDaAplicacao }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: replicasMinimas
        maxReplicas: replicasMaximas
        rules: regrasDeEscala
      }
    }
  }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${prefixo}-web'
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  identity: identidade
  properties: {
    managedEnvironmentId: ambiente.id
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: {
        external: true
        targetPort: portaDaAplicacao
        transport: 'auto'
        allowInsecure: false
        traffic: [{ latestRevision: true, weight: 100 }]
      }
      registries: registros
      secrets: segredoDoInsights
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${loginServerDoAcr}/aurora-web:${tagDasImagens}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: concat(variaveisComuns, [
            { name: 'Api__BaseUrl', value: 'https://${api.properties.configuration.ingress.fqdn}' }
            // Chaves do cookie e do anti-CSRF compartilhadas entre as instâncias (e entre revisões)
            { name: 'DataProtection__CaminhoDasChaves', value: '/chaves' }
          ])
          volumeMounts: [{ volumeName: 'chaves', mountPath: '/chaves' }]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: portaDaAplicacao }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health', port: portaDaAplicacao }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
          ]
        }
      ]
      volumes: [
        {
          name: 'chaves'
          storageType: 'AzureFile'
          storageName: armazenamento.name
          // O processo roda como o usuário "app" da imagem (uid 1654)
          mountOptions: 'uid=1654,gid=1654,dir_mode=0770,file_mode=0660'
        }
      ]
      scale: {
        minReplicas: replicasMinimas
        maxReplicas: replicasMaximas
        rules: regrasDeEscala
      }
    }
  }
}

// Aplica os scripts numerados do banco (DbUp). Disparado pelo pipeline antes de cada deploy; scripts já aplicados são ignorados.
resource jobDoBanco 'Microsoft.App/jobs@2024-03-01' = {
  name: 'job-${prefixo}-dbup'
  location: location
  tags: tags
  identity: identidade
  properties: {
    environmentId: ambiente.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 600
      replicaRetryLimit: 0
      manualTriggerConfig: { parallelism: 1, replicaCompletionCount: 1 }
      registries: registros
      secrets: [
        { name: 'sql-admin-connection', keyVaultUrl: '${uriDoKeyVault}secrets/sql-admin-connection', identity: idDaIdentidade }
      ]
    }
    template: {
      containers: [
        {
          name: 'dbup'
          image: '${loginServerDoAcr}/aurora-dbup:${tagDasImagens}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [{ name: 'ConnectionStrings__Aurora', secretRef: 'sql-admin-connection' }]
        }
      ]
    }
  }
}

output nomeDaApi string = api.name
output nomeDaWeb string = web.name
output nomeDoJobDoBanco string = jobDoBanco.name
output fqdnDaApi string = api.properties.configuration.ingress.fqdn
output fqdnDaWeb string = web.properties.configuration.ingress.fqdn
