# Documentação do PIM IV - Aurora Cadeiras

Índice dos documentos do projeto. Cada arquivo indica a etapa do manual que atende.

## Mapa dos documentos

| Arquivo | Conteúdo | Etapa do manual |
|---|---|---|
| `00-escopo.md` | Empresa, atores, módulos, requisitos, regras, casos de uso | 1, 2, 3 |
| `01-divisao-grupo.md` | Divisão entre os 4 integrantes, páginas por etapa, plano de trabalho | - |
| `02-backlog-agil.md` | Product Backlog, sprints, Kanban, indicadores | **9** |
| `03-checklist-manual.md` | Conformidade item a item com o manual | todas |
| `04-arquitetura.md` | Camadas, padrões, POO, diagramas, convenções, testes | **6** |
| `05-banco-de-dados.md` | DER, 25 tabelas, procedures, triggers, índices, segurança | **7** |
| `06-api-rest.md` | Endpoints, autenticação, permissões, contratos | **4** |
| `07-web-admin.md` | Painel web e área administrativa | **4** |
| `08-mobile.md` | App mobile, fluxo, sincronização offline | **5** |
| `09-nuvem-devops.md` | Nuvem, containers, CI/CD, monitoramento | **8** |
| `10-plano-implementacao.md` | Fases e prompts para o Claude Code | - |
| `11-negocio-e-viabilidade.md` | Caracterização, Canvas, SWOT, viabilidade, responsabilidade social | **1, 2, 3** |
| `12-inovacao-ia-3d-ar.md` | Extensão opcional: site público, 3D/AR, orçamento por foto (IA) | 4, 5, 8 (inovação) |
| `CLAUDE.md` | Contexto do projeto para o Claude Code (já copiado para a raiz do repositório) | - |
| `DECISOES.md` | Registro de decisões, interpretações e desvios da especificação | - |

## Estado do projeto

| Item | Situação |
|---|---|
| Escopo e requisitos | Rascunho pronto, aguardando revisão do grupo |
| Especificações técnicas (04 a 09) | Prontas para implementação |
| Implementação do sistema | **Em andamento:** fases 0, 1a (banco) e 1b (API base e autenticação) concluídas, com 114 testes passando. Próxima: 1c (cadastros básicos, Docker e CI) |
| Decisões e desvios da especificação | Registrados em `DECISOES.md` |
| Texto do PIM (capítulos, Introdução, Conclusão, Resumo, Abstract) | A fazer, **depois** da implementação |
| Documento final em Word com ABNT | A fazer, por último |
| Evidências (`docs/evidencias/`) | A coletar durante a implementação |

## Pendências do grupo

- [ ] Nomes, RAs, líder, cidade do polo e orientador
- [ ] Prazo de postagem e de entrega impressa
- [ ] Confirmar SGBD (SQL Server) e nuvem (Azure, só como projeto, custo zero) com os professores; decisão em `DECISOES.md`
- [ ] **Perguntar ao professor de mobile** se pode ser React Native em vez de Flutter (sem resposta, usar Flutter)
- [ ] Confirmar se o sistema deve ser entregue rodando ou só documentado
- [ ] Confirmar se o Chen vale como modelo lógico ou se o professor quer o relacional
- [ ] Renderizar organograma, fluxos, Canvas e cálculo de viabilidade do `11-negocio-e-viabilidade.md` e trocar as premissas pelas do grupo

## Ordem recomendada

1. Revisar e aprovar `00-escopo.md`.
2. Copiar `CLAUDE.md` para a raiz e seguir `10-plano-implementacao.md`.
3. Guardar as evidências de cada fase em `docs/evidencias/`.
4. Escrever os capítulos do PIM com as próprias palavras, usando as especificações como base (não copiar os textos).
5. Conferir tudo com `03-checklist-manual.md` e postar.
