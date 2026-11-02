# 12 - Matriz de rastreabilidade dos requisitos

**Estado em 02/11/2026**, depois da fase 6. Liga cada requisito do `00-escopo.md` ao código e aos testes que o
sustentam. Os testes citados são arquivos de `tests/` (xUnit) e de `mobile/aurora_mobile/tests` (Jest); a última
execução completa teve **1.331 testes .NET verdes** (cobertura de linhas de 87%, medida com coverlet) mais a suíte do app.

**Legenda:** ✅ atendido e testado · 🟡 atendido em parte (veja a observação) · ⏳ projetado, mas sem verificação real.

---

## 1. Requisitos funcionais

| ID | Requisito | Situação | Onde está | Testes | Observação |
|---|---|---|---|---|---|
| RF01 | Autenticar por e-mail e senha | ✅ | API `AuthController` (login, refresh, logout); web `Conta`; app `features/auth` | `AutenticacaoTests`, `AutenticacaoWebTests`, Jest `unidade` | JWT de 30 min, refresh rotativo com detecção de reuso, bloqueio após 5 falhas |
| RF02 | Administrador gerencia usuários e perfis | ✅ | API `UsuariosController`; web `Usuarios` (lista, novo, editar, ativar, redefinir senha) | `UsuariosTests`, `UsuariosWebTests`, `PermissoesTests` | Senha provisória mostrada uma vez (A80). Tela de "Parâmetros" do docs/07 não feita |
| RF03 | Cadastrar, editar e inativar produtos | ✅ | API `ProdutosController`; web `Produtos` | `ProdutosTests`, `ProdutosWebTests` | |
| RF04 | Ficha técnica do produto | ✅ | API `FichaTecnicaController`; web `Produtos/FichaTecnica` | `FichaTecnicaTests`, `ProdutosWebTests` | |
| RF05 | Matérias-primas com unidade e estoque mínimo | ✅ | API `MateriasPrimasController`; web `MateriasPrimas` | `MateriasPrimasTests`, `MateriasPrimasWebTests` | |
| RF06 | Cadastrar clientes e fornecedores | 🟡 | API e web `Clientes` e `Fornecedores` (com marca de comunidade tradicional) | `ClientesTests`, `FornecedoresTests`, `ClientesWebTests`, `FornecedoresWebTests` | O cadastro de cliente pelo Vendedor no app, gravado primeiro no SQLite, **não foi feito** (A60); falta confirmar com o professor |
| RF07 | Pedidos de venda com vários itens e prazo | ✅ | API `PedidosController`; web `Pedidos` (novo, editar, confirmar, cancelar) | `PedidosTests`, `PedidosWebTests`, `PermissoesPedidosTests` | O vendedor só vê os próprios pedidos |
| RF08 | Acompanhar o status do pedido | ✅ | API `AcompanhamentoController` (pedido + linha do tempo); web detalhe do pedido; app `features/pedidos` | `AcompanhamentoTests`, `ExpedicaoWebTests` (linha do tempo) | Eventos vêm de `vw_AcompanhamentoPedido` |
| RF09 | Gerar OPs a partir de pedidos confirmados | ✅ | `sp_ConfirmarPedido`; API `PedidosController`/`OrdensProducaoController` | `ProducaoTests` (API), `Aurora.Database.Tests/ProducaoTests`, `ProducaoWebTests` | Uma OP por item que o estoque de produto acabado não cobre |
| RF10 | Etapas da OP pelo roteiro do produto | ✅ | `RoteiroController`; geração das etapas na confirmação | `RoteiroTests`, `ProducaoTests` | |
| RF11 | Apontar início e fim de etapa lendo o QR code | 🟡 | API `EtapasController`/`ApontamentosController`; app `features/qr` e `features/etapas`; etiqueta com QR na web | `FilaDaEstacaoTests`, `SincronizacaoTests`, Jest `fluxo` e `unidade`, `ProducaoWebTests` (etiqueta) | **O app nunca rodou em aparelho ou emulador**: a leitura da câmera não foi exercitada de verdade |
| RF12 | Registrar refugo e retrabalho por etapa | ✅ | API `POST /etapas/{id}/perdas`; app `features/perdas`; web mostra as perdas na OP | `ProducaoTests`, Jest `componentes` | Só com conexão (docs/08 §2) |
| RF13 | Baixa automática de matéria-prima ao iniciar a etapa | ✅ | `sp_IniciarEtapa` (RN03/RN04) | `EstoqueDeProducaoTests`, `Aurora.Database.Tests/ProducaoTests` | |
| RF14 | Entradas, saídas e ajustes de estoque | ✅ | `MovimentoEstoque` imutável; `sp_AjustarEstoque`; API `EstoqueController`; web `Estoque` | `EstoqueTests`, `EstoqueWebTests`, `Aurora.Database.Tests/EstoqueETriggersTests`, `PrecisaoDeQuantidadesTests` | Ajuste é sempre um novo movimento, com motivo obrigatório |
| RF15 | Alertar no estoque mínimo | 🟡 | Alertas gerados pelo banco; web `Estoque/Alertas` e cartão no painel; link "Comprar" | `EstoqueTests`, `EstoqueWebTests`, `ComprasWebTests` (alerta até o saldo reposto) | O alerta aparece nas telas; **não há e-mail nem notificação** ao comprador |
| RF16 | Solicitações e pedidos de compra | ✅ | API `PedidosCompraController`; web `Compras` (nova, emitir, cancelar) | `ComprasTests`, `PermissoesFase5Tests`, `ComprasWebTests` | Só o Almoxarife escreve; cancelar e receber têm um único vencedor |
| RF17 | Recebimento de compra com entrada no estoque | ✅ | `sp_ReceberItemCompra`; recebimento por item na web | `ComprasTests`, `PrecisaoDeQuantidadesTests`, `ComprasWebTests` | Recebimento parcial; excedente recusado |
| RF18 | Inspeção de qualidade (aprova ou reprova) | ✅ | `sp_RegistrarInspecao`; API `QualidadeController`; app `features/qualidade`; web mostra as inspeções na OP | `InspecaoTests`, `InspecoesNaOrdemWebTests` | A web só consulta; quem inspeciona é o Inspetor, no app |
| RF19 | Expedição e baixa de produto acabado | ✅ | `sp_Expedir`; API `ExpedicoesController`; web `Expedicao` | `ExpedicaoTests`, `ExpedicaoWebTests` | |
| RF20 | Painel: produção por estação, atrasos, estoque crítico | ✅ | API `PainelController` (views); web `Painel` com gráficos em SVG | `PainelTests`, `PainelWebTests` | |
| RF21 | Relatórios em PDF e Excel | ✅ | API `RelatoriosController` (5 tipos, 2 exportadores); web `Relatorios` | `RelatoriosTests`, `ExportadoresTests`, `RelatoriosWebTests` | Testado também dentro da imagem Alpine (A71) |
| RF22 | Cliente consulta o status do próprio pedido **no app** | ✅ | API `/meus-pedidos` e `/pedidos/{id}/acompanhamento`; app `features/pedidos` | `AcompanhamentoTests`, Jest | O painel web não tem tela para o perfil Cliente (por desenho) |
| RF23 | Sincronizar apontamentos offline | 🟡 | API `POST /apontamentos/sincronizar` (idempotente por `ClientRequestId`); app com fila em SQLite | `SincronizacaoTests`, Jest `fluxo` (node:sqlite) | A lógica é testada; o SQLite nativo do aparelho não foi exercitado |
| RF24 | Funcionalidades por API REST para web e mobile | ✅ | API versionada em `/api/v1`; Swagger (desligado em produção); web e app só falam HTTP | Todos os testes de integração; `DependenciasTests` | |

