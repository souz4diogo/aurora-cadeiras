# 05 - Projeto do Banco de Dados (Etapa 7)

**Disciplina:** Programação de Banco de Dados
**SGBD:** ⚠️ SQL Server (Azure SQL na nuvem). Se a disciplina usar outro, adaptar a sintaxe
das procedures e triggers; o modelo de tabelas permanece.
**Relacionados:** `00-escopo.md` (RF/RN), `04-arquitetura.md`, `06-api-rest.md`

> Especificação para gerar os scripts em `database/`. Os scripts **não foram executados
> nem testados** ao escrever este documento: quem implementar deve rodar os testes de
> integração descritos em `04-arquitetura.md` §10.

---

## 1. Convenções

- Chaves primárias: `<Tabela>Id`, `int IDENTITY` (ou `bigint` em tabelas de alto volume; `tinyint` em tabelas de domínio pequenas).
- Datas e horas: `datetime2(0)` em **UTC**; datas puras: `date`.
- Quantidades de matéria-prima: `decimal(12,3)`; dinheiro: `decimal(12,2)`; custo unitário: `decimal(12,4)`.
- Textos: `varchar` (sem acentos nas colunas de código), `nvarchar` onde houver texto livre.
- Status e tipos: `varchar` com `CHECK` listando os valores válidos.
- Nomes de constraints: `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`; índices `IX_`.
- Padrão de criação: colunas `CriadoEm datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME()`.
- `SET XACT_ABORT ON` e `TRY/CATCH` em todas as procedures.
- `READ_COMMITTED_SNAPSHOT ON` no banco (leituras sem bloquear gravações).

---

## 2. Modelo lógico (DER)

```mermaid
erDiagram
    Perfil ||--o{ Usuario : possui
    Cliente |o--o{ Usuario : representa
    Usuario ||--o{ RefreshToken : emite
    Cliente ||--o{ Pedido : faz
    Usuario ||--o{ Pedido : vende
    Pedido ||--|{ PedidoItem : contem
    Produto ||--o{ PedidoItem : "e vendido em"
    LinhaProduto ||--o{ Produto : agrupa
    Produto ||--o| EstoqueProduto : "tem saldo"
    Produto ||--|{ RoteiroProduto : "passa por"
    EstacaoProducao ||--o{ RoteiroProduto : compoe
    Produto ||--o{ FichaTecnicaItem : "e composto por"
    MateriaPrima ||--o{ FichaTecnicaItem : "entra em"
    EstacaoProducao ||--o{ FichaTecnicaItem : "consumida em"
    PedidoItem ||--o{ OrdemProducao : gera
    Produto ||--o{ OrdemProducao : produz
    OrdemProducao ||--|{ OrdemProducaoEtapa : "tem etapas"
    EstacaoProducao ||--o{ OrdemProducaoEtapa : executa
    OrdemProducaoEtapa ||--o{ ApontamentoProducao : registra
    OrdemProducaoEtapa ||--o{ RegistroPerda : "tem perdas"
    OrdemProducao ||--o{ Inspecao : "e inspecionada"
    MateriaPrima ||--o{ MovimentoEstoque : movimenta
    Produto ||--o{ MovimentoEstoque : movimenta
    MateriaPrima ||--o{ AlertaEstoque : dispara
    Fornecedor |o--o{ MateriaPrima : "fornece preferencialmente"
    Fornecedor ||--o{ PedidoCompra : recebe
    PedidoCompra ||--|{ PedidoCompraItem : contem
    MateriaPrima ||--o{ PedidoCompraItem : "e comprada em"
    Pedido ||--o| Expedicao : "e expedido por"
```

**Gerar o diagrama de Chen:** depois de aplicar `001-schema.sql` num banco, use o
repositório *chen-diagram* (chen-draw) com a connection string do SQL Server
(`sqlserver://usuario:senha@localhost:1433/aurora`). O Chen é o modelo **conceitual**;
o DER acima e as tabelas da §3 formam o modelo **lógico**.

---

## 3. Tabelas

Legenda: **PK** chave primária; **FK→** chave estrangeira; **UQ** único; **NN** not null;
`CK` regra de domínio.

