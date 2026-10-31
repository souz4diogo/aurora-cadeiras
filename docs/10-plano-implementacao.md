# 10 - Plano de Implementação (para o Claude Code)

**Objetivo:** construir o sistema seguindo os documentos `00` a `09`, em fases curtas,
cada uma com prompt pronto, critério de pronto e **evidências** para o texto do PIM.

> **Como usar:** abra o projeto no Claude Code, copie o prompt da fase e rode. Ao terminar,
> confira o critério de pronto, salve as evidências em `docs/evidencias/` e só então avance.
> Uma fase por vez, em ramo próprio (`feat/fase-N-...`) com pull request revisado por outro
> integrante.

---

## 0. Antes de começar

1. Confirme que o `CLAUDE.md` está na **raiz** do repositório (`Pim_Final/CLAUDE.md`); o Claude Code o lê automaticamente.
2. Confirme as decisões ⚠️ de `04-arquitetura.md` §1 (versão do .NET e nuvem; o SGBD já está confirmado: SQL Server) e a tecnologia do mobile (**React Native, autorizado pelo professor**, `08-mobile.md` §1.1).
3. Instale: SDK .NET, SQL Server (Developer/Express ou Docker), Docker Desktop, Android Studio (emulador) e Node 22+ (para o chen-draw). Para o mobile, use Expo/Node (React Native).
4. Crie a pasta `docs/evidencias/`.

### Quem executa o quê

| Fase | Responsável principal | Apoio |
|---|---|---|
| 0 e 1a (esqueleto e banco) | Aluno 1 | Aluno 2 |
| 1b e 1c (API base, cadastros, pipeline) | Aluno 2 | Aluno 1 |
| 2, 3 | Aluno 2 (API/Web) | Aluno 1 |
| 4 | Aluno 3 (mobile) | Aluno 2 |
| 5 | Aluno 2 e Aluno 1 | Aluno 4 |
| 6 | Todos | Aluno 4 (indicadores) |

### Relação com o backlog (`02-backlog-agil.md`)

| Fase | Sprint(s) do backlog | Histórias |
|---|---|---|
| 0 e 1 | Sprint 1 | US01, US02, US03, US05, US24, US25 |
| 2 | Sprint 2 | US04, US06, US07, US08, US14 |
| 3 | Sprint 3 | US09, US10, US12, US13, US15 |
| 4 | Sprint 4 | US11, US18, US19, US22 |
| 5 | Sprint 5 | US16, US17, US20, US23 |
| 6 | Sprint 6 | US21, US26, US27 e estabilização |

> O backlog descreve o planejamento **de uma equipe fictícia em 12 semanas**. Aqui o grupo
> executa as mesmas histórias numa ordem equivalente, em ritmo compatível com o prazo real.

---

## Fase 0 - Esqueleto da solução

**Prompt:**
```
Leia CLAUDE.md e docs/04-arquitetura.md. Crie a estrutura da solução da seção 3:
Aurora.sln, projetos Domain, Application, Infrastructure, Api e Web, projetos de teste,
Directory.Build.props (Nullable, ImplicitUsings, TreatWarningsAsErrors), .editorconfig,
.gitignore e README raiz. Respeite as referências entre projetos da tabela da seção 3.
Não implemente funcionalidades ainda. Garanta que `dotnet build` e `dotnet test` passam.
```
**Pronto quando:** `dotnet build` e `dotnet test` passam; estrutura igual à seção 3.
**Evidência:** `E06-estrutura-solucao.png` (árvore de pastas).

---

## Fase 1 - Banco, API base e pipeline (Sprint 1)

### 1a. Banco
**Prompt:**
```
Leia docs/05-banco-de-dados.md. Gere em database/ os scripts 001-schema.sql (25 tabelas com
constraints e índices), 002-views.sql, 003-procedures.sql (7.1 a 7.9), 004-triggers.sql,
005-seguranca.sql e 006-seed.sql, seguindo exatamente nomes, tipos, CHECKs, códigos de erro
(51001-51008) e regras RN01-RN08. Crie também um projeto de testes de integração com
Testcontainers (SQL Server) cobrindo: saldo insuficiente, etapa fora de ordem, inícios
simultâneos disputando o mesmo saldo, reprovação reabrindo etapas, expedição de pedido não
pronto e imutabilidade de MovimentoEstoque. Rode os testes e corrija até passarem.
```
**Pronto quando:** scripts aplicam em banco vazio; todos os testes de integração do banco passam.
**Evidências:** `E07-*` (ver `05-banco-de-dados.md` §12). Gerar o Chen com o chen-draw apontando para o banco.

