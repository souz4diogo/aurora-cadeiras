-- 001-schema.sql
-- Aurora Cadeiras: 25 tabelas, constraints e índices (docs/05-banco-de-dados.md §3 e §4).
-- Convenções: datas em UTC (datetime2(0)), PK_/FK_/UQ_/CK_/DF_/IX_.

ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

-- Numeração legível dos documentos (PV-2026-00001, OP-2026-00001, PC-2026-00001)
CREATE SEQUENCE dbo.seq_NumeroPedido AS bigint START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE dbo.seq_NumeroOrdemProducao AS bigint START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE dbo.seq_NumeroPedidoCompra AS bigint START WITH 1 INCREMENT BY 1;
GO

-- 3.1 Segurança -------------------------------------------------------------

CREATE TABLE dbo.Perfil (
    PerfilId tinyint     NOT NULL,
    Nome     varchar(30) NOT NULL,
    CONSTRAINT PK_Perfil PRIMARY KEY (PerfilId),
    CONSTRAINT UQ_Perfil_Nome UNIQUE (Nome)
);

-- 3.2 Cadastros (Cliente vem antes de Usuario por causa da FK) ---------------

CREATE TABLE dbo.Cliente (
    ClienteId int IDENTITY(1,1) NOT NULL,
    Nome      nvarchar(150) NOT NULL,
    CpfCnpj   varchar(14)   NOT NULL,
    Email     varchar(150)  NULL,
    Telefone  varchar(20)   NULL,
    Cidade    nvarchar(80)  NULL,
    Uf        char(2)       NULL,
    Ativo     bit           NOT NULL CONSTRAINT DF_Cliente_Ativo DEFAULT (1),
    CriadoEm  datetime2(0)  NOT NULL CONSTRAINT DF_Cliente_CriadoEm DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Cliente PRIMARY KEY (ClienteId),
    CONSTRAINT UQ_Cliente_CpfCnpj UNIQUE (CpfCnpj)
);

CREATE TABLE dbo.Usuario (
    UsuarioId        int IDENTITY(1,1) NOT NULL,
    Nome             nvarchar(120) NOT NULL,
    Email            varchar(150)  NOT NULL,
    SenhaHash        varchar(255)  NOT NULL,
    PerfilId         tinyint       NOT NULL,
    ClienteId        int           NULL,
    Ativo            bit           NOT NULL CONSTRAINT DF_Usuario_Ativo DEFAULT (1),
    TentativasFalhas tinyint       NOT NULL CONSTRAINT DF_Usuario_TentativasFalhas DEFAULT (0),
    BloqueadoAte     datetime2(0)  NULL,
    UltimoLoginEm    datetime2(0)  NULL,
    CriadoEm         datetime2(0)  NOT NULL CONSTRAINT DF_Usuario_CriadoEm DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Usuario PRIMARY KEY (UsuarioId),
    CONSTRAINT UQ_Usuario_Email UNIQUE (Email),
    CONSTRAINT FK_Usuario_Perfil FOREIGN KEY (PerfilId) REFERENCES dbo.Perfil (PerfilId),
    CONSTRAINT FK_Usuario_Cliente FOREIGN KEY (ClienteId) REFERENCES dbo.Cliente (ClienteId)
);

CREATE TABLE dbo.RefreshToken (
    RefreshTokenId bigint IDENTITY(1,1) NOT NULL,
    UsuarioId      int          NOT NULL,
    TokenHash      varchar(255) NOT NULL,
    ExpiraEm       datetime2(0) NOT NULL,
    RevogadoEm     datetime2(0) NULL,
    CriadoEm       datetime2(0) NOT NULL CONSTRAINT DF_RefreshToken_CriadoEm DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_RefreshToken PRIMARY KEY (RefreshTokenId),
    CONSTRAINT UQ_RefreshToken_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_RefreshToken_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (UsuarioId)
);

