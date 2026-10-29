# 02 - Gerenciamento Ágil do Projeto (Etapa 9)

**Projeto:** Sistema de automação da fábrica Aurora Cadeiras
**Metodologia:** Scrum com quadro Kanban
**Disciplina:** Gerenciamento de Projetos Ágil

> Esta etapa planeja o **desenvolvimento da solução** (o sistema da fábrica). O plano de
> trabalho do grupo para terminar o PIM está em `01-divisao-grupo.md`.
> Os itens do backlog derivam dos requisitos em `00-escopo.md` (RF = requisito funcional,
> RNF = não funcional, RN = regra de negócio).

---

## 1. Papéis e cerimônias

| Papel | Quem (no cenário do projeto) |
|---|---|
| Product Owner | Gerente da fábrica (representado pelo professor/cliente fictício) |
| Scrum Master | Aluno 4 |
| Time de desenvolvimento | Alunos 1, 2 e 3 (e o Aluno 4 em tarefas de apoio) |

| Cerimônia | Frequência | Duração |
|---|---|---|
| Sprint Planning | Início de cada sprint | até 2 h |
| Daily Scrum | Diária | 15 min |
| Sprint Review | Fim da sprint | até 1 h |
| Sprint Retrospective | Fim da sprint | até 45 min |

**Duração da sprint:** 2 semanas. **Capacidade estimada (velocity):** 28 a 30 pontos por sprint.
**Estimativa:** pontos de história pela sequência de Fibonacci (1, 2, 3, 5, 8, 13).
**Priorização:** MoSCoW (M = Must, S = Should, C = Could).

---

## 2. Product Backlog

| ID | Épico | História de usuário | Req. | Pts | Prio |
|---|---|---|---|---|---|
| US01 | Acesso | Como usuário, quero fazer login com e-mail e senha para acessar o sistema | RF01, RNF01 | 5 | M |
| US02 | Acesso | Como administrador, quero gerenciar usuários e perfis para controlar permissões | RF02, RNF02 | 5 | M |
| US03 | Cadastros | Como gerente de produção, quero cadastrar produtos para montar o catálogo | RF03 | 3 | M |
| US04 | Cadastros | Como gerente de produção, quero cadastrar a ficha técnica de cada produto para saber os materiais necessários | RF04 | 8 | M |
| US05 | Cadastros | Como almoxarife, quero cadastrar matérias-primas com estoque mínimo para controlar o abastecimento | RF05 | 3 | M |
| US06 | Cadastros | Como vendedor, quero cadastrar clientes e fornecedores para registrar pedidos e compras | RF06 | 3 | S |
| US07 | Pedidos | Como vendedor, quero registrar pedidos com itens e prazo para formalizar a venda | RF07 | 8 | M |
| US08 | Pedidos | Como vendedor, quero acompanhar o status do pedido para informar o cliente | RF08 | 5 | M |
| US09 | Produção | Como gerente, quero gerar ordens de produção a partir de pedidos confirmados | RF09, RN01, RN02 | 8 | M |
| US10 | Produção | Como gerente, quero que a OP tenha as etapas do roteiro do produto para sequenciar o trabalho | RF10, RN05 | 5 | M |
| US11 | Produção | Como operador, quero apontar início e fim de etapa lendo o QR code da OP no celular | RF11 | 13 | M |
| US12 | Produção | Como operador, quero registrar refugo e retrabalho para medir perdas | RF12 | 5 | S |
| US13 | Estoque | Como gerente, quero baixa automática de matéria-prima ao iniciar a etapa para evitar erro manual | RF13, RN03, RN04 | 8 | M |
| US14 | Estoque | Como almoxarife, quero registrar entradas, saídas e ajustes para manter o saldo correto | RF14 | 5 | M |
| US15 | Estoque | Como comprador, quero ser alertado quando o estoque atingir o mínimo para comprar a tempo | RF15, RN08 | 3 | S |
| US16 | Compras | Como comprador, quero registrar solicitações e pedidos de compra a fornecedores | RF16 | 5 | S |
| US17 | Compras | Como almoxarife, quero registrar o recebimento com entrada automática no estoque | RF17 | 5 | S |
| US18 | Qualidade | Como inspetor, quero registrar inspeções aprovando ou reprovando peças | RF18, RN06 | 5 | S |
| US19 | Expedição | Como almoxarife, quero registrar a expedição e dar baixa no produto acabado | RF19, RN07 | 5 | S |
| US20 | Relatórios | Como gerente, quero um painel com produção por estação, atrasos e estoque crítico | RF20 | 8 | S |
| US21 | Relatórios | Como gerente, quero exportar relatórios em PDF e Excel | RF21 | 5 | C |
| US22 | Mobile | Como cliente, quero consultar o status do meu pedido no app | RF22 | 5 | S |
| US23 | Mobile | Como operador, quero que meus apontamentos offline sincronizem quando a rede voltar | RF23 | 8 | S |
| US24 | Plataforma | Como desenvolvedor, quero uma API REST documentada (Swagger) consumida por web e mobile | RF24 | 5 | M |
| US25 | Plataforma | Como equipe, quero containers e pipeline CI/CD para implantar com segurança | RNF06 | 8 | M |
| US26 | Plataforma | Como administrador, quero logs e auditoria das alterações críticas | RNF10 | 5 | S |
| US27 | Plataforma | Como usuário com deficiência, quero interfaces acessíveis (WCAG 2.1 AA) | RNF08 | 5 | S |
| | | **Total** | | **156** | |

