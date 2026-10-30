# Aurora Cadeiras (PIM IV)

Sistema de automação da fábrica de cadeiras fictícia **Aurora Cadeiras**
(pedido → ordem de produção → etapas por estação → estoque → qualidade → expedição).
Projeto Integrado Multidisciplinar IV, Análise e Desenvolvimento de Sistemas, UNIP.

A especificação está em [`docs/`](docs/README.md); o contexto para o Claude Code, em [`CLAUDE.md`](CLAUDE.md).

## Status

| Fase | Situação |
|---|---|
| 0 Esqueleto da solução | Concluída |
| 1a Banco (25 tabelas, procedures, triggers, segurança, seed) | Concluída, 37 testes de integração |
| 1b API base e autenticação (JWT, refresh, usuários, perfis, Swagger, health) | Concluída, 77 testes |
| 1c Cadastros básicos (produtos e matérias-primas), runner DbUp, Docker e CI | Concluída, 211 testes no total |
| 2a API da fase 2 (clientes, fornecedores, ficha técnica, roteiro, estações, pedidos, movimentos e ajuste de estoque) | Concluída |
| 2b Painel web (login por cookie, layout acessível, cadastros, produtos com ficha técnica e roteiro, pedidos) | Concluída, 532 testes no total |
| 3 | Próxima (produção, estoque e alertas) |
| 4 a 6 | A fazer (ver `docs/10-plano-implementacao.md`) |

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/Aurora.Domain` | Entidades, enums, eventos e exceções de domínio |
| `src/Aurora.Application` | Casos de uso, DTOs, validadores, interfaces |
| `src/Aurora.Infrastructure` | EF Core, procedures, JWT, QR code, relatórios |
| `src/Aurora.Api` | Web API REST (composição das dependências) |
| `src/Aurora.Web` | Painel administrativo ASP.NET Core MVC (cliente tipado da API em `Api/`, sessão em cookie, telas por módulo) |
| `src/Aurora.DbUp` | Runner que aplica os scripts de `database/` (usado pelo Docker e por qualquer ambiente) |
| `tests/` | Domain, Application, Api (integração), Database (procedures e triggers) e Tests.Common (SQL Server de teste) |
| `database/` | Scripts SQL numerados `001` a `006` (schema, views, procedures, triggers, segurança, seed) |
| `docker/` | Dockerfiles (api, web, dbup) e `docker-compose.yml` |
| `.github/workflows/` | CI (`ci.yml`) e análise estática (`codeql.yml`) |
| `infra/` | Bicep (fase 6) |
| `mobile/aurora_mobile` | App mobile, fora da solução .NET (tecnologia a definir) |

## Comandos

```bash
dotnet build
dotnet test        # testes de banco e de API sobem um SQL Server em container (Docker aberto)
```

Para rodar os testes contra um SQL Server já instalado (sem Docker), defina `AURORA_TEST_SQL` com a
connection string do servidor; os bancos `aurora_test_*` são criados e apagados automaticamente.

## Subindo tudo com Docker

```bash
cp docker/.env.example docker/.env      # preencha SA_PASSWORD, JWT_KEY e SEED_ADMIN_PASSWORD
docker compose -f docker/docker-compose.yml up --build
```

Sobem, nesta ordem: `sql` (SQL Server 2022), `dbup` (aplica `database/*.sql` e termina), `api` (porta 8080) e
`web` (porta 8081). Teste com `curl localhost:8080/health/ready`. O arquivo `docker/.env` é ignorado pelo Git.
Para ligar o Swagger localmente, defina `SWAGGER_HABILITADO=true` no `.env`.

> No compose a API conecta como `sa`, só para o ambiente local. Em homologação e produção a conexão vem do
> Key Vault, com o login restrito `role_aurora_app` criado conforme `database/005-seguranca.sql`.

Para aplicar os scripts em um banco já existente, sem Docker:

```bash
ConnectionStrings__Aurora="<connection string>" dotnet run --project src/Aurora.DbUp
```

## Rodando a API localmente

1. Crie o banco e aplique os scripts de `database/` com o runner acima (ou na ordem `001` a `006`).
2. Configure os segredos fora do repositório:

```bash
cd src/Aurora.Api
dotnet user-secrets set "Jwt:Key" "<texto aleatório com 32 caracteres ou mais>"
dotnet user-secrets set "Seed:AdminPassword" "<senha do administrador inicial>"
dotnet run
```

3. Abra `/swagger` (ambiente Development), faça login em `POST /api/v1/auth/login` com `admin@aurora.local`
   e a senha de `Seed:AdminPassword`, e use o token no botão **Authorize**.

A connection string de desenvolvimento (`appsettings.Development.json`) usa a autenticação do Windows em
`localhost`, sem senha. Em hom/prod ela vem de `ConnectionStrings__Aurora` (Key Vault).

## Painel web

Com o ambiente de pé (`docker compose`), abra http://localhost:8081 e entre com `admin@aurora.local` e a senha
definida em `SEED_ADMIN_PASSWORD`. O painel só fala com a API (`Api__BaseUrl`); a sessão fica num cookie e o menu
mostra só as áreas do perfil. Sem Docker, rode a API e depois `dotnet run --project src/Aurora.Web` (a URL da API
de desenvolvimento está em `appsettings.Development.json`).

## Decisões

Registradas em [`docs/DECISOES.md`](docs/DECISOES.md).