CREATE TABLE dbo.Fornecedor (
    FornecedorId          int IDENTITY(1,1) NOT NULL,
    RazaoSocial           nvarchar(150) NOT NULL,
    Cnpj                  char(14)      NOT NULL,
    Email                 varchar(150)  NULL,
    Telefone              varchar(20)   NULL,
    ComunidadeTradicional bit           NOT NULL CONSTRAINT DF_Fornecedor_ComunidadeTradicional DEFAULT (0),
    Ativo                 bit           NOT NULL CONSTRAINT DF_Fornecedor_Ativo DEFAULT (1),
    CriadoEm              datetime2(0)  NOT NULL CONSTRAINT DF_Fornecedor_CriadoEm DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Fornecedor PRIMARY KEY (FornecedorId),
    CONSTRAINT UQ_Fornecedor_Cnpj UNIQUE (Cnpj)
);

CREATE TABLE dbo.LinhaProduto (
    LinhaProdutoId tinyint     NOT NULL,
    Nome           varchar(40) NOT NULL,
    CONSTRAINT PK_LinhaProduto PRIMARY KEY (LinhaProdutoId),
    CONSTRAINT UQ_LinhaProduto_Nome UNIQUE (Nome)
);

CREATE TABLE dbo.Produto (
    ProdutoId      int IDENTITY(1,1) NOT NULL,
    Codigo         varchar(20)   NOT NULL,
    Nome           nvarchar(120) NOT NULL,
    LinhaProdutoId tinyint       NOT NULL,
    PrecoVenda     decimal(12,2) NOT NULL,
    Ativo          bit           NOT NULL CONSTRAINT DF_Produto_Ativo DEFAULT (1),
    CriadoEm       datetime2(0)  NOT NULL CONSTRAINT DF_Produto_CriadoEm DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Produto PRIMARY KEY (ProdutoId),
    CONSTRAINT UQ_Produto_Codigo UNIQUE (Codigo),
    CONSTRAINT FK_Produto_Linha FOREIGN KEY (LinhaProdutoId) REFERENCES dbo.LinhaProduto (LinhaProdutoId),
    CONSTRAINT CK_Produto_PrecoVenda CHECK (PrecoVenda >= 0)
);

CREATE TABLE dbo.EstoqueProduto (
    ProdutoId  int NOT NULL,
    Quantidade int NOT NULL,
    Reservado  int NOT NULL,
    CONSTRAINT PK_EstoqueProduto PRIMARY KEY (ProdutoId),
    CONSTRAINT FK_EstoqueProduto_Produto FOREIGN KEY (ProdutoId) REFERENCES dbo.Produto (ProdutoId),
    CONSTRAINT CK_EstoqueProduto_Quantidade CHECK (Quantidade >= 0),
    CONSTRAINT CK_EstoqueProduto_Reservado CHECK (Reservado >= 0 AND Reservado <= Quantidade)
);

CREATE TABLE dbo.MateriaPrima (
    MateriaPrimaId           int IDENTITY(1,1) NOT NULL,
    Codigo                   varchar(20)    NOT NULL,
    Nome                     nvarchar(120)  NOT NULL,
    Unidade                  varchar(5)     NOT NULL,
    EstoqueAtual             decimal(12,3)  NOT NULL CONSTRAINT DF_MateriaPrima_EstoqueAtual DEFAULT (0),
    EstoqueMinimo            decimal(12,3)  NOT NULL CONSTRAINT DF_MateriaPrima_EstoqueMinimo DEFAULT (0),
    CustoUnitario            decimal(12,4)  NOT NULL CONSTRAINT DF_MateriaPrima_CustoUnitario DEFAULT (0),
    FornecedorPreferencialId int            NULL,
    Ativo                    bit            NOT NULL CONSTRAINT DF_MateriaPrima_Ativo DEFAULT (1),
    CriadoEm                 datetime2(0)   NOT NULL CONSTRAINT DF_MateriaPrima_CriadoEm DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_MateriaPrima PRIMARY KEY (MateriaPrimaId),
    CONSTRAINT UQ_MateriaPrima_Codigo UNIQUE (Codigo),
    CONSTRAINT FK_MateriaPrima_Fornecedor FOREIGN KEY (FornecedorPreferencialId) REFERENCES dbo.Fornecedor (FornecedorId),
    CONSTRAINT CK_MateriaPrima_Unidade CHECK (Unidade IN ('un', 'm', 'm2', 'm3', 'l')),
    CONSTRAINT CK_MateriaPrima_EstoqueAtual CHECK (EstoqueAtual >= 0),
    CONSTRAINT CK_MateriaPrima_EstoqueMinimo CHECK (EstoqueMinimo >= 0)
);