### 3.1 Segurança

**Perfil**
| Coluna | Tipo | Regras |
|---|---|---|
| PerfilId | tinyint | PK |
| Nome | varchar(30) | NN, UQ |

**Usuario**
| Coluna | Tipo | Regras |
|---|---|---|
| UsuarioId | int identity | PK |
| Nome | nvarchar(120) | NN |
| Email | varchar(150) | NN, UQ |
| SenhaHash | varchar(255) | NN (hash gerado pela aplicação) |
| PerfilId | tinyint | NN, FK→Perfil |
| ClienteId | int | FK→Cliente, nulo (obrigatório só para o perfil Cliente; validado na aplicação) |
| Ativo | bit | NN, default 1 |
| TentativasFalhas | tinyint | NN, default 0 |
| BloqueadoAte | datetime2(0) | nulo |
| UltimoLoginEm | datetime2(0) | nulo |
| CriadoEm | datetime2(0) | NN |

**RefreshToken**
| Coluna | Tipo | Regras |
|---|---|---|
| RefreshTokenId | bigint identity | PK |
| UsuarioId | int | NN, FK→Usuario |
| TokenHash | varchar(255) | NN, UQ |
| ExpiraEm | datetime2(0) | NN |
| RevogadoEm | datetime2(0) | nulo |
| CriadoEm | datetime2(0) | NN |

### 3.2 Cadastros

**Cliente**: ClienteId (PK), Nome nvarchar(150) NN, CpfCnpj varchar(14) NN UQ, Email varchar(150), Telefone varchar(20), Cidade nvarchar(80), Uf char(2), Ativo bit NN, CriadoEm.

**Fornecedor**: FornecedorId (PK), RazaoSocial nvarchar(150) NN, Cnpj char(14) NN UQ, Email, Telefone, **ComunidadeTradicional bit NN default 0** (apoia a Etapa 3), Ativo, CriadoEm.

**LinhaProduto**: LinhaProdutoId tinyint (PK), Nome varchar(40) NN UQ. *(Escritório, Jantar, Estofada)*

**Produto**: ProdutoId (PK), Codigo varchar(20) NN UQ, Nome nvarchar(120) NN, LinhaProdutoId FK→LinhaProduto NN, PrecoVenda decimal(12,2) NN `CK >= 0`, Ativo, CriadoEm.

**EstoqueProduto** (produto acabado): ProdutoId (PK e FK→Produto), Quantidade int NN `CK >= 0`, Reservado int NN `CK >= 0 AND <= Quantidade`. Disponível = Quantidade − Reservado.

**MateriaPrima**: MateriaPrimaId (PK), Codigo varchar(20) NN UQ, Nome nvarchar(120) NN, Unidade varchar(5) NN (`un`, `m`, `m2`, `m3`, `l`), **EstoqueAtual decimal(12,3) NN `CK >= 0`**, EstoqueMinimo decimal(12,3) NN `CK >= 0`, CustoUnitario decimal(12,4) NN, FornecedorPreferencialId FK→Fornecedor nulo, Ativo, CriadoEm.

**EstacaoProducao**: EstacaoId tinyint (PK), Nome varchar(40) NN UQ, Ordem tinyint NN UQ, **GeraEtapa bit NN** (0 para Expedição, que é tratada no nível do Pedido).

**RoteiroProduto** (quais estações cada produto percorre): ProdutoId FK, EstacaoId FK, Sequencia tinyint NN. PK (ProdutoId, EstacaoId); UQ (ProdutoId, Sequencia).

**FichaTecnicaItem** (BOM): FichaTecnicaItemId (PK), ProdutoId FK NN, MateriaPrimaId FK NN, EstacaoId FK NN (estação em que o material é consumido), Quantidade decimal(12,3) NN `CK > 0`. UQ (ProdutoId, MateriaPrimaId, EstacaoId). A estação deve existir no roteiro do produto (validada na aplicação e na procedure de cadastro).

### 3.3 Vendas

