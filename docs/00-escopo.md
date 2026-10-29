# 00 - Escopo do Sistema

**Projeto:** PIM IV - Solução Tecnológica Corporativa Integrada para uma Fábrica de Cadeiras
**Curso:** Análise e Desenvolvimento de Sistemas - UNIP
**Status:** rascunho para revisão

> **Como usar este documento:** as seções marcadas com ⚠️ contêm premissas assumidas.
> Corte, troque ou acrescente o que for preciso. Todo o restante do PIM (banco, telas,
> arquitetura, backlog) deriva daqui.

---

## 1. Premissas assumidas (confirmar)

| # | Premissa | Alternativa |
|---|---|---|
| 1 | ⚠️ Empresa **fictícia**: "Aurora Cadeiras Indústria Moveleira Ltda" (nome é sugestão) | Qualquer outro nome |
| 2 | ⚠️ SGBD: **SQL Server** | MySQL/MariaDB, PostgreSQL |
| 3 | ⚠️ Mobile: **a definir**, Flutter (aprendido na disciplina) ou React Native; aguardando o professor (ver `08-mobile.md` §1.1) | .NET MAUI |
| 4 | ⚠️ Nuvem: **Azure** (combina com .NET) | AWS, Google Cloud |
| 5 | ⚠️ Autoria: a definir (individual ou grupo) | - |

---

## 2. A organização (resumo para a Etapa 1)

**Aurora Cadeiras** é uma fábrica de médio porte que produz três linhas de produto:

- **Linha Escritório:** cadeiras ergonômicas giratórias.
- **Linha Jantar:** cadeiras de madeira maciça.
- **Linha Estofada:** cadeiras com assento e encosto estofados.

**Processo produtivo (6 estações):**
Corte → Montagem da estrutura → Estofaria → Montagem final → Qualidade (inspeção) → Expedição.
As cinco primeiras geram etapas na ordem de produção, e nem todo produto passa por todas
(a linha Jantar não tem Estofaria). A Expedição é tratada no nível do pedido.

**Estrutura organizacional:**
Diretoria → Gerência de Produção (Corte, Estrutura, Estofaria, Montagem final), Qualidade,
Comercial (vendas e expedição), Compras e Almoxarifado, Administrativo/TI.

**Problemas atuais (situação "antes"):**
- Pedidos e estoque controlados em planilhas e papel.
- Falta de rastreio: ninguém sabe em qual estação está cada pedido.
- Desperdício e falta de matéria-prima por ausência de controle de estoque mínimo.
- Retrabalho por defeitos descobertos só no fim da linha.
- Relatórios gerenciais montados à mão.

**Oportunidades com tecnologia:** rastreio em tempo real, baixa automática de estoque,
alertas de compra, indicadores de produção e apontamento direto no chão de fábrica pelo celular.

---

## 3. Problema, objetivo e proposta de valor (Etapa 2)

- **Problema:** falta de visibilidade e controle integrado de pedidos, produção e estoque.
- **Objetivo da solução:** automatizar o fluxo pedido → produção → estoque → expedição,
  com rastreio em tempo real e relatórios gerenciais.
- **Público-alvo:** equipe interna da fábrica (gestão, produção, compras, vendas) e clientes
  que acompanham seus pedidos.
- **Proposta de valor:** reduzir desperdício e prazo de entrega com controle em tempo real.
- **Benefícios esperados:** menos retrabalho, estoque sob controle, decisões baseadas em dados.
- **Diferenciais:** apontamento de produção por QR code no celular, funcionamento básico
  offline no chão de fábrica, painel gerencial em tempo real.

---

## 4. Atores (usuários do sistema)

| Ator | Descrição | Canal principal |
|---|---|---|
| Administrador | Gerencia usuários, perfis e parâmetros | Web |
| Gerente de Produção | Planeja ordens de produção e acompanha indicadores | Web |
| Operador de Produção | Aponta início/fim de etapas e registra refugo | **Mobile** |
| Vendedor | Cadastra clientes e pedidos, acompanha status | Web e Mobile |
| Comprador / Almoxarife | Controla estoque, fornecedores e compras | Web |
| Inspetor de Qualidade | Registra inspeções e defeitos | Mobile |
| Cliente | Consulta o andamento do próprio pedido | Mobile (somente leitura) |

---

## 5. Módulos do sistema