CREATE TABLE dbo.EstacaoProducao (
    EstacaoId tinyint     NOT NULL,
    Nome      varchar(40) NOT NULL,
    Ordem     tinyint     NOT NULL,
    GeraEtapa bit         NOT NULL,
    CONSTRAINT PK_EstacaoProducao PRIMARY KEY (EstacaoId),
    CONSTRAINT UQ_EstacaoProducao_Nome UNIQUE (Nome),
    CONSTRAINT UQ_EstacaoProducao_Ordem UNIQUE (Ordem)
);

CREATE TABLE dbo.RoteiroProduto (
    ProdutoId int     NOT NULL,
    EstacaoId tinyint NOT NULL,
    Sequencia tinyint NOT NULL,
    CONSTRAINT PK_RoteiroProduto PRIMARY KEY (ProdutoId, EstacaoId),
    CONSTRAINT UQ_RoteiroProduto_Sequencia UNIQUE (ProdutoId, Sequencia),
    CONSTRAINT FK_RoteiroProduto_Produto FOREIGN KEY (ProdutoId) REFERENCES dbo.Produto (ProdutoId),
    CONSTRAINT FK_RoteiroProduto_Estacao FOREIGN KEY (EstacaoId) REFERENCES dbo.EstacaoProducao (EstacaoId)
);

CREATE TABLE dbo.FichaTecnicaItem (
    FichaTecnicaItemId int IDENTITY(1,1) NOT NULL,
    ProdutoId          int           NOT NULL,
    MateriaPrimaId     int           NOT NULL,
    EstacaoId          tinyint       NOT NULL,
    Quantidade         decimal(12,3) NOT NULL,
    CONSTRAINT PK_FichaTecnicaItem PRIMARY KEY (FichaTecnicaItemId),
    CONSTRAINT UQ_FichaTecnicaItem UNIQUE (ProdutoId, MateriaPrimaId, EstacaoId),
    CONSTRAINT FK_FichaTecnicaItem_Produto FOREIGN KEY (ProdutoId) REFERENCES dbo.Produto (ProdutoId),
    CONSTRAINT FK_FichaTecnicaItem_MateriaPrima FOREIGN KEY (MateriaPrimaId) REFERENCES dbo.MateriaPrima (MateriaPrimaId),
    CONSTRAINT FK_FichaTecnicaItem_Estacao FOREIGN KEY (EstacaoId) REFERENCES dbo.EstacaoProducao (EstacaoId),
    CONSTRAINT CK_FichaTecnicaItem_Quantidade CHECK (Quantidade > 0)
);

-- 3.3 Vendas ----------------------------------------------------------------