**Pedido**: PedidoId (PK), Numero varchar(15) NN UQ (`PV-2026-00001`), ClienteId FK NN, VendedorId FK→Usuario NN, DataPedido datetime2(0) NN, PrazoEntrega date NN, Status varchar(15) NN `CK IN ('Aberto','Confirmado','EmProducao','Pronto','Expedido','Cancelado')`, Observacao nvarchar(300), ValorTotal decimal(14,2) NN.

**PedidoItem**: PedidoItemId (PK), PedidoId FK NN, ProdutoId FK NN, Quantidade int NN `CK > 0`, PrecoUnitario decimal(12,2) NN. UQ (PedidoId, ProdutoId).

### 3.4 Produção

**OrdemProducao**: OrdemProducaoId (PK), Numero varchar(15) NN UQ (`OP-2026-00001`), PedidoItemId FK NN, ProdutoId FK NN, Quantidade int NN `CK > 0`, Status varchar(12) NN `CK IN ('Planejada','EmAndamento','Concluida','Cancelada')`, **CodigoQr uniqueidentifier NN UQ default NEWID()**, PrevisaoConclusao date, CriadaEm, ConcluidaEm nulo.

**OrdemProducaoEtapa**: OrdemProducaoEtapaId (PK), OrdemProducaoId FK NN, EstacaoId FK NN, Sequencia tinyint NN, Status varchar(12) NN `CK IN ('Pendente','EmAndamento','Concluida')`, IniciadaEm nulo, ConcluidaEm nulo, OperadorId FK→Usuario nulo. UQ (OrdemProducaoId, Sequencia).

**ApontamentoProducao** (histórico imutável; garante idempotência da sincronização offline): ApontamentoId bigint (PK), OrdemProducaoEtapaId FK NN, Tipo varchar(6) NN `CK IN ('Inicio','Fim')`, UsuarioId FK NN, DataHoraCliente datetime2(0) NN, DataHoraServidor datetime2(0) NN default SYSUTCDATETIME(), **ClientRequestId uniqueidentifier NN UQ**.

**RegistroPerda**: RegistroPerdaId (PK), OrdemProducaoEtapaId FK NN, Tipo varchar(10) NN `CK IN ('Refugo','Retrabalho')`, Quantidade int NN `CK > 0`, Motivo nvarchar(200) NN, UsuarioId FK NN, CriadoEm.

### 3.5 Qualidade e Expedição

**Inspecao**: InspecaoId (PK), OrdemProducaoId FK NN, InspetorId FK→Usuario NN, Resultado varchar(10) NN `CK IN ('Aprovada','Reprovada')`, EstacaoRetrabalhoId tinyint FK→EstacaoProducao nulo (`CK`: obrigatório se Reprovada), Observacao nvarchar(300), DataHora NN.

**Expedicao**: ExpedicaoId (PK), PedidoId FK NN **UQ**, UsuarioId FK NN, Transportadora nvarchar(80), CodigoRastreio varchar(40), DataHora NN.

### 3.6 Estoque

**MovimentoEstoque** (livro-razão **imutável**): MovimentoEstoqueId bigint (PK), MateriaPrimaId FK nulo, ProdutoId FK nulo, Tipo varchar(8) NN `CK IN ('Entrada','Saida','Ajuste')`, **Quantidade decimal(12,3) NN com sinal** (Entrada > 0, Saída < 0, Ajuste ≠ 0), Origem varchar(20) NN `CK IN ('Compra','Producao','ProducaoConcluida','Expedicao','Ajuste')`, OrigemId int nulo, UsuarioId FK nulo, Motivo nvarchar(200) nulo, DataHora NN.
`CK`: exatamente um entre MateriaPrimaId e ProdutoId preenchido.

**AlertaEstoque**: AlertaEstoqueId (PK), MateriaPrimaId FK NN, CriadoEm NN, LidoEm nulo, LidoPorId FK→Usuario nulo. **Índice único filtrado** em (MateriaPrimaId) onde LidoEm IS NULL (um alerta aberto por material).

### 3.7 Compras

**PedidoCompra**: PedidoCompraId (PK), Numero varchar(15) NN UQ (`PC-2026-00001`), FornecedorId FK NN, CompradorId FK→Usuario NN, Status varchar(22) NN `CK IN ('Solicitado','Emitido','ParcialmenteRecebido','Recebido','Cancelado')`, DataPrevista date, CriadoEm.

