# CLAUDE.md - Contexto do Projeto Aurora Cadeiras (PIM IV)

## O que é este projeto

Sistema de automação da fábrica de cadeiras **fictícia** "Aurora Cadeiras", feito como
**PIM IV** do curso de Análise e Desenvolvimento de Sistemas (UNIP) por um grupo de 4 alunos.
Entregáveis: o sistema (API, web, mobile, banco, infraestrutura) **e** um documento
acadêmico de 20 a 30 páginas (escrito à parte, depois da implementação).

Fluxo de negócio: **pedido → ordem de produção → etapas por estação → estoque →
qualidade → expedição**.

## Onde está a especificação (leia antes de codar)

| Assunto | Arquivo |
|---|---|
| Escopo, requisitos (RF/RNF), regras (RN), casos de uso | `docs/00-escopo.md` |
| Arquitetura, camadas, padrões, convenções, testes | `docs/04-arquitetura.md` |
| Banco: tabelas, procedures, triggers, índices, segurança | `docs/05-banco-de-dados.md` |
| API REST: endpoints, permissões, contratos, erros | `docs/06-api-rest.md` |
| Painel web | `docs/07-web-admin.md` |
| App mobile e sincronização offline | `docs/08-mobile.md` |
| Nuvem, containers, CI/CD | `docs/09-nuvem-devops.md` |
| Backlog e sprints | `docs/02-backlog-agil.md` |
| Fases e prompts de implementação | `docs/10-plano-implementacao.md` |

**Os documentos são a fonte da verdade.** Se algo faltar ou for ambíguo, **pergunte** ou
registre a decisão em `docs/DECISOES.md`. Não invente requisitos nem altere documentos
sem avisar.

## Stack

.NET LTS (10; usar 8 se a disciplina usar) · ASP.NET Core Web API · ASP.NET Core MVC (Razor) ·
EF Core + procedures · SQL Server · DbUp · xUnit · Docker · GitHub Actions · Azure (Bicep) ·
Mobile: Flutter (Dart) **ou** React Native, ainda a definir.
⚠️ SGBD, nuvem e **mobile** aguardam confirmação (ver `docs/04-arquitetura.md` §1 e
`docs/08-mobile.md` §1.1). **Não inicie o app mobile antes de a tecnologia ser confirmada.**

## Estrutura do repositório

```
docs/  database/  src/{Aurora.Domain,Aurora.Application,Aurora.Infrastructure,Aurora.Api,Aurora.Web}
mobile/aurora_mobile (fora da solução .NET)  tests/  infra/  docker/  .github/workflows/
```

Dependências: Domain ← Application ← Infrastructure; Api compõe tudo. Web e Mobile
**só falam com a API por HTTP**, nunca com o banco.

## Regras de negócio que não podem quebrar

- **RN01** Pedido só vira OP depois de confirmado.
- **RN02** Se houver produto acabado disponível, reserva em vez de gerar OP.
- **RN03/RN04** A baixa de matéria-prima ocorre ao **iniciar** a etapa que a consome, conforme a ficha técnica; sem saldo suficiente a etapa **não inicia** (erro 51001).
- **RN05** Uma etapa só inicia depois que a anterior estiver concluída (51002).
- **RN06** Inspeção reprovada reabre as etapas a partir da estação de retrabalho.
- **RN07** Pedido só é expedido quando **Pronto** (51004).
- **RN08** Estoque no mínimo ou abaixo gera alerta.
- `MovimentoEstoque` é **imutável**: correções são novos movimentos de ajuste.
- Operações que mexem em estoque e produção rodam em **procedures** (atomicidade); EF Core cuida do CRUD simples.

## Convenções

- Domínio em **português** (`Pedido`, `OrdemProducao`); sufixos técnicos em inglês (`Service`, `Repository`, `Controller`).
- Controllers finos; regra em Application/Domain/procedures; sem `DbContext` em controllers.
- Nullable ligado; avisos como erro; `async` com sufixo `Async`.
- Nada de SQL por concatenação; sempre parâmetros.
- **Sem segredos no código ou no repositório.** Use `appsettings` sem segredo, variáveis de ambiente e Key Vault.
- Commits no padrão Conventional Commits, em português (`feat(pedidos): confirma pedido e gera OPs`).
- Pull requests pequenos, revisados por outro integrante, com CI verde.

## Comandos (após a Fase 0)

```bash
dotnet build
dotnet test
docker compose -f docker/docker-compose.yml up --build
```

## Como trabalhar

1. Siga `docs/10-plano-implementacao.md`: **uma fase por vez**.
2. Antes de implementar, leia os trechos citados no prompt da fase.
3. **Escreva e rode testes**; mostre o resultado antes de dizer que terminou.
4. Mudanças pequenas e incrementais; não refatore o que não foi pedido.
5. Ao concluir uma fase, liste as **evidências** (prints, diagramas) que o grupo deve salvar em `docs/evidencias/`.
6. Os scripts SQL dos documentos **não foram testados**: valide com os testes de integração.

## Não fazer

- Não commitar senhas, chaves, connection strings reais ou arquivos `.env`.
- Não criar acesso direto da Web/Mobile ao banco.
- Não alterar scripts de banco já aplicados; crie um novo script numerado.
- Não remover validações de regra de negócio para "fazer o teste passar".