## 2. Requisitos não funcionais

| ID | Categoria | Situação | Como é atendido | Verificação |
|---|---|---|---|---|
| RNF01 | Segurança | ✅ | Senha com PBKDF2; JWT HS256 de 30 min; refresh rotativo | `AutenticacaoTests`, `PoliticaDeSenhaTests` |
| RNF02 | Segurança | ✅ | `[Authorize(Roles)]` em cada ação; matriz do docs/06 §3 | `CoberturaDeAutorizacaoTests` (falha se uma ação não declarar perfis) e as matrizes `Permissoes*Tests` |
| RNF03 | Segurança | ✅ | HSTS, cabeçalhos de segurança e CSP; SQL só com parâmetros; Razor codifica a saída; cookie `Secure` | `InfraestruturaTests`, `ForwardedHeadersTests`, CSP verificada em `AutenticacaoWebTests` e `ProducaoWebTests`; CodeQL e varredura de segredos no CI |
| RNF04 | Desempenho | ⏳ | Índices e views; consultas paginadas | **Não há teste de carga**: "2 s com 50 usuários" não foi medido |
| RNF05 | Disponibilidade | ⏳ | Probes `/health` e `/health/ready`, alertas e teste de disponibilidade no Bicep | Nunca aplicado a uma assinatura Azure |
| RNF06 | Escalabilidade | ⏳ | API sem estado em container; escala por requisições e CPU no Bicep; chaves da Web compartilhadas | Imagens sobem no `docker compose`; a escala na nuvem não foi exercitada |
| RNF07 | Usabilidade | ⏳ | Telas simples, alvo de 48 dp, mensagens com a ação seguinte | **Sem teste com usuários reais** |
| RNF08 | Acessibilidade | 🟡 | Web: axe e Lighthouse em 31 telas, nota 100 e zero violações (`docs/evidencias/acessibilidade-web.md`); app: rótulos, papéis, alto contraste | Faltam NVDA na web e TalkBack e fonte 200% no app |
| RNF09 | Manutenibilidade | ✅ | Domain ← Application ← Infrastructure ← Api; uma configuração do EF por entidade | `DependenciasTests` impede dependência fora das camadas; avisos tratados como erro |
| RNF10 | Auditoria | ✅ | Triggers em Pedido, OrdemProducao, MateriaPrima e Usuario; `MovimentoEstoque` imutável; `/auditoria` e tela web | `AuditoriaTests`, `AuditoriaWebTests`, `Aurora.Database.Tests/EstoqueETriggersTests` |
| RNF11 | Privacidade (LGPD) | 🟡 | Logs sem dado pessoal ou token; auditoria sem senha; papel de relatórios só com views; banco local do app apagado ao sair; telemetria sem texto de SQL | `SegurancaTests` (banco). **Faltam** política de privacidade, base legal e fluxo de exclusão ou anonimização a pedido do titular |
| RNF12 | Portabilidade | ⏳ | App em React Native (Expo), alvo Android | Só tipos, lint, bundle Android e Jest; **nunca rodou em aparelho** |