CREATE TABLE dbo.Pedido (
    PedidoId    int IDENTITY(1,1) NOT NULL,
    Numero      varchar(15)   NOT NULL
        CONSTRAINT DF_Pedido_Numero DEFAULT (
            'PV-' + CAST(YEAR(SYSUTCDATETIME()) AS char(4)) + '-'
            + RIGHT('00000' + CAST(NEXT VALUE FOR dbo.seq_NumeroPedido AS varchar(10)), 5)),
    ClienteId   int           NOT NULL,
    VendedorId  int           NOT NULL,
    DataPedido  datetime2(0)  NOT NULL CONSTRAINT DF_Pedido_DataPedido DEFAULT (SYSUTCDATETIME()),
    PrazoEntrega date         NOT NULL,
    Status      varchar(15)   NOT NULL CONSTRAINT DF_Pedido_Status DEFAULT ('Aberto'),
    Observacao  nvarchar(300) NULL,
    ValorTotal  decimal(14,2) NOT NULL CONSTRAINT DF_Pedido_ValorTotal DEFAULT (0),
    CONSTRAINT PK_Pedido PRIMARY KEY (PedidoId),
    CONSTRAINT UQ_Pedido_Numero UNIQUE (Numero),
    CONSTRAINT FK_Pedido_Cliente FOREIGN KEY (ClienteId) REFERENCES dbo.Cliente (ClienteId),
    CONSTRAINT FK_Pedido_Vendedor FOREIGN KEY (VendedorId) REFERENCES dbo.Usuario (UsuarioId),
    CONSTRAINT CK_Pedido_Status CHECK (Status IN ('Aberto', 'Confirmado', 'EmProducao', 'Pronto', 'Expedido', 'Cancelado')),
    CONSTRAINT CK_Pedido_ValorTotal CHECK (ValorTotal >= 0)
);

CREATE TABLE dbo.PedidoItem (
    PedidoItemId  int IDENTITY(1,1) NOT NULL,
    PedidoId      int           NOT NULL,
    ProdutoId     int           NOT NULL,
    Quantidade    int           NOT NULL,
    PrecoUnitario decimal(12,2) NOT NULL,
    CONSTRAINT PK_PedidoItem PRIMARY KEY (PedidoItemId),
    CONSTRAINT UQ_PedidoItem_Pedido_Produto UNIQUE (PedidoId, ProdutoId),
    CONSTRAINT FK_PedidoItem_Pedido FOREIGN KEY (PedidoId) REFERENCES dbo.Pedido (PedidoId),
    CONSTRAINT FK_PedidoItem_Produto FOREIGN KEY (ProdutoId) REFERENCES dbo.Produto (ProdutoId),
    CONSTRAINT CK_PedidoItem_Quantidade CHECK (Quantidade > 0),
    CONSTRAINT CK_PedidoItem_PrecoUnitario CHECK (PrecoUnitario >= 0)
);

-- 3.4 Produção --------------------------------------------------------------

CREATE TABLE dbo.OrdemProducao (
    OrdemProducaoId   int IDENTITY(1,1) NOT NULL,
    Numero            varchar(15)      NOT NULL
        CONSTRAINT DF_OrdemProducao_Numero DEFAULT (
            'OP-' + CAST(YEAR(SYSUTCDATETIME()) AS char(4)) + '-'
            + RIGHT('00000' + CAST(NEXT VALUE FOR dbo.seq_NumeroOrdemProducao AS varchar(10)), 5)),
    PedidoItemId      int              NOT NULL,
    ProdutoId         int              NOT NULL,
    Quantidade        int              NOT NULL,
    Status            varchar(12)      NOT NULL CONSTRAINT DF_OrdemProducao_Status DEFAULT ('Planejada'),
    CodigoQr          uniqueidentifier NOT NULL CONSTRAINT DF_OrdemProducao_CodigoQr DEFAULT (NEWID()),
    PrevisaoConclusao date             NULL,
    CriadaEm          datetime2(0)     NOT NULL CONSTRAINT DF_OrdemProducao_CriadaEm DEFAULT (SYSUTCDATETIME()),
    ConcluidaEm       datetime2(0)     NULL,
    CONSTRAINT PK_OrdemProducao PRIMARY KEY (OrdemProducaoId),
    CONSTRAINT UQ_OrdemProducao_Numero UNIQUE (Numero),
    CONSTRAINT UQ_OrdemProducao_CodigoQr UNIQUE (CodigoQr),
    CONSTRAINT FK_OrdemProducao_PedidoItem FOREIGN KEY (PedidoItemId) REFERENCES dbo.PedidoItem (PedidoItemId),
    CONSTRAINT FK_OrdemProducao_Produto FOREIGN KEY (ProdutoId) REFERENCES dbo.Produto (ProdutoId),
    CONSTRAINT CK_OrdemProducao_Quantidade CHECK (Quantidade > 0),
    CONSTRAINT CK_OrdemProducao_Status CHECK (Status IN ('Planejada', 'EmAndamento', 'Concluida', 'Cancelada'))
);

