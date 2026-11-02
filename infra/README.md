# Infraestrutura em nuvem (Azure, Bicep)

Implementa o projeto do `docs/09-nuvem-devops.md`. **Nada aqui foi aplicado em uma assinatura**: o grupo ainda não tem
uma. O que foi verificado: o Bicep compila e passa no linter (`az bicep build`), e os três workflows passam no
`actionlint`. O `what-if` só roda com uma assinatura.

## O que cria

| Módulo | Recursos |
|---|---|
| `main.bicep` | Orquestra tudo por ambiente (`hom` ou `prod`) e cria a identidade gerenciada |
| `modules/acr.bicep` | Container Registry (sem usuário administrador; a identidade tem só `AcrPull`) |
| `modules/sql.bicep` | Servidor e banco Azure SQL: TDE, TLS 1.2, PITR, auditoria, firewall só para serviços do Azure |
| `modules/keyvault.bicep` | Key Vault com RBAC; segredos `jwt-key`, `sql-connection`, `sql-admin-connection`, `seed-admin-password` |
| `modules/storage.bicep` | Compartilhamento de arquivos com as chaves de proteção de dados da Web (cookie e anti-CSRF) |
| `modules/monitor.bicep` | Log Analytics, Application Insights, grupo de ação e alertas (5xx, p95, CPU do banco) |
| `modules/containerapps.bicep` | Ambiente, API, Web e o *job* do DbUp; escala por requisições e CPU; revisões múltiplas |
| `modules/disponibilidade.bicep` | Teste de disponibilidade em `/health/ready` e alerta de API indisponível |

## Ordem da primeira implantação

1. Crie o grupo de recursos (`rg-aurora-hom`) e um *app registration* com credencial federada (OIDC) para o repositório
   (ambiente `hom`). Dê a ele **Contributor** e **User Access Administrator** no grupo (o Bicep cria atribuições de
   papel) e **Key Vault Secrets Officer** depois que o cofre existir (ou use Owner no grupo).
2. No GitHub, crie os *Environments* `hom` e `prod` (em `prod`, marque **Required reviewers**) com as variáveis
   `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `ACR_NAME`, `API_APP`,
   `WEB_APP`, `DBUP_JOB`, `ALERTA_EMAIL` e os segredos `SQL_ADMIN_PASSWORD`, `JWT_KEY`, `SEED_ADMIN_PASSWORD`.
   Variáveis do repositório: `ACR_HOM` (nome do ACR de hom, usado na promoção) e `AZURE_DEPLOY_ATIVO=true` (só então o
   `deploy.yml` roda).
3. Rode o workflow **Infraestrutura** com `acao=what-if`, depois `deploy` e `implantar-apps=false`: cria a base (ACR,
   SQL, Key Vault, monitoramento, armazenamento). Copie os nomes (ACR, apps e job) para as variáveis do Environment.
4. Faça um merge na `main`: o **Deploy** publica as imagens no ACR (tag = SHA). Rode **Infraestrutura** de novo com
   `implantar-apps=true` e `tag-das-imagens=<sha>` para criar os Container Apps e o job do DbUp.
5. Dali em diante, cada merge na `main` implanta em **hom**; a tag `v*` (ou execução manual escolhendo `prod`) promove a
   **mesma imagem** para **prod**, depois da aprovação.

## O que o pipeline faz a cada deploy (`deploy.yml` e `implantar.yml`)

1. Imagens `api`, `web` e `dbup` no ACR de hom, com o SHA do commit.
2. DbUp: roda o job do banco com a imagem nova e espera terminar (scripts já aplicados são ignorados).
3. Atualiza API e Web com a imagem do SHA (revisão nova) e espera ela ficar pronta.
4. *Smoke test*: `/health/ready` da API, `/health` e `/Conta/Login` da Web devem responder 200.
5. Se algo falhar, o tráfego volta para a revisão anterior e a nova é desativada. **O banco não volta atrás**: os
   scripts novos precisam ser compatíveis com a versão anterior do código (primeiro acrescentar, depois remover).

## Limitações conhecidas (evolução)

- A API e o DbUp usam o **mesmo login administrador** do SQL; o ideal é um usuário contido só com o papel
  `role_aurora_app` (e acesso por identidade gerenciada, como pede o docs/09 §6).
- O SQL aceita conexões de qualquer serviço do Azure (regra `0.0.0.0`); o ideal é *private endpoint*.
- Falta o alerta de **segredo prestes a expirar** (exige Event Grid no Key Vault) e o *workbook* do painel.
- Front Door e WAF (opcionais no docs/09) não foram criados.
- O `what-if` e a implantação nunca rodaram contra uma assinatura real: as primeiras execuções devem ser em hom.