## 3. Regras de negócio

| ID | Regra | Situação | Onde é imposta | Testes |
|---|---|---|---|---|
| RN01 | Pedido só vira OP depois de confirmado | ✅ | `sp_ConfirmarPedido` (só pedido Aberto) | `PedidosTests`, `ProducaoTests`, `Aurora.Database.Tests/ProducaoTests` |
| RN02 | Produto acabado disponível reserva, sem gerar OP | ✅ | Mesma procedure; o pedido pode ir direto a Pronto | `PedidosTests`, `ProducaoWebTests` ("nenhuma ordem necessária") |
| RN03 | Baixa pela ficha técnica ao iniciar a etapa | ✅ | `sp_IniciarEtapa` | `EstoqueDeProducaoTests` |
| RN04 | Sem saldo, a etapa não inicia (erro 51001 → 409 `RN04`) | ✅ | `sp_IniciarEtapa` + tradução no repositório | `EstoqueDeProducaoTests`, `SincronizacaoTests` |
| RN05 | Etapa só inicia depois da anterior (51002 → 409) | ✅ | `sp_IniciarEtapa`; a fila marca etapa travada | `FilaDaEstacaoTests`, `ProducaoTests` |
| RN06 | Reprovada volta à estação de retrabalho (422 `RN06`) | ✅ | `sp_RegistrarInspecao` | `InspecaoTests`, `Aurora.Database.Tests/ProducaoTests` |
| RN07 | Só expede pedido Pronto (51004 → 409 `RN07`) | ✅ | `sp_Expedir` | `ExpedicaoTests`, `ExpedicaoWebTests` |
| RN08 | Estoque no mínimo ou abaixo gera alerta | 🟡 | Banco gera o alerta; painel, tela e link de compra | `EstoqueTests`, `PainelTests`; sem notificação ativa ao comprador (veja RF15) |

## 4. Casos de uso

| UC | Caso de uso | Ator | Requisitos | Situação |
|---|---|---|---|---|
| UC01 | Realizar login | Todos | RF01 | ✅ |
| UC02 | Cadastrar produto e ficha técnica | Gerente de Produção | RF03, RF04 | ✅ |
| UC03 | Registrar pedido de venda | Vendedor | RF07, RF08 | ✅ |
| UC04 | Gerar ordem de produção | Gerente de Produção | RF09, RF10 | ✅ (a geração acontece ao confirmar o pedido) |
| UC05 | Apontar etapa por QR code | Operador | RF11, RF12, RF13, RF23 | 🟡 sem teste em aparelho |
| UC06 | Controlar estoque e receber alerta | Almoxarife | RF14, RF15 | 🟡 alerta só em tela |
| UC07 | Registrar compra e recebimento | Comprador (perfil Almoxarife) | RF16, RF17 | ✅ |
| UC08 | Registrar inspeção de qualidade | Inspetor | RF18 | ✅ |
| UC09 | Expedir pedido | Almoxarife | RF19 | ✅ |
| UC10 | Consultar painel gerencial | Gerente de Produção | RF20, RF21 | ✅ |

## 5. Resumo

| Grupo | ✅ | 🟡 | ⏳ | Total |
|---|---|---|---|---|
| Requisitos funcionais | 20 | 4 | 0 | 24 |
| Requisitos não funcionais | 5 | 2 | 5 | 12 |
| Regras de negócio | 7 | 1 | 0 | 8 |

## 6. O que ainda falta para fechar as lacunas

1. **App em aparelho ou emulador** (RF11, RF23, RNF08, RNF12): rodar o fluxo do operador com TalkBack e fonte em 200%.
2. **Teste de carga** (RNF04): k6 ou similar com 50 usuários simultâneos nas telas principais.
3. **Cadastro de cliente no app** (RF06, A60): confirmar com o professor se Registrar perda e Inspeção bastam como "tela de cadastro".
4. **Notificação do alerta de estoque** (RF15, RN08): e-mail ou aviso ativo ao Almoxarife.
5. **LGPD** (RNF11): política de privacidade, base legal por dado e procedimento de exclusão ou anonimização.
6. **Nuvem** (RNF05, RNF06): aplicar o `infra/` em hom quando houver assinatura e confirmar o provedor com o professor.
7. **Teste de usabilidade** (RNF07) e **leitor de tela na web** (RNF08).
