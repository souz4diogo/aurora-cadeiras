# 06 - API REST (Etapa 4: Solução Web)

**Disciplina:** Desenvolvimento Web com .NET
**Projeto:** `Aurora.Api` (ASP.NET Core Web API)
**Relacionados:** `04-arquitetura.md`, `05-banco-de-dados.md`, `07-web-admin.md`, `08-mobile.md`

> A API é o **contrato único** consumido pela web e pelo mobile. Toda regra de negócio passa
> por ela; nenhum cliente acessa o banco diretamente.

---

## 1. Padrões gerais

| Item | Definição |
|---|---|
| Base | `https://<host>/api/v1` |
| Formato | JSON, propriedades em `camelCase`, datas em ISO 8601 UTC (`2026-10-29T17:30:00Z`) |
| Documentação | OpenAPI/**Swagger** em `/swagger` (desligado em produção ou protegido) |
| Autenticação | `Authorization: Bearer <JWT>` |
| Paginação | `?page=1&pageSize=20` (máximo 100); resposta `{ "items": [], "total": 0, "page": 1, "pageSize": 20 }` |
| Ordenação e filtro | `?sort=prazoEntrega:desc`, filtros por campo (`?status=Confirmado`) |
| Erros | **ProblemDetails (RFC 7807)** com campo extra `code` |
| Idempotência | Operações vindas do mobile enviam `clientRequestId` (GUID); repetir não duplica |
| Validação | FluentValidation; erros em `400` com lista por campo |
| Versionamento | Prefixo `/v1`; mudança incompatível cria `/v2` |
| Saúde | `GET /health` (vivo) e `GET /health/ready` (inclui banco) |

### Códigos HTTP

| Código | Uso |
|---|---|
| 200 / 201 / 204 | Sucesso / criado (com `Location`) / sem conteúdo |
| 400 | Falha de validação de entrada |
| 401 | Não autenticado ou token inválido/expirado |
| 403 | Autenticado, mas sem permissão para o perfil |
| 404 | Recurso inexistente (ou fora do escopo do usuário) |
| 409 | Conflito de regra de negócio (51001 a 51005) |
| 422 | Entrada coerente mas inválida para a regra (51006, 51007) |
| 429 | Limite de requisições (login) |
| 500 | Erro inesperado (sem detalhes técnicos na resposta) |

### Formato de erro

```json
{
  "type": "https://aurora.local/erros/saldo-insuficiente",
  "title": "Saldo insuficiente de matéria-prima",
  "status": 409,
  "code": "RN04",
  "detail": "Madeira maciça: necessário 0,450 m3, disponível 0,200 m3.",
  "traceId": "00-8f3a..."
}
```

Mapeamento dos erros das procedures: 51001 → `RN04` (409), 51002 → `RN05` (409),
51003 → `RN01` (409), 51004 → `RN07` (409), 51005 → `STATUS_INVALIDO` (409),
51006 → `RN06` (422), 51007 → `RECEBIMENTO_EXCEDENTE` (422), 51008 → 404.

---

## 2. Autenticação e segurança

| Item | Definição |
|---|---|
| Token de acesso | JWT assinado (HS256 ou RS256), validade **30 min** |
| Claims | `sub` (UsuarioId), `name`, `role` (perfil), `clienteId` (se for cliente) |
| Refresh token | Opaco, aleatório, validade **7 dias**, **rotativo** (cada uso emite um novo e revoga o anterior); guardado como hash |
| Senha | Mínimo 10 caracteres; hash PBKDF2 via `PasswordHasher` |
| Bloqueio | 5 tentativas falhas bloqueiam por 15 min (`Usuario.BloqueadoAte`) |
| Transporte | Somente HTTPS; HSTS |
| Autorização | `[Authorize(Roles = "...")]` em cada rota (matriz da §3) e escopo do dado (vendedor vê os próprios pedidos; cliente vê só os seus) |
| Proteções | CORS restrito à origem da web; limite de taxa no `/auth/login`; cabeçalhos de segurança; sem `Server` header; logs sem dados pessoais nem tokens |
| Contexto no banco | Cada requisição define `sp_set_session_context 'UsuarioId'` para a auditoria |

---

## 3. Matriz de permissões

`E` = leitura e escrita, `L` = somente leitura, `-` = sem acesso.

| Módulo | Admin | Gerente Prod. | Operador | Vendedor | Almoxarife | Inspetor | Cliente |
|---|---|---|---|---|---|---|---|
| Usuários e perfis | E | - | - | - | - | - | - |
| Clientes | E | L | - | E | - | - | - |
| Fornecedores | E | L | - | - | E | - | - |
| Produtos, ficha técnica, roteiro | E | E | L | L | L | L | - |
| Matérias-primas | E | L | L | - | E | - | - |
| Pedidos de venda | E | L | - | E (próprios) | L | - | L (próprios) |
| Ordens de produção | L | E | L | L | - | L | - |
| Etapas e apontamentos | - | L | E | - | - | E (Qualidade) | - |
| Inspeção de qualidade | L | L | - | - | - | E | - |
| Estoque (saldos, movimentos, ajustes, alertas) | L | L | L (saldo) | - | E | - | - |
| Compras | L | L | - | - | E | - | - |
| Expedição | L | L | - | L | E | - | - |
| Painel e relatórios | L | L | - | L (pedidos) | L (estoque) | - | - |
| Auditoria | L | - | - | - | - | - | - |

---

## 4. Endpoints

### 4.1 Autenticação

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| POST | `/auth/login` | Anônimo | Autentica e devolve token de acesso e refresh token (RF01) |
| POST | `/auth/refresh` | Anônimo | Troca o refresh token por um novo par |
| POST | `/auth/logout` | Autenticado | Revoga o refresh token |
| GET | `/auth/me` | Autenticado | Dados do usuário logado e perfil |

### 4.2 Usuários e perfis (RF02)

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/perfis` | Admin | Lista perfis |
| GET | `/usuarios` | Admin | Lista paginada |
| GET | `/usuarios/{id}` | Admin | Detalhe |
| POST | `/usuarios` | Admin | Cria usuário (senha provisória) |
| PUT | `/usuarios/{id}` | Admin | Atualiza nome, e-mail, perfil |
| PATCH | `/usuarios/{id}/ativo` | Admin | Ativa ou inativa |
| POST | `/usuarios/{id}/redefinir-senha` | Admin | Gera nova senha provisória |

### 4.3 Cadastros

| Método | Rota | Acesso | Descrição | RF |
|---|---|---|---|---|
| GET/POST | `/clientes` | Admin, Vendedor (GET também Gerente) | Lista / cria | RF06 |
| GET/PUT | `/clientes/{id}` | idem | Detalhe / atualiza | RF06 |
| PATCH | `/clientes/{id}/ativo` | Admin, Vendedor | Ativa/inativa | RF06 |
| GET/POST | `/fornecedores` | Admin, Almoxarife (GET também Gerente) | Lista / cria | RF06 |
| GET/PUT | `/fornecedores/{id}` | idem | Detalhe / atualiza | RF06 |
| GET/POST | `/produtos` | Todos os internos (POST: Admin, Gerente) | Lista / cria | RF03 |
| GET/PUT | `/produtos/{id}` | idem | Detalhe / atualiza | RF03 |
| PATCH | `/produtos/{id}/ativo` | Admin, Gerente | Ativa/inativa | RF03 |
| GET/PUT | `/produtos/{id}/ficha-tecnica` | PUT: Admin, Gerente | Lê / **substitui** a ficha técnica | RF04 |
| GET/PUT | `/produtos/{id}/roteiro` | PUT: Admin, Gerente | Lê / substitui o roteiro | RF10 |
| GET/POST | `/materias-primas` | GET: Admin, Gerente, Operador, Almoxarife; POST: Admin, Almoxarife | Lista / cria | RF05 |
| GET/PUT | `/materias-primas/{id}` | GET: Admin, Gerente, Operador, Almoxarife; PUT: Admin, Almoxarife | Detalhe / atualiza | RF05 |
| PATCH | `/materias-primas/{id}/ativo` | Admin, Almoxarife | Ativa/inativa | RF05 |
| GET | `/estacoes` | Internos | Lista as estações | RF10 |

### 4.4 Pedidos de venda

| Método | Rota | Acesso | Descrição | RF/RN |
|---|---|---|---|---|
| GET | `/pedidos` | Admin, Gerente, Vendedor (próprios), Almoxarife | Lista com filtros (status, cliente, período, atraso) | RF08 |
| GET | `/pedidos/{id}` | idem | Detalhe com itens | RF08 |
| POST | `/pedidos` | Admin, Vendedor | Cria pedido em **Aberto** | RF07 |
| PUT | `/pedidos/{id}` | Admin, Vendedor | Edita (somente em Aberto) | RF07 |
| POST | `/pedidos/{id}/confirmar` | Admin, Vendedor | Confirma e gera OPs (`sp_ConfirmarPedido`) | RN01, RN02 |
| POST | `/pedidos/{id}/cancelar` | Admin, Vendedor | Cancela e libera reservas | - |
| GET | `/pedidos/{id}/acompanhamento` | idem + Cliente (próprio) | Linha do tempo | RF08, RF22 |
| GET | `/meus-pedidos` | Cliente | Pedidos do cliente logado | RF22 |

### 4.5 Produção

| Método | Rota | Acesso | Descrição | RF/RN |
|---|---|---|---|---|
| GET | `/ordens-producao` | Gerente, Admin, Vendedor, Inspetor | Lista (filtros: status, estação, pedido) | RF09 |
| GET | `/ordens-producao/{id}` | idem + Operador | Detalhe com etapas | RF09 |
| GET | `/ordens-producao/qr/{codigoQr}` | Operador, Inspetor, Gerente | Localiza a OP pelo QR code | RF11 |
| GET | `/etapas/minhas?estacaoId=` | Operador, Inspetor | Etapas **pendentes ou em andamento** da estação | RF11 |
| POST | `/etapas/{id}/iniciar` | Operador, Inspetor | Inicia (baixa de estoque) | RF11, RF13, RN03-RN05 |
| POST | `/etapas/{id}/finalizar` | Operador | Finaliza a etapa | RF11, RN05 |
| POST | `/etapas/{id}/perdas` | Operador | Registra refugo ou retrabalho | RF12 |
| POST | `/apontamentos/sincronizar` | Operador, Inspetor | **Lote** de apontamentos offline | RF23 |
| POST | `/ordens-producao/{id}/cancelar` | Gerente, Admin | Cancela OP não iniciada | - |

### 4.6 Qualidade

| Método | Rota | Acesso | Descrição | RF/RN |
|---|---|---|---|---|
| POST | `/ordens-producao/{id}/inspecoes` | Inspetor | Registra inspeção aprovada ou reprovada | RF18, RN06 |
| GET | `/inspecoes` | Inspetor, Gerente, Admin | Histórico de inspeções | RF18 |

### 4.7 Estoque

| Método | Rota | Acesso | Descrição | RF |
|---|---|---|---|---|
| GET | `/estoque/materias-primas` | Almoxarife, Gerente, Admin, Operador | Saldos e mínimos | RF14 |
| GET | `/estoque/produtos` | Almoxarife, Gerente, Admin | Saldo de produto acabado (total, reservado, disponível) | RF14 |
| GET | `/estoque/movimentos` | Almoxarife, Gerente, Admin | Extrato com filtros | RF14 |
| POST | `/estoque/ajustes` | Almoxarife | Ajuste com motivo (`sp_AjustarEstoque`) | RF14 |
| GET | `/estoque/alertas` | Almoxarife, Gerente | Alertas abertos | RF15 |
| POST | `/estoque/alertas/{id}/ler` | Almoxarife | Marca como lido | RF15 |

### 4.8 Compras

| Método | Rota | Acesso | Descrição | RF |
|---|---|---|---|---|
| GET/POST | `/pedidos-compra` | Almoxarife (GET também Gerente, Admin) | Lista / cria solicitação | RF16 |
| GET | `/pedidos-compra/{id}` | idem | Detalhe | RF16 |
| POST | `/pedidos-compra/{id}/emitir` | Almoxarife | Emite ao fornecedor | RF16 |
| POST | `/pedidos-compra/{id}/cancelar` | Almoxarife | Cancela | RF16 |
| POST | `/pedidos-compra/itens/{itemId}/receber` | Almoxarife | Recebe e dá entrada no estoque | RF17 |

### 4.9 Expedição

| Método | Rota | Acesso | Descrição | RF/RN |
|---|---|---|---|---|
| GET | `/expedicoes` | Almoxarife, Gerente, Admin, Vendedor | Lista | RF19 |
| POST | `/pedidos/{id}/expedir` | Almoxarife | Expede pedido **Pronto** (`sp_Expedir`) | RF19, RN07 |

### 4.10 Painel, relatórios e auditoria

| Método | Rota | Acesso | Descrição | RF |
|---|---|---|---|---|
| GET | `/painel/resumo` | Gerente, Admin | Indicadores gerais (pedidos abertos, OPs, estoque crítico) | RF20 |
| GET | `/painel/producao-por-estacao` | Gerente, Admin | OPs por estação e tempo médio | RF20 |
| GET | `/painel/pedidos-atrasados` | Gerente, Admin, Vendedor | Pedidos atrasados | RF20 |
| GET | `/relatorios/{tipo}?formato=pdf\|xlsx` | conforme matriz | `tipo`: `pedidos`, `producao`, `estoque`, `consumo`, `compras-comunitarias` | RF21 |
| GET | `/auditoria` | Admin | Histórico por tabela/registro | RNF10 |

---

## 5. Exemplos de contrato

### Login
`POST /api/v1/auth/login`
```json
{ "email": "operador@aurora.local", "senha": "********" }
```
Resposta `200`:
```json
{
  "accessToken": "eyJhbGciOi...",
  "expiresInSeconds": 1800,
  "refreshToken": "q8n3...",
  "usuario": { "id": 12, "nome": "Carlos", "perfil": "Operador" }
}
```

### Criar pedido
`POST /api/v1/pedidos`
```json
{
  "clienteId": 3,
  "prazoEntrega": "2026-12-15",
  "observacao": "Entrega parcelada",
  "itens": [
    { "produtoId": 5, "quantidade": 40 },
    { "produtoId": 2, "quantidade": 20 }
  ]
}
```
Resposta `201` com `Location: /api/v1/pedidos/87` e corpo com `numero`, `status: "Aberto"` e `valorTotal`.

### Iniciar etapa
`POST /api/v1/etapas/341/iniciar`
```json
{ "clientRequestId": "b6d1c0f2-1d1e-4a7e-9d3b-3f7e5c2a9a10", "dataHoraCliente": "2026-10-29T17:30:12Z" }
```
Resposta `200` com o novo estado da etapa. Erros possíveis: `409 RN04`, `409 RN05`, `409 STATUS_INVALIDO`.

### Sincronização offline
`POST /api/v1/apontamentos/sincronizar`
```json
{
  "itens": [
    { "clientRequestId": "…", "tipo": "Inicio", "etapaId": 341, "dataHoraCliente": "2026-10-29T17:30:12Z" },
    { "clientRequestId": "…", "tipo": "Fim",    "etapaId": 341, "dataHoraCliente": "2026-10-29T18:02:40Z" }
  ]
}
```
Resposta `200` (processa em ordem; um erro não derruba os demais):
```json
{
  "resultados": [
    { "clientRequestId": "…", "status": "Aplicado" },
    { "clientRequestId": "…", "status": "Rejeitado", "code": "RN05", "mensagem": "Etapa anterior não concluída" }
  ]
}
```
Status possíveis: `Aplicado`, `JaProcessado` (idempotência), `Rejeitado`.

### Inspeção reprovada
`POST /api/v1/ordens-producao/58/inspecoes`
```json
{ "resultado": "Reprovada", "estacaoRetrabalhoId": 3, "observacao": "Costura do encosto torta" }
```

---

## 6. Estrutura do projeto `Aurora.Api`

```
Aurora.Api/
├─ Controllers/        AuthController, PedidosController, ProducaoController, EstoqueController...
├─ Middlewares/        TratamentoDeErrosMiddleware, ContextoDeUsuarioMiddleware
├─ Filters/            ValidacaoFilter
├─ Extensions/         AddAuroraAuth, AddAuroraSwagger, AddAuroraHealth
├─ Program.cs
└─ appsettings.json    (sem segredos)
```

**Pontos de atenção na implementação**
- Controllers só chamam serviços da `Application`; nada de `DbContext` neles.
- O middleware de erros converte `RegraDeNegocioException` e erros SQL 51001-51008 em `ProblemDetails`.
- Testar com `WebApplicationFactory` cada perfil nas rotas críticas (200/401/403).
- Registrar no Swagger o esquema Bearer e exemplos das respostas de erro.
