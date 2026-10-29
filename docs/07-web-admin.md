# 07 - Painel Web e Área Administrativa (Etapa 4: Solução Web)

**Disciplina:** Desenvolvimento Web com .NET
**Projeto:** `Aurora.Web` (ASP.NET Core MVC + Razor)
**Relacionados:** `06-api-rest.md` (contrato), `00-escopo.md` (RF/UC), `04-arquitetura.md`

---

## 1. Visão geral

A aplicação web é o painel interno da fábrica. Ela **não acessa o banco**: toda operação
chama a API por um **HttpClient tipado**, o que demonstra a integração entre módulos.

| Item | Definição |
|---|---|
| Tecnologia | ASP.NET Core MVC, Razor, Bootstrap 5, JavaScript mínimo |
| Autenticação | O usuário faz login na web; a web obtém o JWT da API e o guarda no **cookie de autenticação** (HttpOnly, Secure, SameSite=Strict); o token é anexado nas chamadas à API |
| Autorização | Menus e botões conforme o perfil (claims); a API continua sendo a barreira real |
| Tratamento de erro | Erros `ProblemDetails` da API viram mensagens amigáveis; `409/422` mostram a regra violada |
| Sessão | Renovação automática do token; expira por inatividade |
| Estrutura | Uma *Area* por grupo de módulos (`Admin`, `Producao`, `Vendas`, `Estoque`) |

---

## 2. Mapa de navegação por perfil

| Perfil | Menu |
|---|---|
| Administrador | Painel · Usuários · Clientes · Fornecedores · Produtos · Matérias-primas · Pedidos · Produção · Estoque · Compras · Expedição · Relatórios · **Auditoria** |
| Gerente de Produção | Painel · Produtos · Pedidos · Produção · Estoque (consulta) · Relatórios |
| Vendedor | Pedidos · Clientes · Acompanhamento · Relatórios (pedidos) |
| Almoxarife | Estoque · Compras · Fornecedores · Matérias-primas · Expedição |
| Operador e Inspetor | Usam o **app mobile**; na web, apenas consulta de OP (opcional) |
| Cliente | Usa o app mobile (consulta de pedido) |

---

## 3. Telas

### 3.1 Acesso
| Tela | Conteúdo | RF |
|---|---|---|
| Login | E-mail, senha, mensagem de erro genérica, bloqueio após falhas | RF01 |
| Minha conta | Dados e troca de senha | RF01 |

### 3.2 Administração (estrutura administrativa)
| Tela | Conteúdo | RF |
|---|---|---|
| Usuários | Lista com busca; formulário (nome, e-mail, perfil, ativo); redefinir senha | RF02 |
| Auditoria | Filtros por tabela, registro, usuário, período; visualização antes/depois | RNF10 |
| Parâmetros | Textos e limites operacionais (ex.: validade da sessão) | - |

### 3.3 Cadastros
| Tela | Conteúdo | RF |
|---|---|---|
| Clientes | Lista, formulário, ativar/inativar | RF06 |
| Fornecedores | Lista, formulário, marcador "comunidade tradicional" | RF06 |
| Produtos | Lista, formulário, **editor da ficha técnica** (material × estação × quantidade) e **editor do roteiro** (estações em ordem) | RF03, RF04, RF10 |
| Matérias-primas | Lista com saldo e mínimo, formulário | RF05 |

### 3.4 Vendas
| Tela | Conteúdo | RF |
|---|---|---|
| Pedidos | Lista com filtros (status, cliente, período, atrasados) | RF08 |
| Novo pedido | Cliente, prazo, itens (produto, quantidade), total calculado | RF07 |
| Detalhe do pedido | Itens, status, **linha do tempo** da produção, botões Confirmar, Cancelar, Expedir (conforme o status e o perfil) | RF08, RF09 |

### 3.5 Produção
| Tela | Conteúdo | RF |
|---|---|---|
| Quadro de produção | **Colunas por estação**, cartões das OPs com tempo na etapa e destaque de atraso | RF20 |
| Detalhe da OP | Etapas, apontamentos, perdas, inspeções, botão **imprimir etiqueta com QR code** | RF09-RF12 |
| Etiqueta QR | Página de impressão com o QR da OP (biblioteca QRCoder), número e produto | RF11 |

### 3.6 Estoque, compras e expedição
| Tela | Conteúdo | RF |
|---|---|---|
| Saldos | Matérias-primas e produto acabado (total, reservado, disponível), indicador de crítico | RF14 |
| Movimentos | Extrato filtrável por material e período | RF14 |
| Ajuste | Formulário com motivo obrigatório | RF14 |
| Alertas | Lista de alertas abertos e marcação de lido | RF15 |
| Compras | Lista, nova solicitação, emissão, **recebimento por item** | RF16, RF17 |
| Expedição | Pedidos prontos, formulário de despacho (transportadora, rastreio) | RF19 |

### 3.7 Painel e relatórios
| Tela | Conteúdo | RF |
|---|---|---|
| Painel gerencial | Cartões (pedidos em aberto, OPs em andamento, atrasos, estoque crítico) e gráficos (produção por estação, consumo de material) | RF20 |
| Relatórios | Escolha do tipo, filtros, **exportação em PDF e Excel** | RF21 |

---

## 4. Fluxos principais (para os casos de uso)

**Pedido até a expedição**
1. Vendedor cria o pedido (Aberto) → confirma → o sistema gera as OPs.
2. Gerente acompanha no quadro de produção; operadores apontam pelo app.
3. Inspetor aprova no app → a OP conclui; o pedido muda para **Pronto**.
4. Almoxarife expede → pedido **Expedido**; cliente vê no app.

**Estoque crítico**
1. Etapa inicia e baixa material → o saldo cruza o mínimo → alerta é criado.
2. Almoxarife vê o alerta, cria a solicitação de compra, emite, recebe → entrada no estoque.

---

## 5. Acessibilidade (WCAG 2.1 AA) e usabilidade

| Requisito | Como atender |
|---|---|
| HTML semântico | `header`, `nav`, `main`, `footer`, títulos em ordem, tabelas com `th scope` |
| Formulários | `label` associado a cada campo, mensagens de erro ligadas por `aria-describedby`, foco no primeiro erro |
| Teclado | Toda a interface operável sem mouse; foco visível; link "pular para o conteúdo" |
| Contraste | Mínimo 4,5:1 para texto; informação nunca só por cor (ícone e texto acompanham) |
| Leitores de tela | `aria-live` para alertas e resultados de ações; textos alternativos |
| Ampliação | Layout responsivo; legível com zoom de 200% |
| Linguagem | `lang="pt-BR"`; textos simples e curtos |
| Verificação | Auditoria com axe e Lighthouse (**nota ≥ 90**); salvar prints como evidência |

---

## 6. Integração com a API (implementação)

- **Um cliente tipado por módulo:** `IPedidosApi`, `IProducaoApi`, `IEstoqueApi`...
- **Handler de autenticação** (`DelegatingHandler`) que adiciona o Bearer e renova o token ao receber `401`.
- **Tratamento central de erros** convertendo `ProblemDetails` em `ModelState`/mensagens.
- **ViewModels próprios** da web (não reutilizar entidades de domínio).
- **Configuração:** URL da API em `appsettings` ou variável de ambiente (`Api__BaseUrl`).

## 7. Evidências para o texto

Salvar em `docs/evidencias/`: `E04-login.png`, `E04-pedido-novo.png`, `E04-quadro-producao.png`,
`E04-painel.png`, `E04-ficha-tecnica.png`, `E04-swagger.png`, `E04-lighthouse.png`.