**PedidoCompraItem**: PedidoCompraItemId (PK), PedidoCompraId FK NN, MateriaPrimaId FK NN, Quantidade decimal(12,3) NN `CK > 0`, CustoUnitario decimal(12,4) NN, QuantidadeRecebida decimal(12,3) NN default 0 `CK BETWEEN 0 AND Quantidade`. UQ (PedidoCompraId, MateriaPrimaId).

### 3.8 Auditoria

**Auditoria**: AuditoriaId bigint (PK), Tabela sysname NN, Registro varchar(50) NN, Acao char(1) NN `CK IN ('I','U','D')`, UsuarioId int nulo (lido de `SESSION_CONTEXT(N'UsuarioId')`), DataHora NN, Antes nvarchar(max) nulo (JSON), Depois nvarchar(max) nulo (JSON).

> A API define `EXEC sp_set_session_context 'UsuarioId', @id` em cada conexão para que
> triggers e procedures saibam quem fez a ação.

**Total: 25 tabelas.**

---

## 4. Índices (otimização)

| Índice | Tabela (colunas) | Para quê |
|---|---|---|
| IX_Pedido_Cliente_Status | Pedido (ClienteId, Status) INCLUDE (PrazoEntrega, ValorTotal) | Lista do cliente e filtros |
| IX_Pedido_Prazo | Pedido (PrazoEntrega) WHERE Status IN ('Confirmado','EmProducao') | Pedidos atrasados |
| IX_PedidoItem_Pedido | PedidoItem (PedidoId) INCLUDE (ProdutoId, Quantidade) | Itens do pedido |
| IX_OP_PedidoItem | OrdemProducao (PedidoItemId) | Vínculo pedido → OP |
| IX_OP_Status | OrdemProducao (Status) INCLUDE (PrevisaoConclusao) | Painel de produção |
| IX_OPE_Estacao_Status | OrdemProducaoEtapa (EstacaoId, Status) INCLUDE (OrdemProducaoId, Sequencia) | **Tela do operador** e painel por estação |
| IX_Mov_MP_Data | MovimentoEstoque (MateriaPrimaId, DataHora DESC) | Extrato de estoque |
| IX_Mov_Prod_Data | MovimentoEstoque (ProdutoId, DataHora DESC) | Extrato de produto acabado |
| IX_MP_Critico | MateriaPrima (EstoqueAtual) INCLUDE (EstoqueMinimo) WHERE Ativo = 1 | Estoque crítico |
| UQ_Alerta_Aberto | AlertaEstoque (MateriaPrimaId) WHERE LidoEm IS NULL | Um alerta aberto por material |
| IX_Ficha_Produto | FichaTecnicaItem (ProdutoId, EstacaoId) INCLUDE (MateriaPrimaId, Quantidade) | Consumo ao iniciar etapa |
| IX_Auditoria_Tabela_Reg | Auditoria (Tabela, Registro, DataHora DESC) | Consulta de histórico |
| IX_Usuario_Email | (já coberto pelo UQ) | Login |

**Outras estratégias:** paginação com `OFFSET/FETCH`; nunca `SELECT *`; atualizar estatísticas;
ativar **Query Store** para achar consultas lentas; arquivar `Auditoria` e `MovimentoEstoque`
com mais de 2 anos; compressão de página em tabelas grandes.

> **Evidência para o PIM:** capturar o **plano de execução antes e depois** do
> `IX_OPE_Estacao_Status` na consulta da tela do operador e salvar em
> `docs/evidencias/E07-plano-execucao-antes.png` e `...-depois.png`.

---

## 5. Views

| View | Conteúdo | Usada por |
|---|---|---|
| `vw_EstoqueCritico` | Matérias-primas com `EstoqueAtual <= EstoqueMinimo` e dados do fornecedor preferencial | RF15, painel |
| `vw_PainelProducao` | OPs em andamento por estação, tempo na etapa, atraso vs previsão | RF20 |
| `vw_PedidosAtrasados` | Pedidos com prazo vencido e não expedidos | RF20 |
| `vw_AcompanhamentoPedido` | Linha do tempo do pedido (confirmado, em produção por etapa, pronto, expedido) | RF08, RF22 |
| `vw_ConsumoMateriaPrima` | Consumo por período e por produto | Relatórios |
| `vw_ComprasComunidadesTradicionais` | Compras recebidas de fornecedores com `ComunidadeTradicional = 1`, por período, valor e percentual do total | Relatório `compras-comunitarias`, Etapa 3 |