**Critérios de aceite (exemplos):**
- **US01:** login válido devolve token JWT com expiração; credencial inválida retorna erro 401 sem revelar qual campo falhou.
- **US11:** ler o QR code abre a OP; "iniciar" e "finalizar" gravam data/hora e usuário; etapa fora de ordem é bloqueada (RN05).
- **US13:** ao iniciar a etapa, o saldo de cada matéria-prima diminui conforme a ficha técnica; sem saldo, a etapa não inicia (RN04).

---

## 3. Sprint Backlog e cronograma

Seis sprints de 2 semanas (12 semanas de desenvolvimento).

| Sprint | Semanas | Objetivo da sprint | Histórias | Pts |
|---|---|---|---|---|
| 1 | 1-2 | Base técnica: acesso, API e pipeline | US01, US02, US03, US05, US24, US25 | 29 |
| 2 | 3-4 | Cadastros e pedidos | US04, US06, US07, US08, US14 | 29 |
| 3 | 5-6 | Núcleo da produção e baixa de estoque | US09, US10, US12, US13, US15 | 29 |
| 4 | 7-8 | Chão de fábrica no celular, qualidade e expedição | US11, US18, US19, US22 | 28 |
| 5 | 9-10 | Compras, painel e modo offline | US16, US17, US20, US23 | 26 |
| 6 | 11-12 | Fechamento: relatórios, auditoria, acessibilidade e estabilização | US21, US26, US27 + testes e ajustes | 15 |
| | | | **Total** | **156** |

A Sprint 6 tem carga menor de propósito, reservando tempo para correção de defeitos, testes
de aceite e implantação final.

### Entregas por sprint (incrementos)

| Sprint | Incremento entregue |
|---|---|
| 1 | Login com perfis, API publicada com Swagger, cadastro de produto e matéria-prima, pipeline CI/CD rodando |
| 2 | Ficha técnica, cadastro de clientes/fornecedores, pedidos com status, movimentações de estoque |
| 3 | Geração de OP com etapas, baixa automática de estoque, alerta de estoque mínimo, refugo/retrabalho |
| 4 | App do operador com QR code, inspeção de qualidade, expedição, consulta de pedido pelo cliente |
| 5 | Compras com recebimento, painel gerencial, sincronização offline |
| 6 | Relatórios exportáveis, auditoria, acessibilidade AA, versão final em produção |

---

## 4. Quadro Kanban

**Colunas e limites de trabalho em andamento (WIP):**

| Backlog da sprint | Em desenvolvimento (WIP 4) | Em revisão (WIP 3) | Em teste (WIP 3) | Concluído |
|---|---|---|---|---|

**Exemplo: início da Sprint 1**

| Backlog da sprint | Em desenvolvimento | Em revisão | Em teste | Concluído |
|---|---|---|---|---|
| US05 Cadastro de matéria-prima | US01 Login JWT | US24 API + Swagger | | |
| US03 Cadastro de produto | US25 Pipeline CI/CD | | | |
| US02 Gestão de usuários | | | | |

**Regras do quadro:**
- Respeitar o limite de WIP: terminar antes de começar.
- Um cartão só vai para "Concluído" se atender à Definição de Pronto.
- Cartões bloqueados recebem etiqueta vermelha e são tratados na Daily.

### Definição de Pronto (DoD)
- Código revisado por outro integrante (pull request aprovado).
- Testes automatizados da história passando.
- Pipeline CI/CD verde.
- Critérios de aceite validados pelo Product Owner.
- Documentação atualizada (Swagger e/ou README).

---

## 5. Indicadores de acompanhamento

| Indicador | O que mede | Meta |
|---|---|---|
| **Velocity** | Pontos concluídos por sprint | 28 a 30 |
| **Burndown da sprint** | Pontos restantes por dia | Linha descendente sem platôs longos |
| **Burndown do release** | Pontos restantes do produto (156) | Zerar na Sprint 6 |
| **Lead time** | Do cartão criado ao concluído | ≤ 10 dias |
| **Cycle time** | Do início do desenvolvimento ao concluído | ≤ 5 dias |
| **Cumulative Flow** | Distribuição dos cartões por coluna | Sem gargalo crescente em "Em teste" |
| **Cumprimento da meta da sprint** | Sprints com o objetivo atingido | ≥ 80% |
| **Taxa de defeitos** | Defeitos encontrados após "Concluído" | ≤ 1 por sprint |
| **Cobertura de testes** | Código coberto por testes automatizados | ≥ 70% |

**Como serão acompanhados:** gráficos atualizados ao fim de cada dia (burndown) e de cada
sprint (velocity), revisados na Sprint Review e discutidos na Retrospectiva.

---

## 6. Riscos do projeto

| Risco | Probabilidade | Impacto | Resposta |
|---|---|---|---|
| História US11 (QR code) maior do que o estimado | Média | Alto | Dividir em "ler QR" e "gravar apontamento"; spike no início da Sprint 3 |
| Mudança de requisito pelo cliente | Média | Médio | Backlog refinado a cada sprint; mudanças entram pelo Product Owner |
| Falha de rede no chão de fábrica | Alta | Médio | Modo offline com fila local (US23) |
| Defasagem entre o time e a infraestrutura | Baixa | Médio | Pipeline CI/CD desde a Sprint 1 |
