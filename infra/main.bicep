// Aurora Cadeiras: infraestrutura de um ambiente (hom ou prod) no Azure (docs/09 §1 e §7).
//
//   az deployment group create -g rg-aurora-<env> -f infra/main.bicep -p infra/parameters.<env>.json \
//     -p sqlAdminPassword=... jwtKey=... alertaEmail=...
//
// Primeira vez: use implantarApps=false para criar só a base (ACR, SQL, Key Vault, monitoramento...), publique as imagens no
// ACR e rode de novo com implantarApps=true. Os deploys seguintes das imagens são feitos pelo pipeline (deploy.yml).

targetScope = 'resourceGroup'

@allowed(['hom', 'prod'])
param ambiente string
param location string = resourceGroup().location

@description('Nome-base dos recursos.')
param nomeBase string = 'aurora'

@description('Login do administrador do SQL.')
param sqlAdminLogin string = 'aurora_admin'
@secure()
@description('Senha do administrador do SQL (vai para o Key Vault).')
param sqlAdminPassword string
@secure()
@description('Chave de assinatura do JWT: texto aleatório de 32 caracteres ou mais (vai para o Key Vault).')
param jwtKey string
@secure()
@description('Senha do administrador inicial do sistema; vazio = não cria o segredo.')
param seedAdminPassword string = ''

@description('E-mail que recebe os alertas do Azure Monitor.')
param alertaEmail string

@description('Camada do Azure SQL, ex.: { name: "Basic", tier: "Basic" }.')
param sqlSku object
param sqlRetencaoDoPitrEmDias int = 7
@allowed(['Basic', 'Standard'])
param acrSku string = 'Basic'
param replicasMinimas int
param replicasMaximas int
param protecaoContraRemocaoDoCofre bool = false
param origensCors array = []

@description('false cria só a base; true cria também os Container Apps (exige as imagens já publicadas no ACR).')
param implantarApps bool = true
@description('Tag das imagens (SHA do commit).')
param tagDasImagens string = 'bootstrap'

var prefixo = '${nomeBase}-${ambiente}'
var sufixoUnico = uniqueString(resourceGroup().id)
var nomeDoBanco = 'aurora'
// Só o fato de existir senha inicial segue adiante; o valor fica no Key Vault
var temSenhaInicial = !empty(seedAdminPassword)
var tags = {
  projeto: 'aurora-cadeiras'
  ambiente: ambiente
  gerenciadoPor: 'bicep'
}

resource identidade 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefixo}'
  location: location
  tags: tags
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    nome: 'acr${nomeBase}${ambiente}${sufixoUnico}'
    location: location
    sku: acrSku
    principalIdDaIdentidade: identidade.properties.principalId
    tags: tags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    nomeDoServidor: 'sql-${prefixo}-${sufixoUnico}'
    nomeDoBanco: nomeDoBanco
    location: location
    administrador: sqlAdminLogin
    senhaDoAdministrador: sqlAdminPassword
    sku: sqlSku
    retencaoDoPitrEmDias: sqlRetencaoDoPitrEmDias
    tags: tags
  }
}

var conexaoDoBanco = 'Server=tcp:${sql.outputs.fqdnDoServidor},1433;Database=${nomeDoBanco};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30'

module cofre 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    nome: 'kv-${nomeBase}-${ambiente}-${take(sufixoUnico, 6)}'
    location: location
    principalIdDaIdentidade: identidade.properties.principalId
    protecaoContraRemocao: protecaoContraRemocaoDoCofre
    chaveDoJwt: jwtKey
    // Por ora a aplicação e o DbUp usam o mesmo login; o ideal é um usuário só com o papel role_aurora_app (evolução)
    conexaoDaAplicacao: conexaoDoBanco
    conexaoDoAdministrador: conexaoDoBanco
    senhaDoAdministradorInicial: seedAdminPassword
    tags: tags
  }
}

module monitor 'modules/monitor.bicep' = {
  name: 'monitor'
  params: {
    prefixo: prefixo
    location: location
    emailDosAlertas: alertaEmail
    nomeDoServidorSql: sql.outputs.nomeDoServidor
    nomeDoBancoSql: sql.outputs.nomeDoBanco
    idDoBancoSql: sql.outputs.idDoBanco
    tags: tags
  }
}

module armazenamento 'modules/storage.bicep' = {
  name: 'armazenamento'
  params: {
    nome: take('st${nomeBase}${ambiente}${sufixoUnico}', 24)
    location: location
    tags: tags
  }
}

module apps 'modules/containerapps.bicep' = if (implantarApps) {
  name: 'containerapps'
  params: {
    prefixo: prefixo
    location: location
    idDaIdentidade: identidade.id
    loginServerDoAcr: acr.outputs.loginServer
    nomeDoLogAnalytics: monitor.outputs.nomeDoLogAnalytics
    conexaoDoInsights: monitor.outputs.conexaoDoInsights
    uriDoKeyVault: cofre.outputs.uri
    temSenhaInicial: temSenhaInicial
    nomeDaContaDeArquivos: armazenamento.outputs.nomeDaConta
    nomeDoCompartilhamento: armazenamento.outputs.nomeDoCompartilhamento
    tagDasImagens: tagDasImagens
    replicasMinimas: replicasMinimas
    replicasMaximas: replicasMaximas
    origensCors: origensCors
    tags: tags
  }
}

module disponibilidade 'modules/disponibilidade.bicep' = if (implantarApps) {
  name: 'disponibilidade'
  params: {
    prefixo: prefixo
    location: location
    nomeDoInsights: monitor.outputs.nomeDoInsights
    idDoInsights: monitor.outputs.idDoInsights
    idDoGrupoDeAcao: monitor.outputs.idDoGrupoDeAcao
    fqdnDaApi: apps!.outputs.fqdnDaApi
    tags: tags
  }
}

output nomeDoAcr string = acr.outputs.nome
output loginServerDoAcr string = acr.outputs.loginServer
output nomeDoKeyVault string = cofre.outputs.nome
output nomeDoServidorSql string = sql.outputs.nomeDoServidor
output nomeDaApi string = implantarApps ? apps!.outputs.nomeDaApi : ''
output nomeDaWeb string = implantarApps ? apps!.outputs.nomeDaWeb : ''
output nomeDoJobDoBanco string = implantarApps ? apps!.outputs.nomeDoJobDoBanco : ''
output enderecoDaApi string = implantarApps ? 'https://${apps!.outputs.fqdnDaApi}' : ''
output enderecoDaWeb string = implantarApps ? 'https://${apps!.outputs.fqdnDaWeb}' : ''
