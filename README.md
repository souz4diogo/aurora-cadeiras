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
| 1c Cadastros básicos, Docker e CI | Próxima |
| 2 a 6 | A fazer (ver `docs/10-plano-implementacao.md`) |

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/Aurora.Domain` | Entidades, enums, eventos e exceções de domínio |
| `src/Aurora.Application` | Casos de uso, DTOs, validadores, interfaces |
| `src/Aurora.Infrastructure` | EF Core, procedures, JWT, QR code, relatórios |
| `src/Aurora.Api` | Web API REST (composição das dependências) |
| `src/Aurora.Web` | Painel administrativo ASP.NET Core MVC |
| `tests/` | Domain, Application, Api (integração), Database (procedures e triggers) e Tests.Common (SQL Server de teste) |
| `database/` | Scripts SQL numerados `001` a `006` (schema, views, procedures, triggers, segurança, seed) |
| `docker/`, `infra/`, `.github/workflows/` | Containers, Bicep e CI/CD (fases seguintes) |
| `mobile/aurora_mobile` | App mobile, fora da solução .NET (tecnologia a definir) |

## Comandos

```bash
dotnet build
dotnet test        # testes de banco e de API sobem um SQL Server em container (Docker aberto)
```

Para rodar os testes contra um SQL Server já instalado (sem Docker), defina `AURORA_TEST_SQL` com a
connection string do servidor; os bancos `aurora_test_*` são criados e apagados automaticamente.

## Rodando a API localmente

1. Crie o banco e aplique os scripts de `database/` na ordem (`001` a `006`); o runner DbUp chega na fase 1c.
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

## Decisões

Registradas em [`docs/DECISOES.md`](docs/DECISOES.md).