1. **Acesso e Usuários:** login, perfis, permissões.
2. **Cadastros:** produtos, **ficha técnica (BOM)**, matérias-primas, fornecedores, clientes.
3. **Pedidos de Venda:** criação, itens, status, prazo.
4. **Produção:** ordens de produção (OP), etapas por estação, apontamentos.
5. **Estoque:** matéria-prima e produto acabado, movimentações, estoque mínimo.
6. **Compras:** solicitações e pedidos de compra a fornecedores.
7. **Qualidade:** inspeções, defeitos, refugo.
8. **Expedição:** separação e despacho de pedidos concluídos.
9. **Relatórios e Painel:** indicadores de produção, estoque e vendas.

> A **ficha técnica (BOM)** liga produto → matérias-primas e quantidades. É o núcleo que
> conecta pedidos, produção e estoque.

---

## 6. Requisitos funcionais

| ID | Requisito | Módulo | Prioridade |
|---|---|---|---|
| RF01 | O sistema deve autenticar usuários por e-mail e senha | Acesso | Alta |
| RF02 | O administrador deve gerenciar usuários e perfis de acesso | Acesso | Alta |
| RF03 | Cadastrar, editar e inativar produtos | Cadastros | Alta |
| RF04 | Cadastrar a ficha técnica de cada produto (materiais e quantidades) | Cadastros | Alta |
| RF05 | Cadastrar matérias-primas com unidade e estoque mínimo | Cadastros | Alta |
| RF06 | Cadastrar clientes e fornecedores | Cadastros | Média |
| RF07 | Registrar pedidos de venda com múltiplos itens e prazo de entrega | Pedidos | Alta |
| RF08 | Acompanhar o status do pedido (aberto, em produção, pronto, expedido) | Pedidos | Alta |
| RF09 | Gerar ordens de produção a partir de pedidos confirmados | Produção | Alta |
| RF10 | Definir as etapas de cada OP conforme o **roteiro do produto** (estações de produção) | Produção | Alta |
| RF11 | Apontar início e fim de etapa pelo app, lendo o QR code da OP | Produção | Alta |
| RF12 | Registrar refugo e retrabalho por etapa | Produção | Média |
| RF13 | Dar baixa automática de matéria-prima ao iniciar a etapa que a consome | Estoque | Alta |
| RF14 | Registrar entradas, saídas e ajustes de estoque | Estoque | Alta |
| RF15 | Alertar quando a matéria-prima atingir o estoque mínimo | Estoque | Média |
| RF16 | Registrar solicitações e pedidos de compra a fornecedores | Compras | Média |
| RF17 | Registrar recebimento de compra com entrada automática no estoque | Compras | Média |
| RF18 | Registrar inspeção de qualidade com aprovação ou reprovação | Qualidade | Média |
| RF19 | Registrar expedição e baixa de produto acabado | Expedição | Média |
| RF20 | Exibir painel com produção por estação, atrasos e estoque crítico | Relatórios | Média |
| RF21 | Gerar relatórios exportáveis (PDF/Excel) de pedidos, produção e estoque | Relatórios | Baixa |
| RF22 | Permitir que o cliente consulte o status do próprio pedido no app | Mobile | Média |
| RF23 | Sincronizar apontamentos feitos offline quando a conexão voltar | Mobile | Média |
| RF24 | Expor funcionalidades por **API REST** consumida por web e mobile | Integração | Alta |

---

## 7. Requisitos não funcionais

| ID | Categoria | Requisito |
|---|---|---|
| RNF01 | Segurança | Senhas com hash e salt; autenticação por **JWT** com expiração |
| RNF02 | Segurança | Controle de acesso por perfil (RBAC) em todas as rotas da API |
| RNF03 | Segurança | Comunicação somente por HTTPS; proteção contra injeção de SQL e XSS |
| RNF04 | Desempenho | Telas principais respondem em até 2 s com 50 usuários simultâneos |
| RNF05 | Disponibilidade | Meta de 99% de disponibilidade no horário de operação |
| RNF06 | Escalabilidade | API em containers, com escala horizontal na nuvem |
| RNF07 | Usabilidade | Interface simples, utilizável por operadores com pouca experiência digital |
| RNF08 | Acessibilidade | Seguir **WCAG 2.1 nível AA** (contraste, teclado, leitor de tela) |
| RNF09 | Manutenibilidade | Arquitetura em camadas, código modular e testável |
| RNF10 | Auditoria | Registrar quem alterou pedidos, estoque e ordens de produção |
| RNF11 | Privacidade | Tratamento de dados pessoais conforme a **LGPD** |
| RNF12 | Portabilidade | Mobile funcional em Android (iOS opcional) |