---

## 6. Códigos de erro das procedures

Procedures usam `THROW` com estes números; a API traduz para HTTP.

| Código | Regra | Mensagem base | HTTP |
|---|---|---|---|
| 51001 | RN04 | Saldo insuficiente de matéria-prima | 409 |
| 51002 | RN05 | Etapa anterior não concluída | 409 |
| 51003 | RN01 | Pedido não está confirmado | 409 |
| 51004 | RN07 | Pedido não está pronto para expedição | 409 |
| 51005 | - | Status atual não permite a operação | 409 |
| 51006 | RN06 | Inspeção inválida (reprovação sem estação de retrabalho) | 422 |
| 51007 | - | Recebimento acima da quantidade comprada | 422 |
| 51008 | - | Registro não encontrado | 404 |

---

## 7. Procedures

Todas: `SET XACT_ABORT ON`, `BEGIN TRAN` ... `COMMIT`, `TRY/CATCH` com `ROLLBACK` e
`THROW`, leitura do usuário via `SESSION_CONTEXT`.

### 7.1 `sp_ConfirmarPedido (@PedidoId int)`
- Exige `Status = 'Aberto'` (senão 51005) e ao menos um item.
- Altera para `Confirmado` e executa `sp_GerarOrdensProducao`.

### 7.2 `sp_GerarOrdensProducao (@PedidoId int)` (RN01, RN02)
Para cada item do pedido:
1. `disponivel = EstoqueProduto.Quantidade - Reservado` (0 se não existir linha).
2. `reservar = MIN(item.Quantidade, disponivel)`; incrementa `Reservado`.
3. `produzir = item.Quantidade - reservar`; se > 0, cria `OrdemProducao` e uma
   `OrdemProducaoEtapa` por linha do `RoteiroProduto` cujo `EstacaoProducao.GeraEtapa = 1`.
4. Status do pedido: `EmProducao` se criou alguma OP; senão `Pronto`.

### 7.3 `sp_IniciarEtapa (@OrdemProducaoEtapaId int, @ClientRequestId uniqueidentifier, @DataHoraCliente datetime2(0))` (RN03, RN04, RN05)
1. **Idempotência:** se `ClientRequestId` já existe em `ApontamentoProducao`, retorna sem erro.
2. Etapa deve estar `Pendente` (senão 51005) e a etapa de `Sequencia` anterior `Concluida` (senão 51002).
3. Consumo = `FichaTecnicaItem` do produto **na estação da etapa** × `OrdemProducao.Quantidade`.
4. Para cada material: lê o saldo com `UPDLOCK, ROWLOCK`; se `EstoqueAtual < consumo`, `THROW 51001`.
5. Insere `MovimentoEstoque` tipo `Saida`, origem `Producao`, quantidade negativa (o trigger baixa o saldo).
6. Etapa → `EmAndamento`, grava `OperadorId` e `IniciadaEm`; OP → `EmAndamento`.
7. Insere `ApontamentoProducao` tipo `Inicio`.

### 7.4 `sp_FinalizarEtapa (@OrdemProducaoEtapaId int, @ClientRequestId uniqueidentifier, @DataHoraCliente datetime2(0))`
- Idempotente pelo `ClientRequestId`.
- Exige `EmAndamento`; etapa → `Concluida`; insere apontamento `Fim`.
- A etapa da estação **Qualidade** não é finalizada aqui: só por `sp_RegistrarInspecao`.

### 7.5 `sp_RegistrarInspecao (@OrdemProducaoId int, @Resultado varchar(10), @EstacaoRetrabalhoId tinyint = NULL, @Observacao nvarchar(300) = NULL)` (RN06)
- Exige etapa da Qualidade `EmAndamento` e anteriores `Concluida`.
- **Aprovada:** conclui a etapa; OP → `Concluida`; entrada de produto acabado
  (`MovimentoEstoque` origem `ProducaoConcluida`, trigger soma em `EstoqueProduto`);
  se todos os itens do pedido estão atendidos, pedido → `Pronto`.
