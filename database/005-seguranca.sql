-- 005-seguranca.sql
-- Mínimo privilégio, acesso por procedure e mascaramento (docs/05-banco-de-dados.md §9).
--
-- Sem senhas no repositório: este script cria apenas PAPÉIS do banco. Os logins e usuários
-- (aurora_app, aurora_readonly) são criados pela infraestrutura com a senha vinda do Key Vault
-- ou de variável de ambiente, e depois adicionados aos papéis abaixo:
--   CREATE LOGIN aurora_app WITH PASSWORD = '<segredo>';
--   CREATE USER aurora_app FOR LOGIN aurora_app;
--   ALTER ROLE role_aurora_app ADD MEMBER aurora_app;
GO

CREATE ROLE role_aurora_app;
CREATE ROLE role_aurora_readonly;
GO

-- Aplicação: CRUD das tabelas de cadastro, pedidos, compras e perdas ---------
GRANT SELECT ON SCHEMA::dbo TO role_aurora_app;

GRANT INSERT, UPDATE ON dbo.Usuario          TO role_aurora_app;
GRANT INSERT, UPDATE, DELETE ON dbo.RefreshToken TO role_aurora_app;
GRANT INSERT, UPDATE ON dbo.Cliente          TO role_aurora_app;
GRANT INSERT, UPDATE ON dbo.Fornecedor       TO role_aurora_app;
GRANT INSERT, UPDATE ON dbo.Produto          TO role_aurora_app;
GRANT INSERT, UPDATE ON dbo.MateriaPrima     TO role_aurora_app;
GRANT INSERT, UPDATE, DELETE ON dbo.RoteiroProduto    TO role_aurora_app;
GRANT INSERT, UPDATE, DELETE ON dbo.FichaTecnicaItem  TO role_aurora_app;
GRANT INSERT, UPDATE ON dbo.Pedido           TO role_aurora_app;
GRANT INSERT, UPDATE, DELETE ON dbo.PedidoItem        TO role_aurora_app;
GRANT INSERT, UPDATE ON dbo.PedidoCompra     TO role_aurora_app;
GRANT INSERT, UPDATE, DELETE ON dbo.PedidoCompraItem  TO role_aurora_app;
GRANT INSERT, UPDATE ON dbo.RegistroPerda    TO role_aurora_app;
GRANT INSERT, UPDATE ON dbo.AlertaEstoque    TO role_aurora_app;
GRANT INSERT, UPDATE ON dbo.OrdemProducao    TO role_aurora_app;
GRANT INSERT ON dbo.Inspecao                 TO role_aurora_app;
GRANT INSERT ON dbo.Expedicao                TO role_aurora_app;

GRANT EXECUTE ON SCHEMA::dbo TO role_aurora_app;
GRANT UNMASK TO role_aurora_app;
GRANT VIEW DEFINITION ON SCHEMA::dbo TO role_aurora_app;
GO

-- Estoque e produção só mudam pelas procedures (cadeia de propriedade dbo) ----
DENY INSERT, UPDATE, DELETE ON dbo.MovimentoEstoque     TO role_aurora_app;
DENY INSERT, UPDATE, DELETE ON dbo.EstoqueProduto       TO role_aurora_app;
DENY INSERT, UPDATE, DELETE ON dbo.OrdemProducaoEtapa   TO role_aurora_app;
DENY INSERT, UPDATE, DELETE ON dbo.ApontamentoProducao  TO role_aurora_app;
DENY INSERT, UPDATE, DELETE ON dbo.Auditoria            TO role_aurora_app;
GO

-- Relatórios: somente as views ------------------------------------------------
GRANT SELECT ON dbo.vw_EstoqueCritico                TO role_aurora_readonly;
GRANT SELECT ON dbo.vw_PainelProducao                TO role_aurora_readonly;
GRANT SELECT ON dbo.vw_PedidosAtrasados              TO role_aurora_readonly;
GRANT SELECT ON dbo.vw_AcompanhamentoPedido          TO role_aurora_readonly;
GRANT SELECT ON dbo.vw_ConsumoMateriaPrima           TO role_aurora_readonly;
GRANT SELECT ON dbo.vw_ComprasComunidadesTradicionais TO role_aurora_readonly;
GO

-- LGPD: Dynamic Data Masking (quem não tem UNMASK vê os dados mascarados) ------
ALTER TABLE dbo.Cliente ALTER COLUMN CpfCnpj ADD MASKED WITH (FUNCTION = 'partial(2, "XXXXXXXX", 2)');
ALTER TABLE dbo.Cliente ALTER COLUMN Email   ADD MASKED WITH (FUNCTION = 'email()');
ALTER TABLE dbo.Cliente ALTER COLUMN Telefone ADD MASKED WITH (FUNCTION = 'default()');
GO
