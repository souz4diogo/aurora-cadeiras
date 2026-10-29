-- 006-seed.sql
-- Dados iniciais (docs/05-banco-de-dados.md §10). Sem senhas: usuários de demonstração
-- e pedidos de exemplo (que precisam de um vendedor) são criados pelo seeder da aplicação.
GO

INSERT dbo.Perfil (PerfilId, Nome) VALUES
    (1, 'Administrador'), (2, 'GerenteProducao'), (3, 'Operador'), (4, 'Vendedor'),
    (5, 'Almoxarife'), (6, 'Inspetor'), (7, 'Cliente');

INSERT dbo.EstacaoProducao (EstacaoId, Nome, Ordem, GeraEtapa) VALUES
    (1, 'Corte', 1, 1),
    (2, 'Montagem da Estrutura', 2, 1),
    (3, 'Estofaria', 3, 1),
    (4, 'Montagem Final', 4, 1),
    (5, 'Qualidade', 5, 1),
    (6, 'Expedicao', 6, 0);

INSERT dbo.LinhaProduto (LinhaProdutoId, Nome) VALUES (1, 'Escritorio'), (2, 'Jantar'), (3, 'Estofada');

INSERT dbo.Produto (Codigo, Nome, LinhaProdutoId, PrecoVenda) VALUES
    ('CE-100', N'Cadeira Ergonômica Presidente', 1, 1290.00),
    ('CE-200', N'Cadeira Operacional Giratória',  1,  690.00),
    ('CJ-100', N'Cadeira de Jantar Ipê',          2,  480.00),
    ('CJ-200', N'Cadeira de Jantar Cumaru',       2,  560.00),
    ('CS-100', N'Cadeira Estofada Linho',         3,  620.00),
    ('CS-200', N'Cadeira Estofada Veludo',        3,  740.00);

-- Fornecedores (CNPJs fictícios); ao menos dois de comunidades tradicionais
INSERT dbo.Fornecedor (RazaoSocial, Cnpj, Email, ComunidadeTradicional) VALUES
    (N'Madeireira Serra Verde Ltda',                 '11000000000101', 'vendas@serraverde.example', 0),
    (N'Metalúrgica Tubos do Sul S.A.',               '11000000000102', 'comercial@tubosdosul.example', 0),
    (N'Têxtil Horizonte Ltda',                       '11000000000103', 'pedidos@textilhorizonte.example', 0),
    (N'Associação de Artesãs Palha do Cerrado',      '11000000000104', 'contato@palhacerrado.example', 1),
    (N'Cooperativa Extrativista Ribeirinha Aruanã',  '11000000000105', 'cooperativa@aruana.example', 1);

-- Matérias-primas; saldo inicial entra como movimento de ajuste (livro-razão)
INSERT dbo.MateriaPrima (Codigo, Nome, Unidade, EstoqueMinimo, CustoUnitario, FornecedorPreferencialId) VALUES
    ('MP-001', N'Madeira maciça',        'm3',   2.000, 4500.0000, (SELECT FornecedorId FROM dbo.Fornecedor WHERE Cnpj = '11000000000105')),
    ('MP-002', N'Compensado',            'm2',  50.000,   85.0000, (SELECT FornecedorId FROM dbo.Fornecedor WHERE Cnpj = '11000000000101')),
    ('MP-003', N'Tubo metálico',         'm',  100.000,   12.0000, (SELECT FornecedorId FROM dbo.Fornecedor WHERE Cnpj = '11000000000102')),
    ('MP-004', N'Espuma D33',            'm3',   3.000,  900.0000, (SELECT FornecedorId FROM dbo.Fornecedor WHERE Cnpj = '11000000000103')),
    ('MP-005', N'Tecido linho',          'm',   80.000,   38.0000, (SELECT FornecedorId FROM dbo.Fornecedor WHERE Cnpj = '11000000000103')),
    ('MP-006', N'Tecido veludo',         'm',   80.000,   52.0000, (SELECT FornecedorId FROM dbo.Fornecedor WHERE Cnpj = '11000000000103')),
    ('MP-007', N'Palha natural',         'm2',  20.000,   70.0000, (SELECT FornecedorId FROM dbo.Fornecedor WHERE Cnpj = '11000000000104')),
    ('MP-008', N'Parafusos',             'un', 1000.000,   0.1500, (SELECT FornecedorId FROM dbo.Fornecedor WHERE Cnpj = '11000000000102')),
    ('MP-009', N'Cola',                  'l',   10.000,   22.0000, NULL),
    ('MP-010', N'Verniz',                'l',   10.000,   45.0000, NULL),
    ('MP-011', N'Rodízio',               'un',  200.000,    6.0000, (SELECT FornecedorId FROM dbo.Fornecedor WHERE Cnpj = '11000000000102')),
    ('MP-012', N'Pistão a gás',          'un',   50.000,   28.0000, (SELECT FornecedorId FROM dbo.Fornecedor WHERE Cnpj = '11000000000102')),
    ('MP-013', N'Assento plástico ABS',  'un',   50.000,   18.0000, NULL),
    ('MP-014', N'Embalagem',             'un',  100.000,    7.0000, NULL);