- **Reprovada:** exige `@EstacaoRetrabalhoId` (senão 51006); etapas daquela estação até a
  Qualidade voltam a `Pendente`; registra `RegistroPerda` tipo `Retrabalho`; OP → `EmAndamento`.
- Sempre grava a linha em `Inspecao`.

### 7.6 `sp_ReceberItemCompra (@PedidoCompraItemId int, @Quantidade decimal(12,3))` (RF17)
- Valida `QuantidadeRecebida + @Quantidade <= Quantidade` (senão 51007).
- Atualiza o item, insere `MovimentoEstoque` `Entrada` origem `Compra`, atualiza o último
  `CustoUnitario` da matéria-prima e o status do pedido de compra (`ParcialmenteRecebido` ou `Recebido`).

### 7.7 `sp_AjustarEstoque (@MateriaPrimaId int, @NovaQuantidade decimal(12,3), @Motivo nvarchar(200))`
- Calcula a diferença, insere `MovimentoEstoque` tipo `Ajuste`; motivo obrigatório.

### 7.8 `sp_Expedir (@PedidoId int, @Transportadora nvarchar(80), @CodigoRastreio varchar(40))` (RN07)
- Exige `Status = 'Pronto'` (senão 51004).
- Para cada item: **primeiro** reduz `EstoqueProduto.Reservado` e **depois** insere o `MovimentoEstoque` `Saida` origem `Expedicao` (o trigger reduz `Quantidade`). Essa ordem evita violar o `CHECK Reservado <= Quantidade`.
- Insere `Expedicao` e altera o pedido para `Expedido`.

### 7.9 `sp_CancelarPedido (@PedidoId int)`
- Permitido em `Aberto`, `Confirmado` ou `EmProducao` (não em `Pronto`/`Expedido`).
- Libera `Reservado`, cancela OPs não concluídas e marca o pedido como `Cancelado`.
- Matéria-prima já consumida por etapas iniciadas **não volta** ao estoque; se houver sobra aproveitável, o almoxarife registra um ajuste.

> **CRUD simples** (cadastros, pedidos em rascunho, registro de perdas) usa **EF Core**,
> sem procedure.

---

## 8. Triggers

| Trigger | Tabela / evento | Função |
|---|---|---|
| `trg_MovimentoEstoque_AI` | MovimentoEstoque, AFTER INSERT | Aplica `Quantidade` (com sinal) em `MateriaPrima.EstoqueAtual` ou `EstoqueProduto.Quantidade`, **set-based** (funciona com várias linhas); cria a linha de `EstoqueProduto` se ainda não existir (`MERGE`) |
| `trg_MovimentoEstoque_Imutavel` | MovimentoEstoque, INSTEAD OF UPDATE, DELETE | `THROW`: livro-razão não se altera; correções são novos movimentos de ajuste |
| `trg_MateriaPrima_AU_Alerta` | MateriaPrima, AFTER UPDATE | Se `EstoqueAtual <= EstoqueMinimo`, insere `AlertaEstoque` (se não houver alerta aberto) |
| `trg_Auditoria_Pedido` | Pedido, AFTER INSERT/UPDATE/DELETE | Grava antes/depois em JSON (`FOR JSON AUTO`) |
| `trg_Auditoria_OrdemProducao` | OrdemProducao, idem | Idem |
| `trg_Auditoria_MateriaPrima` | MateriaPrima, idem | Idem |
| `trg_Auditoria_Usuario` | Usuario, idem | Idem (nunca grava `SenhaHash`) |

---

## 9. Segurança do banco