CREATE TABLE dbo.OrdemProducaoEtapa (
    OrdemProducaoEtapaId int IDENTITY(1,1) NOT NULL,
    OrdemProducaoId      int          NOT NULL,
    EstacaoId            tinyint      NOT NULL,
    Sequencia            tinyint      NOT NULL,
    Status               varchar(12)  NOT NULL CONSTRAINT DF_OrdemProducaoEtapa_Status DEFAULT ('Pendente'),
    IniciadaEm           datetime2(0) NULL,
    ConcluidaEm          datetime2(0) NULL,
    OperadorId           int          NULL,
    CONSTRAINT PK_OrdemProducaoEtapa PRIMARY KEY (OrdemProducaoEtapaId),
    CONSTRAINT UQ_OrdemProducaoEtapa_Sequencia UNIQUE (OrdemProducaoId, Sequencia),
    CONSTRAINT FK_OrdemProducaoEtapa_OP FOREIGN KEY (OrdemProducaoId) REFERENCES dbo.OrdemProducao (OrdemProducaoId),
    CONSTRAINT FK_OrdemProducaoEtapa_Estacao FOREIGN KEY (EstacaoId) REFERENCES dbo.EstacaoProducao (EstacaoId),
    CONSTRAINT FK_OrdemProducaoEtapa_Operador FOREIGN KEY (OperadorId) REFERENCES dbo.Usuario (UsuarioId),
    CONSTRAINT CK_OrdemProducaoEtapa_Status CHECK (Status IN ('Pendente', 'EmAndamento', 'Concluida'))
);

CREATE TABLE dbo.ApontamentoProducao (
    ApontamentoId        bigint IDENTITY(1,1) NOT NULL,
    OrdemProducaoEtapaId int              NOT NULL,
    Tipo                 varchar(6)       NOT NULL,
    UsuarioId            int              NOT NULL,
    DataHoraCliente      datetime2(0)     NOT NULL,
    DataHoraServidor     datetime2(0)     NOT NULL CONSTRAINT DF_ApontamentoProducao_DataHoraServidor DEFAULT (SYSUTCDATETIME()),
    ClientRequestId      uniqueidentifier NOT NULL,
    CONSTRAINT PK_ApontamentoProducao PRIMARY KEY (ApontamentoId),
    CONSTRAINT UQ_ApontamentoProducao_ClientRequestId UNIQUE (ClientRequestId),
    CONSTRAINT FK_ApontamentoProducao_Etapa FOREIGN KEY (OrdemProducaoEtapaId) REFERENCES dbo.OrdemProducaoEtapa (OrdemProducaoEtapaId),
    CONSTRAINT FK_ApontamentoProducao_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (UsuarioId),
    CONSTRAINT CK_ApontamentoProducao_Tipo CHECK (Tipo IN ('Inicio', 'Fim'))
);

CREATE TABLE dbo.RegistroPerda (
    RegistroPerdaId      int IDENTITY(1,1) NOT NULL,
    OrdemProducaoEtapaId int           NOT NULL,
    Tipo                 varchar(10)   NOT NULL,
    Quantidade           int           NOT NULL,
    Motivo               nvarchar(200) NOT NULL,
    UsuarioId            int           NOT NULL,
    CriadoEm             datetime2(0)  NOT NULL CONSTRAINT DF_RegistroPerda_CriadoEm DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_RegistroPerda PRIMARY KEY (RegistroPerdaId),
    CONSTRAINT FK_RegistroPerda_Etapa FOREIGN KEY (OrdemProducaoEtapaId) REFERENCES dbo.OrdemProducaoEtapa (OrdemProducaoEtapaId),
    CONSTRAINT FK_RegistroPerda_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (UsuarioId),
    CONSTRAINT CK_RegistroPerda_Tipo CHECK (Tipo IN ('Refugo', 'Retrabalho')),
    CONSTRAINT CK_RegistroPerda_Quantidade CHECK (Quantidade > 0)
);