INSERT dbo.MovimentoEstoque (MateriaPrimaId, Tipo, Quantidade, Origem, Motivo)
SELECT mp.MateriaPrimaId, 'Ajuste', s.Saldo, 'Ajuste', N'Saldo inicial'
  FROM (VALUES ('MP-001', 20), ('MP-002', 400), ('MP-003', 1500), ('MP-004', 25), ('MP-005', 600),
               ('MP-006', 500), ('MP-007', 150), ('MP-008', 20000), ('MP-009', 150), ('MP-010', 120),
               ('MP-011', 3000), ('MP-012', 500), ('MP-013', 400), ('MP-014', 2000)) s (Codigo, Saldo)
  JOIN dbo.MateriaPrima mp ON mp.Codigo = s.Codigo;

-- Roteiros: Escritório e Estofada passam por 5 estações; Jantar não tem Estofaria
INSERT dbo.RoteiroProduto (ProdutoId, EstacaoId, Sequencia)
SELECT p.ProdutoId, r.EstacaoId, r.Sequencia
  FROM dbo.Produto p
  JOIN dbo.LinhaProduto l ON l.LinhaProdutoId = p.LinhaProdutoId
  JOIN (VALUES ('Escritorio', 1, 1), ('Escritorio', 2, 2), ('Escritorio', 3, 3), ('Escritorio', 4, 4), ('Escritorio', 5, 5),
               ('Estofada',   1, 1), ('Estofada',   2, 2), ('Estofada',   3, 3), ('Estofada',   4, 4), ('Estofada',   5, 5),
               ('Jantar',     1, 1), ('Jantar',     2, 2), ('Jantar',     4, 3), ('Jantar',     5, 4)) r (Linha, EstacaoId, Sequencia)
    ON r.Linha = l.Nome;

-- Fichas técnicas: material consumido em cada estação (a Qualidade não consome)
INSERT dbo.FichaTecnicaItem (ProdutoId, MateriaPrimaId, EstacaoId, Quantidade)
SELECT p.ProdutoId, mp.MateriaPrimaId, f.EstacaoId, f.Quantidade
  FROM (VALUES
    ('CE-100','MP-002',1,0.500), ('CE-100','MP-003',2,6.000), ('CE-100','MP-008',2,12.000),
    ('CE-100','MP-004',3,0.020), ('CE-100','MP-005',3,1.200), ('CE-100','MP-009',3,0.100),
    ('CE-100','MP-011',4,5.000), ('CE-100','MP-012',4,1.000), ('CE-100','MP-008',4,8.000), ('CE-100','MP-014',4,1.000),

    ('CE-200','MP-002',1,0.400), ('CE-200','MP-003',2,5.000), ('CE-200','MP-008',2,10.000),
    ('CE-200','MP-004',3,0.015), ('CE-200','MP-005',3,1.000), ('CE-200','MP-009',3,0.080),
    ('CE-200','MP-011',4,5.000), ('CE-200','MP-012',4,1.000), ('CE-200','MP-013',4,1.000), ('CE-200','MP-014',4,1.000),

    ('CJ-100','MP-001',1,0.030), ('CJ-100','MP-008',2,8.000), ('CJ-100','MP-009',2,0.050),
    ('CJ-100','MP-010',4,0.150), ('CJ-100','MP-014',4,1.000),

    ('CJ-200','MP-001',1,0.035), ('CJ-200','MP-008',2,8.000), ('CJ-200','MP-009',2,0.060),
    ('CJ-200','MP-010',4,0.200), ('CJ-200','MP-007',4,0.500), ('CJ-200','MP-014',4,1.000),

    ('CS-100','MP-001',1,0.020), ('CS-100','MP-002',1,0.300), ('CS-100','MP-008',2,10.000), ('CS-100','MP-009',2,0.060),
    ('CS-100','MP-004',3,0.025), ('CS-100','MP-005',3,1.500), ('CS-100','MP-009',3,0.100),
    ('CS-100','MP-010',4,0.100), ('CS-100','MP-014',4,1.000),

    ('CS-200','MP-001',1,0.020), ('CS-200','MP-002',1,0.300), ('CS-200','MP-008',2,10.000), ('CS-200','MP-009',2,0.060),
    ('CS-200','MP-004',3,0.025), ('CS-200','MP-006',3,1.600), ('CS-200','MP-009',3,0.100),
    ('CS-200','MP-010',4,0.100), ('CS-200','MP-014',4,1.000)
  ) f (Produto, Materia, EstacaoId, Quantidade)
  JOIN dbo.Produto p ON p.Codigo = f.Produto
  JOIN dbo.MateriaPrima mp ON mp.Codigo = f.Materia;

INSERT dbo.Cliente (Nome, CpfCnpj, Email, Telefone, Cidade, Uf) VALUES
    (N'Móveis Bela Vista Ltda',   '22000000000101', 'compras@belavista.example',  '1130000001', N'São Paulo',      'SP'),
    (N'Hotel Recanto das Águas',  '22000000000102', 'suprimentos@recanto.example', '4130000002', N'Curitiba',       'PR'),
    (N'Escritórios Prime S.A.',   '22000000000103', 'facilities@prime.example',    '3130000003', N'Belo Horizonte', 'MG');
GO