### 1b. API base e autenticação
**Prompt:**
```
Leia docs/06-api-rest.md (seções 1, 2, 3 e 4.1) e docs/04-arquitetura.md. Implemente em
Aurora.Api, Application e Infrastructure: login com JWT (30 min), refresh token rotativo
(7 dias, guardado como hash), bloqueio após 5 falhas, logout, /auth/me, CRUD de usuários e
perfis, autorização por perfil, ProblemDetails com tradução dos erros 51001-51008, Swagger
com Bearer, /health e /health/ready, e sp_set_session_context por requisição. Adicione
testes unitários e de integração (WebApplicationFactory) para 200/401/403 em cada perfil.
```
**Pronto quando:** login e renovação funcionam no Swagger; matriz de permissões validada por testes.
**Evidências:** `E04-swagger.png`, `E04-login.png`.

### 1c. Cadastros básicos e pipeline
**Prompt:**
```
Implemente os endpoints de produtos e matérias-primas (docs/06 seção 4.3) com testes.
Depois crie docker/ (Dockerfiles multi-stage e docker-compose com sql, dbup, api e web) e
.github/workflows/ci.yml conforme docs/09-nuvem-devops.md seções 2 e 3: build, testes,
verificação de vulnerabilidades e build das imagens. Garanta que `docker compose up` sobe tudo.
```
**Pronto quando:** CI verde no GitHub; `docker compose up` sobe banco, API e web.
**Evidências:** `E08-pipeline-ci.png`, `E08-docker-compose.png`.

---

## Fase 2 - Cadastros, pedidos e web (Sprint 2)

**Prompt:**
```
Leia docs/06 (4.3, 4.4, 4.7) e docs/07. Implemente: ficha técnica e roteiro do produto
(PUT substitui), clientes e fornecedores, pedidos (criar, editar em Aberto, listar, detalhe,
cancelar), movimentos de estoque (consulta) e ajuste via sp_AjustarEstoque. Na Aurora.Web,
crie o layout com menu por perfil, login, e as telas de clientes, fornecedores, produtos
(com editores de ficha técnica e roteiro), matérias-primas e pedidos, usando HttpClient
tipado e tratamento de ProblemDetails. Siga a seção 5 do docs/07 (acessibilidade).
```
**Pronto quando:** UC01, UC02 e UC03 executáveis ponta a ponta pela web.
**Evidências:** `E04-pedido-novo.png`, `E04-ficha-tecnica.png`.

---

## Fase 3 - Produção e estoque (Sprint 3)

**Prompt:**
```
Leia docs/05 (7.1 a 7.5, 8), docs/06 (4.4, 4.5, 4.7) e docs/07 (3.5, 3.6). Implemente na API:
confirmar pedido (sp_ConfirmarPedido), consulta de OPs e etapas, iniciar/finalizar etapa
(sp_IniciarEtapa/sp_FinalizarEtapa com clientRequestId), registro de perdas, alertas de
estoque mínimo. Na web: quadro de produção por estação, detalhe da OP, etiqueta com QR code
(QRCoder) e telas de saldos, movimentos e alertas. Escreva testes de integração do fluxo
pedido → OP → etapa → baixa de estoque → alerta.
```
**Pronto quando:** confirmar um pedido gera OPs; iniciar etapa baixa estoque; sem saldo retorna 409 `RN04`.
**Evidências:** `E04-quadro-producao.png`, `E07-teste-procedure-saldo-insuficiente.png`.

---

## Fase 4 - Mobile, qualidade e expedição (Sprint 4)

### 4a. API
**Prompt:**
```
Leia docs/06 (4.5, 4.6, 4.9). Implemente: GET /ordens-producao/qr/{codigoQr},
GET /etapas/minhas, POST /apontamentos/sincronizar (lote, idempotente, resultado por item),
inspeções (sp_RegistrarInspecao), expedição (sp_Expedir), /meus-pedidos e
/pedidos/{id}/acompanhamento. Com testes de integração, incluindo reenvio do mesmo
clientRequestId e rejeição parcial no lote.
```