-- 3.5 Qualidade e Expedição -------------------------------------------------

CREATE TABLE dbo.Inspecao (
    InspecaoId           int IDENTITY(1,1) NOT NULL,
    OrdemProducaoId      int           NOT NULL,
    InspetorId           int           NOT NULL,
    Resultado            varchar(10)   NOT NULL,
    EstacaoRetrabalhoId  tinyint       NULL,
    Observacao           nvarchar(300) NULL,
    DataHora             datetime2(0)  NOT NULL CONSTRAINT DF_Inspecao_DataHora DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Inspecao PRIMARY KEY (InspecaoId),
    CONSTRAINT FK_Inspecao_OP FOREIGN KEY (OrdemProducaoId) REFERENCES dbo.OrdemProducao (OrdemProducaoId),
    CONSTRAINT FK_Inspecao_Inspetor FOREIGN KEY (InspetorId) REFERENCES dbo.Usuario (UsuarioId),
    CONSTRAINT FK_Inspecao_EstacaoRetrabalho FOREIGN KEY (EstacaoRetrabalhoId) REFERENCES dbo.EstacaoProducao (EstacaoId),
    CONSTRAINT CK_Inspecao_Resultado CHECK (Resultado IN ('Aprovada', 'Reprovada')),
    CONSTRAINT CK_Inspecao_Retrabalho CHECK (Resultado = 'Aprovada' OR EstacaoRetrabalhoId IS NOT NULL)
);

CREATE TABLE dbo.Expedicao (
    ExpedicaoId     int IDENTITY(1,1) NOT NULL,
    PedidoId        int          NOT NULL,
    UsuarioId       int          NOT NULL,
    Transportadora  nvarchar(80) NULL,
    CodigoRastreio  varchar(40)  NULL,
    DataHora        datetime2(0) NOT NULL CONSTRAINT DF_Expedicao_DataHora DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Expedicao PRIMARY KEY (ExpedicaoId),
    CONSTRAINT UQ_Expedicao_Pedido UNIQUE (PedidoId),
    CONSTRAINT FK_Expedicao_Pedido FOREIGN KEY (PedidoId) REFERENCES dbo.Pedido (PedidoId),
    CONSTRAINT FK_Expedicao_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (UsuarioId)
);

-- 3.6 Estoque ---------------------------------------------------------------

CREATE TABLE dbo.MovimentoEstoque (
    MovimentoEstoqueId bigint IDENTITY(1,1) NOT NULL,
    MateriaPrimaId     int           NULL,
    ProdutoId          int           NULL,
    Tipo               varchar(8)    NOT NULL,
    Quantidade         decimal(12,3) NOT NULL,
    Origem             varchar(20)   NOT NULL,
    OrigemId           int           NULL,
    UsuarioId          int           NULL,
    Motivo             nvarchar(200) NULL,
    DataHora           datetime2(0)  NOT NULL CONSTRAINT DF_MovimentoEstoque_DataHora DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_MovimentoEstoque PRIMARY KEY (MovimentoEstoqueId),
    CONSTRAINT FK_MovimentoEstoque_MateriaPrima FOREIGN KEY (MateriaPrimaId) REFERENCES dbo.MateriaPrima (MateriaPrimaId),
    CONSTRAINT FK_MovimentoEstoque_Produto FOREIGN KEY (ProdutoId) REFERENCES dbo.Produto (ProdutoId),
    CONSTRAINT FK_MovimentoEstoque_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (UsuarioId),
    CONSTRAINT CK_MovimentoEstoque_Tipo CHECK (Tipo IN ('Entrada', 'Saida', 'Ajuste')),
    CONSTRAINT CK_MovimentoEstoque_Origem CHECK (Origem IN ('Compra', 'Producao', 'ProducaoConcluida', 'Expedicao', 'Ajuste')),
    CONSTRAINT CK_MovimentoEstoque_Item CHECK (
        (MateriaPrimaId IS NOT NULL AND ProdutoId IS NULL) OR (MateriaPrimaId IS NULL AND ProdutoId IS NOT NULL)),
    CONSTRAINT CK_MovimentoEstoque_Sinal CHECK (
        (Tipo = 'Entrada' AND Quantidade > 0)
        OR (Tipo = 'Saida' AND Quantidade < 0)
        OR (Tipo = 'Ajuste' AND Quantidade <> 0))
);