---

## 8. Regras de negócio

- **RN01:** pedido só pode virar OP depois de confirmado.
- **RN02:** se houver produto acabado em estoque, a OP não é gerada para aquela quantidade.
- **RN03:** a baixa de matéria-prima segue a ficha técnica e ocorre ao iniciar a etapa.
- **RN04:** não é possível iniciar uma etapa sem saldo suficiente de matéria-prima.
- **RN05:** a etapa seguinte só inicia após a conclusão da anterior.
- **RN06:** peça reprovada na qualidade volta para a etapa indicada como retrabalho.
- **RN07:** pedido só é expedido depois de todos os itens aprovados na qualidade.
- **RN08:** estoque abaixo do mínimo gera alerta ao comprador.

---

## 9. Casos de uso principais

| ID | Caso de uso | Ator | Requisitos |
|---|---|---|---|
| UC01 | Realizar login | Todos | RF01 |
| UC02 | Cadastrar produto e ficha técnica | Gerente de Produção | RF03, RF04 |
| UC03 | Registrar pedido de venda | Vendedor | RF07, RF08 |
| UC04 | Gerar ordem de produção | Gerente de Produção | RF09, RF10 |
| UC05 | Apontar etapa de produção (QR code) | Operador | RF11, RF12, RF13, RF23 |
| UC06 | Controlar estoque e receber alerta | Almoxarife | RF14, RF15 |
| UC07 | Registrar compra e recebimento | Comprador | RF16, RF17 |
| UC08 | Registrar inspeção de qualidade | Inspetor | RF18 |
| UC09 | Expedir pedido | Almoxarife | RF19 |
| UC10 | Consultar painel gerencial | Gerente de Produção | RF20, RF21 |
| UC11 | Acompanhar pedido pelo app | Cliente | RF22 |

---

## 10. Escopo mobile (Etapa 5)

**App do chão de fábrica (Operador e Inspetor):**
login → lista de OPs da minha estação → ler QR code → iniciar/finalizar etapa →
registrar refugo/inspeção → consulta rápida de estoque.
Funciona offline com fila local, sincronizando quando a rede voltar.

**Consulta de pedido (Vendedor e Cliente):**
login → meus pedidos → detalhe e status da produção.

---

## 11. Visão técnica proposta

| Camada | Tecnologia sugerida |
|---|---|
| Web (administrativo) | ASP.NET Core (MVC/Razor ou Blazor) |
| API | ASP.NET Core Web API (REST) + JWT |
| Mobile | Flutter (Dart) ou React Native, a definir |
| Banco de dados | SQL Server (procedures, triggers, índices) |
| Nuvem | Azure (App Service/containers, banco gerenciado, monitoramento) |
| DevOps | Docker + pipeline CI/CD (GitHub Actions) |
| Gestão ágil | Scrum com quadro Kanban |

---

## 12. Fora do escopo

- Emissão de nota fiscal e integração com a SEFAZ.
- Módulo financeiro e contábil completo.
- Integração com maquinário (IoT/CLP).
- Loja virtual para consumidor final.

---

## 13. Responsabilidade social e diversidade (Etapa 3)

- **Acessibilidade:** interfaces web e mobile seguindo WCAG 2.1 AA.
- **Inclusão no trabalho:** interface simples e ícones claros para operadores com diferentes níveis de escolaridade e digitalização.
- **Valorização afro-indígena:** linha de produtos inspirada em grafismos e técnicas afro-brasileiras e indígenas, com cadastro de fornecedores de comunidades tradicionais e remuneração justa.
- **Combate à discriminação:** política de contratação inclusiva e canal interno de denúncias (processo organizacional; fora do escopo do sistema, previsto como evolução).
- **Cidadania:** relatórios de impacto social (fornecedores comunitários, vagas inclusivas).

---

## 14. Decisões pendentes

- [ ] Confirmar SGBD, nuvem e **tecnologia do mobile** (seção 1).
- [ ] Individual ou grupo; nomes e RAs para a capa.
- [ ] Nível de implementação esperado pelo professor (só projeto ou código rodando).
- [ ] Nome definitivo da empresa fictícia.