### 4b. App
**Prompt:**
```
Leia docs/08-mobile.md inteiro. A tecnologia é React Native (Expo + TypeScript), autorizada
pelo professor; use os pacotes da coluna React Native da tabela da seção 1.2. O app deve
ter tela de cadastro e banco local SQLite (expo-sqlite).
Crie o app em mobile/aurora_mobile. Implemente login com armazenamento seguro e
interceptor de renovação do token, escolha de estação, lista de etapas, leitura de QR,
detalhe da etapa (iniciar/finalizar), fila local em SQLite, serviço de sincronização
conforme a seção 5, tela de fila de sincronização, perda, inspeção, consulta de estoque e
telas de pedidos para vendedor/cliente. Siga a organização da seção 7 e a acessibilidade
da seção 8. Adicione testes de unidade (ViewModels e sincronização com API falsa), de
componente e um teste do fluxo offline. Rode a análise estática e os testes e corrija
até passarem.
```
**Pronto quando:** roteiro offline do `docs/08` §10 passa (modo avião → iniciar → finalizar → reconectar).
**Evidências:** `E05-*` (ver `08-mobile.md` §11).

---

## Fase 5 - Compras, painel e relatórios (Sprint 5)

**Prompt:**
```
Leia docs/06 (4.8, 4.10) e docs/07 (3.6, 3.7). Implemente pedidos de compra (criar, emitir,
cancelar, receber item com sp_ReceberItemCompra), painel gerencial (resumo, produção por
estação, pedidos atrasados) usando as views, relatórios com exportação PDF e Excel
(IExportador com duas implementações) e as telas correspondentes na web. Testes incluídos.
```
**Pronto quando:** fluxo estoque crítico → compra → recebimento → saldo atualizado funciona; relatórios exportam.
**Evidências:** `E04-painel.png`.

---

## Fase 6 - Fechamento (Sprint 6)

**Prompt (auditoria e acessibilidade):**
```
Implemente a tela de auditoria (docs/06 4.10, docs/07 3.2) e revise toda a web e o app
segundo as seções de acessibilidade de docs/07 e docs/08. Rode Lighthouse/axe e corrija
até nota >= 90. Revise segurança contra o checklist de docs/06 seção 2 e docs/09 seção 6.
```
**Prompt (nuvem):**
```
Leia docs/09. Crie infra/ com os módulos Bicep da seção 7 e o workflow deploy.yml da seção 3
(OIDC, DbUp, smoke test, promoção com aprovação). Não aplique em nenhuma assinatura:
apenas valide com `az bicep build` e `what-if` se disponível.
```
**Pronto quando:** Lighthouse ≥ 90, cobertura ≥ 70%, pipeline completo descrito e validado.
**Evidências:** `E04-lighthouse.png`, `E08-*`.

---

## Evidências por etapa do PIM

| Etapa | Evidências | Origem |
|---|---|---|
| 1 e 2 | Organograma, fluxo do processo, Canvas, viabilidade | `11-negocio-e-viabilidade.md` (renderizar os diagramas) |
| 3 | Prints de acessibilidade, cadastro de fornecedor com marcador de comunidade tradicional | Fases 2 e 6 |
| 4 | Swagger, login, pedido, painel, Lighthouse | Fases 1 a 6 |
| 5 | Telas do app e do modo offline | Fase 4 |
| 6 | Estrutura da solução, diagrama de classes e de camadas, trecho de código dos padrões | Fases 0 e 3 |
| 7 | DER, Chen, planos de execução, testes de procedure, auditoria | Fase 1 e 6 |
| 8 | Pipeline, containers, Application Insights, alertas | Fases 1 e 6 |
| 9 | Backlog, burndown, Kanban (usar o histórico real de pull requests e issues como apoio) | `02-backlog-agil.md` |

## Regras de ouro ao usar o Claude Code

1. **Uma fase por vez**; não pedir "faça tudo".
2. Sempre pedir para **rodar os testes** e mostrar o resultado.
3. **Ler e entender** o que foi gerado: o professor pode perguntar em apresentação.
4. Se o prompt conflitar com um documento, **o documento vence** (ou atualize o documento primeiro).
5. Registrar mudanças de decisão em `docs/DECISOES.md`.
6. Nunca commitar segredos nem senhas.