CREATE TABLE dbo.AlertaEstoque (
    AlertaEstoqueId int IDENTITY(1,1) NOT NULL,
    MateriaPrimaId  int          NOT NULL,
    CriadoEm        datetime2(0) NOT NULL CONSTRAINT DF_AlertaEstoque_CriadoEm DEFAULT (SYSUTCDATETIME()),
    LidoEm          datetime2(0) NULL,
    LidoPorId       int          NULL,
    CONSTRAINT PK_AlertaEstoque PRIMARY KEY (AlertaEstoqueId),
    CONSTRAINT FK_AlertaEstoque_MateriaPrima FOREIGN KEY (MateriaPrimaId) REFERENCES dbo.MateriaPrima (MateriaPrimaId),
    CONSTRAINT FK_AlertaEstoque_LidoPor FOREIGN KEY (LidoPorId) REFERENCES dbo.Usuario (UsuarioId)
);

-- 3.7 Compras ---------------------------------------------------------------

CREATE TABLE dbo.PedidoCompra (
    PedidoCompraId int IDENTITY(1,1) NOT NULL,
    Numero         varchar(15)  NOT NULL
        CONSTRAINT DF_PedidoCompra_Numero DEFAULT (
            'PC-' + CAST(YEAR(SYSUTCDATETIME()) AS char(4)) + '-'
            + RIGHT('00000' + CAST(NEXT VALUE FOR dbo.seq_NumeroPedidoCompra AS varchar(10)), 5)),
    FornecedorId   int          NOT NULL,
    CompradorId    int          NOT NULL,
    Status         varchar(22)  NOT NULL CONSTRAINT DF_PedidoCompra_Status DEFAULT ('Solicitado'),
    DataPrevista   date         NULL,
    CriadoEm       datetime2(0) NOT NULL CONSTRAINT DF_PedidoCompra_CriadoEm DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_PedidoCompra PRIMARY KEY (PedidoCompraId),
    CONSTRAINT UQ_PedidoCompra_Numero UNIQUE (Numero),
    CONSTRAINT FK_PedidoCompra_Fornecedor FOREIGN KEY (FornecedorId) REFERENCES dbo.Fornecedor (FornecedorId),
    CONSTRAINT FK_PedidoCompra_Comprador FOREIGN KEY (CompradorId) REFERENCES dbo.Usuario (UsuarioId),
    CONSTRAINT CK_PedidoCompra_Status CHECK (Status IN ('Solicitado', 'Emitido', 'ParcialmenteRecebido', 'Recebido', 'Cancelado'))
);

CREATE TABLE dbo.PedidoCompraItem (
    PedidoCompraItemId int IDENTITY(1,1) NOT NULL,
    PedidoCompraId     int           NOT NULL,
    MateriaPrimaId     int           NOT NULL,
    Quantidade         decimal(12,3) NOT NULL,
    CustoUnitario      decimal(12,4) NOT NULL,
    QuantidadeRecebida decimal(12,3) NOT NULL CONSTRAINT DF_PedidoCompraItem_QuantidadeRecebida DEFAULT (0),
    CONSTRAINT PK_PedidoCompraItem PRIMARY KEY (PedidoCompraItemId),
    CONSTRAINT UQ_PedidoCompraItem UNIQUE (PedidoCompraId, MateriaPrimaId),
    CONSTRAINT FK_PedidoCompraItem_PedidoCompra FOREIGN KEY (PedidoCompraId) REFERENCES dbo.PedidoCompra (PedidoCompraId),
    CONSTRAINT FK_PedidoCompraItem_MateriaPrima FOREIGN KEY (MateriaPrimaId) REFERENCES dbo.MateriaPrima (MateriaPrimaId),
    CONSTRAINT CK_PedidoCompraItem_Quantidade CHECK (Quantidade > 0),
    CONSTRAINT CK_PedidoCompraItem_CustoUnitario CHECK (CustoUnitario >= 0),
    CONSTRAINT CK_PedidoCompraItem_Recebida CHECK (QuantidadeRecebida BETWEEN 0 AND Quantidade)
);