| Mecanismo | Aplicação |
|---|---|
| **Mínimo privilégio** | Login `aurora_app` com permissão de `SELECT/INSERT/UPDATE` nas tabelas de cadastro, pedidos, compras e perdas, e `EXECUTE` nas procedures |
| **Acesso só por procedure** | `DENY INSERT, UPDATE, DELETE` em `MovimentoEstoque`, `EstoqueProduto`, `OrdemProducaoEtapa` e `ApontamentoProducao` para `aurora_app`; validar com testes que procedures e triggers seguem funcionando (cadeia de propriedade) |
| **Usuário de relatórios** | `aurora_readonly`: apenas `SELECT` nas views |
| **Dados pessoais (LGPD)** | **Dynamic Data Masking** em `Cliente.CpfCnpj`, `Email` e `Telefone` para `aurora_readonly`; coleta mínima |
| **Senhas** | Nunca no banco em texto; hash (PBKDF2/ASP.NET Identity) gerado pela aplicação |
| **SQL Injection** | Consultas parametrizadas (EF Core e `SqlParameter`); nada de SQL por concatenação |
| **Criptografia** | TDE (padrão no Azure SQL) e TLS na conexão (`Encrypt=True`) |
| **Segredos** | Connection string no Key Vault; nunca no repositório |
| **Auditoria** | Triggers de auditoria + log de login falho |
| **Backup** | Azure SQL: restauração a um ponto no tempo (PITR) de 7 a 35 dias; metas RPO 15 min, RTO 4 h |
| **Integridade** | `CHECK`, `UNIQUE`, FK em todas as relações; `READ_COMMITTED_SNAPSHOT` e bloqueios explícitos nas procedures de estoque |

---

## 10. Dados iniciais (`006-seed.sql`)

Sem senhas no repositório: os usuários de demonstração são criados pela aplicação
(*seeder*) lendo a senha de uma variável de ambiente.

| Tabela | Conteúdo |
|---|---|
| Perfil | Administrador, GerenteProducao, Operador, Vendedor, Almoxarife, Inspetor, Cliente |
| EstacaoProducao | 1 Corte, 2 Montagem da Estrutura, 3 Estofaria, 4 Montagem Final, 5 Qualidade, 6 Expedição (`GeraEtapa = 0`) |
| LinhaProduto | Escritório, Jantar, Estofada |
| Produto (6) | CE-100 Ergonômica Presidente; CE-200 Operacional Giratória; CJ-100 Jantar Ipê; CJ-200 Jantar Cumaru; CS-100 Estofada Linho; CS-200 Estofada Veludo |
| Roteiros | **Escritório:** Corte → Estrutura → Estofaria → Montagem Final → Qualidade. **Jantar:** Corte → Estrutura → Montagem Final → Qualidade (sem Estofaria). **Estofada:** as 5 estações |
| MateriaPrima (cerca de 12) | Madeira maciça (m3), Compensado (m2), Tubo metálico (m), Espuma D33 (m3), Tecido linho (m), Tecido veludo (m), Palha natural (m2), Parafusos (un), Cola (l), Verniz (l), Rodízio (un), Pistão a gás (un), Assento plástico ABS (un), Embalagem (un) |
| FichaTecnicaItem | Uma ficha por produto, vinculando materiais às estações em que são consumidos |
| Fornecedor | 4 a 5 exemplos, ao menos 2 com `ComunidadeTradicional = 1` |
| Cliente, Pedidos | Poucos exemplos para demonstração |

---

## 11. Ordem dos scripts

1. `001-schema.sql` - tabelas, constraints, índices
2. `002-views.sql`
3. `003-procedures.sql`
4. `004-triggers.sql`
5. `005-seguranca.sql`
6. `006-seed.sql`
7. `007-idempotencia-concorrente.sql` - corrige o reenvio simultâneo do mesmo `ClientRequestId` em `sp_IniciarEtapa` e `sp_FinalizarEtapa`

O DbUp executa na ordem e registra o que já foi aplicado. Scripts já aplicados não mudam: correções entram em
um novo script numerado.

---

## 12. Evidências para o texto da Etapa 7

Salvar em `docs/evidencias/`:
- `E07-der-logico.png` (renderizado do Mermaid da §2)
- `E07-diagrama-chen.png` (gerado pelo chen-draw)
- `E07-plano-execucao-antes.png` e `E07-plano-execucao-depois.png`
- `E07-teste-procedure-saldo-insuficiente.png` (erro 51001 em ação)
- `E07-auditoria-exemplo.png` (linha gerada por trigger)