-- 3.8 Auditoria -------------------------------------------------------------

CREATE TABLE dbo.Auditoria (
    AuditoriaId bigint IDENTITY(1,1) NOT NULL,
    Tabela      sysname       NOT NULL,
    Registro    varchar(50)   NOT NULL,
    Acao        char(1)       NOT NULL,
    UsuarioId   int           NULL,
    DataHora    datetime2(0)  NOT NULL CONSTRAINT DF_Auditoria_DataHora DEFAULT (SYSUTCDATETIME()),
    Antes       nvarchar(max) NULL,
    Depois      nvarchar(max) NULL,
    CONSTRAINT PK_Auditoria PRIMARY KEY (AuditoriaId),
    CONSTRAINT CK_Auditoria_Acao CHECK (Acao IN ('I', 'U', 'D'))
);
GO

-- 4. Índices ----------------------------------------------------------------

CREATE INDEX IX_Pedido_Cliente_Status ON dbo.Pedido (ClienteId, Status) INCLUDE (PrazoEntrega, ValorTotal);
CREATE INDEX IX_Pedido_Prazo ON dbo.Pedido (PrazoEntrega) WHERE Status IN ('Confirmado', 'EmProducao');
CREATE INDEX IX_PedidoItem_Pedido ON dbo.PedidoItem (PedidoId) INCLUDE (ProdutoId, Quantidade);
CREATE INDEX IX_OP_PedidoItem ON dbo.OrdemProducao (PedidoItemId);
CREATE INDEX IX_OP_Status ON dbo.OrdemProducao (Status) INCLUDE (PrevisaoConclusao);
CREATE INDEX IX_OPE_Estacao_Status ON dbo.OrdemProducaoEtapa (EstacaoId, Status) INCLUDE (OrdemProducaoId, Sequencia);
CREATE INDEX IX_Mov_MP_Data ON dbo.MovimentoEstoque (MateriaPrimaId, DataHora DESC);
CREATE INDEX IX_Mov_Prod_Data ON dbo.MovimentoEstoque (ProdutoId, DataHora DESC);
CREATE INDEX IX_MP_Critico ON dbo.MateriaPrima (EstoqueAtual) INCLUDE (EstoqueMinimo) WHERE Ativo = 1;
CREATE UNIQUE INDEX UQ_Alerta_Aberto ON dbo.AlertaEstoque (MateriaPrimaId) WHERE LidoEm IS NULL;
CREATE INDEX IX_Ficha_Produto ON dbo.FichaTecnicaItem (ProdutoId, EstacaoId) INCLUDE (MateriaPrimaId, Quantidade);
CREATE INDEX IX_Auditoria_Tabela_Reg ON dbo.Auditoria (Tabela, Registro, DataHora DESC);

-- Apoio às chaves estrangeiras mais consultadas
CREATE INDEX IX_Apontamento_Etapa ON dbo.ApontamentoProducao (OrdemProducaoEtapaId);
CREATE INDEX IX_Inspecao_OP ON dbo.Inspecao (OrdemProducaoId);
CREATE INDEX IX_RefreshToken_Usuario ON dbo.RefreshToken (UsuarioId);
CREATE INDEX IX_PedidoCompraItem_MP ON dbo.PedidoCompraItem (MateriaPrimaId);
GO
