# DECISOES - Registro de decisões do projeto

Registre aqui toda decisão ou mudança de decisão (data, escolha, motivo, impacto).
Referências: `04-arquitetura.md` §1, `08-mobile.md` §1.1, `09-nuvem-devops.md`.

---

## 2026-10-29 - SGBD, nuvem e custo

| Item | Decisão |
|---|---|
| SGBD | **SQL Server** (confirmado). Procedures, triggers e códigos de erro 51001-51008 estão em T-SQL |
| Desenvolvimento | SQL Server Developer/Express em **container Docker** (gratuito), via `docker compose` |
| Provedor de nuvem | **Azure** como alvo (stack .NET; `09-nuvem-devops.md` e `infra/` já desenhados para ele) |
| Custo | **Zero**: nenhuma assinatura paga será criada |

**Motivo:** trocar de SGBD obrigaria reescrever as procedures e triggers; trocar para AWS
obrigaria reescrever o `09-nuvem-devops.md` e o Bicep. A Etapa 8 pede para *definir* a
infraestrutura, e o `10-plano-implementacao.md` (Fase 6) já manda **não aplicar** o Bicep em
nenhuma assinatura, apenas validar com `az bicep build`.

**Como proceder:**
- Sistema roda localmente com Docker; a arquitetura de nuvem é descrita como **projeto**.
- Evidências da Etapa 8: telas do `docker compose` e do CI no GitHub (ver `09-nuvem-devops.md` §9).
- Só criar conta na nuvem se o professor exigir o sistema rodando lá. Opções a verificar nos
  sites oficiais (as ofertas mudam): Azure for Students (crédito sem cartão, se o e-mail da
  UNIP for aceito), conta gratuita Azure e oferta gratuita do Azure SQL Database.

**SQL Server confirmado pelo aluno responsável pelo projeto em 2026-10-30** (a confirmação do professor segue pendente).

**Ainda pendente:** confirmar com os professores a nuvem e se o sistema deve ser entregue
rodando ou só documentado. Se a resposta divergir, registrar uma nova entrada abaixo.

---

## 2026-10-29 - Fase 1a (banco): interpretações e acréscimos à especificação

Os documentos continuam sendo a fonte da verdade; estas entradas cobrem o que eles deixavam em aberto
ou o que os testes de integração revelaram.

| # | Decisão | Motivo |
|---|---|---|
| B01 | Dois códigos de erro novos: **51009** (movimento de estoque imutável, 409) e **51010** (parâmetro inválido ou usuário ausente no `SESSION_CONTEXT`, 422) | O doc define 51001-51008; faltavam códigos para a imutabilidade (trigger) e para validações de entrada das procedures. A API (fase 1b) deve mapeá-los |
| B02 | Na aprovação da inspeção, além da entrada em `EstoqueProduto`, a procedure **reserva** a quantidade da OP (`Reservado += Quantidade`) | `sp_Expedir` libera `Reservado` antes de baixar; sem a reserva na aprovação o `CK Reservado <= Quantidade` falharia na expedição |
| B03 | `PrevisaoConclusao` da OP = prazo do pedido menos 2 dias (padrão em SQL) | O Strategy `ICalculoPrevisaoStrategy` da aplicação pode sobrescrever depois |
| B04 | Produto sem roteiro que gere etapa não gera OP: erro 51005 | Evita OP "presa" sem etapas |
| B05 | Seed: 14 matérias-primas (o doc diz "cerca de 12", mas lista 14); estações e linhas **sem acento** (`Expedicao`, `Escritorio`) | Colunas de código sem acento, como a convenção do doc |
| B06 | Seed **não** cria usuários nem pedidos de exemplo | Pedido exige `VendedorId` e senhas não vão ao repositório: o seeder da aplicação cria |
| B07 | `005-seguranca.sql` cria só **papéis** (`role_aurora_app`, `role_aurora_readonly`); logins/usuários com senha são criados pela infraestrutura | Sem segredos no repositório. Testado com `CREATE USER ... WITHOUT LOGIN` + `EXECUTE AS` |
| B08 | Erros **2601/2627** (duplicata de `ClientRequestId`) em `sp_IniciarEtapa`/`sp_FinalizarEtapa` são tratados como reenvio idempotente | Evita `HOLDLOCK` (key-range) que causava risco de deadlock |
| B09 | Criação da linha de `EstoqueProduto` em `sp_GerarOrdensProducao` usa `sp_getapplock` | Evita deadlock/PK duplicada entre confirmações simultâneas de produtos novos |
| B10 | Retrabalho consome matéria-prima de novo ao reiniciar as etapas reabertas | A baixa ocorre ao iniciar a etapa (RN03); o doc não prevê estorno |

**Bug encontrado pelos testes (corrigido):** com `READ_COMMITTED_SNAPSHOT`, um `SELECT ... WITH (UPDLOCK)`
filtrado pelo saldo não trava a linha que não qualifica; dois inícios simultâneos passavam pela
checagem de RN04 e o `CHECK EstoqueAtual >= 0` estourava (erro 547). A procedure agora trava por chave
(sem filtro de saldo) e só depois confere o saldo. Teste: `Inicios_simultaneos_disputando_o_mesmo_saldo_permitem_apenas_um`
(25 rodadas).

## 2026-10-29 - Fase 1b (API base e autenticação)

| # | Decisão | Motivo |
|---|---|---|
| A01 | **Seguro por padrão**: `FallbackPolicy` exige usuário autenticado em toda rota; só `[AllowAnonymous]` (login, refresh, health, Swagger quando ligado) escapa | Uma rota nova esquecida sem `[Authorize]` nasce protegida |
| A02 | Configuração (connection string, limite de login, CORS) é lida **na primeira resolução do serviço**, não no registro | Respeita qualquer fonte adicionada depois (testes, Key Vault); a leitura antecipada fez os testes usarem o banco do `appsettings.Development.json` |
| A03 | Login: e-mail inexistente, usuário inativo e senha errada devolvem o mesmo erro `CREDENCIAIS_INVALIDAS` (401); conta bloqueada devolve `CONTA_BLOQUEADA` (401) | Não revelar qual campo falhou (US01); o bloqueio de 5 falhas/15 min é regra do Domain |
| A04 | Reuso de refresh token **já revogado** revoga todas as sessões do usuário | Indício de token roubado (rotação de refresh) |
| A05 | Inativar usuário e redefinir senha revogam os refresh tokens | A sessão não pode sobreviver à perda de acesso |
| A06 | Senha provisória (16 caracteres, gerada com `RandomNumberGenerator`) só aparece na resposta de criação/redefinição | O banco guarda apenas o hash (PBKDF2 do `PasswordHasher`) |
| A07 | Segredos: `Jwt:Key` e `ConnectionStrings:Aurora` não estão no repositório. Em desenvolvimento: `dotnet user-secrets` e `appsettings.Development.json` (autenticação Windows, sem senha) | CLAUDE.md: sem segredos no código |
| A08 | Swagger só liga com `Swagger:Habilitado=true` (padrão `false`); o JSON fica anônimo quando ligado | Doc 06 §1: desligado em produção ou protegido |
| A09 | O `UsuarioId` da sessão só é definido quando há usuário autenticado; durante o login (anônimo) a auditoria de `Usuario` registra `UsuarioId` nulo | O doc pede o contexto por requisição autenticada |
| A10 | Os pacotes transitivos `System.Security.Cryptography.Pkcs` (vulnerabilidade NU1902/NU1903) são fixados na 10.0.14 em vez de suprimir o aviso | Avisos são erros no build (CLAUDE.md) |
| A11 | O token de acesso (30 min) **não é revogado** ao inativar o usuário ou trocar o perfil; vale até expirar. Só os refresh tokens são revogados (A05). Não há consulta ao banco por requisição | Trade-off do JWT sem estado. Se a janela precisar fechar, checar `Ativo` em `OnTokenValidated` (custa uma consulta por requisição) ou reduzir `Jwt:ExpiracaoEmMinutos` |
| A12 | O JWT só aceita o algoritmo `HS256` (`ValidAlgorithms`) | Torna explícita a defesa contra troca de algoritmo, além da chave simétrica |
| A13 | Cadastros: `PUT` de matéria-prima **não aceita saldo** (`EstoqueAtual`); o saldo só muda por movimento de estoque. `POST` cria com saldo 0 | `MovimentoEstoque` é o livro-razão (CLAUDE.md); o saldo inicial entra por ajuste (`sp_AjustarEstoque`, fase 2) |
| A14 | Código de produto e de matéria-prima é normalizado para maiúsculas e único; duplicado devolve 409 `CODIGO_DUPLICADO`, fornecedor inexistente devolve 422 `FORNECEDOR_INEXISTENTE` | Alinha com `UQ_*_Codigo` e com os códigos 4xx de docs/06 |
| A15 | Imagens Docker em Alpine (`aspnet:10.0-alpine`) com `icu-libs`, usuário não-root `app`, `HEALTHCHECK` via `wget`. O compose local usa `sa`; hom/prod usam o login restrito | Imagem menor e `wget` disponível para o healthcheck; o `sa` fica restrito ao ambiente local descartável |
| A16 | O runner de scripts é o projeto `src/Aurora.DbUp` (DbUp), lido de `database/` por `SCRIPTS_PATH` e configurado por `ConnectionStrings__Aurora` | Mesmo mecanismo nos testes, no Docker e no CD; scripts já aplicados são ignorados |
| A17 | CI (`ci.yml`) cobre build, testes com cobertura, pacotes vulneráveis, segredos (gitleaks) e imagens com Trivy; CodeQL em `codeql.yml`. **Ainda não executado no GitHub**; publicação no ACR e meta de cobertura ≥ 70% ficam para a fase 6 | Sem assinatura Azure (custo zero); o gate de cobertura só faz sentido com os módulos prontos |
| A18 | O refresh é **atômico**: a revogação do token é um `UPDATE ... WHERE RevogadoEm IS NULL` e só quem o executa emite o novo par. Quem perde a corrida pelo mesmo token é tratado como reuso (A04) e derruba as sessões do usuário. O par novo é gravado **antes** de reivindicar o antigo: se a gravação falhar o usuário mantém a sessão, e o perdedor encontra o par do vencedor já gravado ao revogar tudo | Antes, 8 requisições simultâneas com o mesmo refresh token tinham todas sucesso (reproduzido em teste). O custo é que um cliente legítimo que dispare duas renovações ao mesmo tempo precisa logar de novo; o app deve serializar a renovação |
| A19 | `ForwardedHeaders` (`Habilitado`, `ProxiesConfiaveis`, `RedesConfiaveis`) vem **desligado**; ligado, usa só o último salto de `X-Forwarded-For` (`ForwardLimit=1`) e só de proxies listados. Sem lista, confia no proxy imediato, o que só vale se a API for alcançável apenas pelo ingress | O rate limit do login é por IP: atrás do ingress todos teriam o IP do proxy e dividiriam um único limite; aceitar o cabeçalho de qualquer origem permitiria forjar o IP e burlar o limite |
| A20 | **Risco aceito:** o bloqueio de conta (5 falhas, 15 min; RN do Domain) permite que um terceiro mantenha uma conta travada de propósito, e o e-mail do administrador inicial é público (README). Mitigações: rate limit por IP no login (A19), e o bloqueio zera por expiração ou quando outro administrador redefine a senha. Recomendação: manter **ao menos dois administradores** | Troca consciente entre barrar força bruta e permitir negação de serviço dirigida. Alternativas se o risco crescer: bloqueio por par (e-mail, IP), CAPTCHA, alerta na auditoria |
| A35 | Matéria-prima: a **unidade de medida não muda** depois que a matéria-prima tem movimento de estoque ou consta em alguma ficha técnica (409 `UNIDADE_EM_USO`); o saldo e as quantidades da ficha ficariam com outro sentido sem nenhum registro no livro-razão. Antes do primeiro uso a unidade pode ser corrigida | Revisão de código da fase 2 |
| A36 | A página das listas tem teto (`Paginacao.PaginaMaxima`, 1 milhão): sem ele `page=2147483647` estourava o deslocamento (`(page - 1) * pageSize`) e a API respondia 500 | Revisão de código da fase 2 |
| A21 | CPF/CNPJ de clientes e CNPJ de fornecedores são validados só por **formato** (11 ou 14 dígitos, aceitando máscara, gravados sem ela); os dígitos verificadores não são conferidos | O seed usa CNPJs fictícios e a banca precisa cadastrar dados de demonstração; a unicidade (`DOCUMENTO_DUPLICADO`, 409) continua garantida |
| A22 | `PUT` da ficha técnica e do roteiro **substitui** o conjunto inteiro dentro de uma transação própria do repositório (apaga e regrava, tudo ou nada); recusas de regra acontecem antes e deixam os dados anteriores intactos | Evita ficha ou roteiro pela metade e o conflito de unicidade ao reordenar `Sequencia`; o EF com retentativa exige a transação dentro da estratégia de execução |
| A23 | Regras do roteiro: ao menos 1 estação, sem repetição, só estações que geram etapa (a Expedição fica de fora); a sequência é a posição na lista. A ficha só consome material em estações do roteiro (docs/05 §3.2) e o roteiro não pode retirar uma estação que a ficha usa (422) | A baixa de matéria-prima ocorre ao iniciar a etapa (RN03); material numa estação sem etapa nunca seria baixado. O fim do roteiro na Qualidade não é imposto: a especificação não exige |
| A24 | Pedido: o preço de cada item é congelado no `PedidoItem` (preço de venda do produto no momento em que o item entra); ao editar, o item que já estava mantém o preço e só o item novo usa o preço atual. Produto inativo só bloqueia item novo | Reprecificar o produto não pode mudar pedidos em aberto, e o vendedor que ajusta uma quantidade não deve ver os outros preços mudarem |
| A25 | Escopo do vendedor: o perfil Vendedor lista, consulta, edita e cancela apenas os próprios pedidos (`VendedorId`); o pedido de outro vendedor responde 404, não 403. Administrador, Gerente e Almoxarife enxergam todos. O perfil Cliente não usa `/pedidos` (consulta pelo `/meus-pedidos`, fase 4) | docs/06 §1 (404 para recurso fora do escopo) e matriz §3; evita revelar que o pedido existe |
| A27 | Estoque: a API só **consulta** o extrato (`GET /estoque/movimentos`, com filtros e nome do item/usuário) e **ajusta** pela `sp_AjustarEstoque` (`POST /estoque/ajustes`, só o Almoxarife, motivo obrigatório). O ajuste informa o saldo contado e a diferença vira um movimento de ajuste; ajustar para o mesmo saldo não cria movimento. `MovimentoEstoque` é mapeado só para leitura | `MovimentoEstoque` é imutável e o login da API tem DENY de escrita nele (CLAUDE.md); o alerta de estoque mínimo continua sendo gerado pelo trigger (RN08) |
| A26 | Edição só com o pedido Aberto (409 `STATUS_INVALIDO` nos demais); `PUT` substitui os itens (item do mesmo produto é atualizado, os ausentes saem). Cancelar usa `sp_CancelarPedido` (Aberto, Confirmado ou Em produção), que libera reservas e cancela OPs. Prazo de entrega não pode estar no passado, com um dia de folga por causa do fuso (servidor em UTC) | O fluxo depois de Aberto é por procedures (RN01, RN07); "atrasado" segue a view `vw_PedidosAtrasados` (Confirmado, Em produção ou Pronto com prazo vencido) |
| A28 | Painel web: **sessão em cookie** criptografado (Data Protection) que carrega os tokens da API; `HttpOnly`, `SameSite=Strict` e `Secure` quando a requisição é HTTPS (`SameAsRequest`, para o ambiente local em HTTP funcionar); expira por inatividade (`Sessao:InatividadeEmMinutos`, 30). As chaves de criptografia ficam no container: reiniciar desloga e, com mais de uma instância, será preciso compartilhá-las (Key Vault/Blob, fase 6) | docs/07 §1; o JWT nunca chega ao JavaScript |
| A29 | O `TokenHandler` renova o token **antes de expirar (30 s)** e **uma vez ao receber 401**, repetindo a requisição. Os tokens renovados ficam em `HttpContext.Items` para a mesma requisição jamais reusar o refresh antigo (a API trata reuso como roubo, A04/A18). Falha na renovação volta ao login. Duas abas renovando no mesmo instante podem forçar novo login: aceito | Mantém a regra de segurança da API sem a tela sofrer com tokens curtos |
| A30 | **Política de conteúdo restritiva** (`default-src 'self'`, `frame-ancestors 'none'`): as telas não usam `<script>` nem `style=""` embutidos; o comportamento fica em arquivos (`site.js`, `linhas-dinamicas.js`, `total-do-pedido.js`) e há teste que impede a volta de código embutido | Defesa em profundidade contra XSS |
| A31 | O painel roda em **pt-BR** (cultura fixa). Campos decimais são `type="text" inputmode="decimal"` (o `type="number"` enviaria ponto e o pt-BR leria "10.5" como 105). A imagem Alpine do painel instala `icu-data-full`; a inicialização **falha** se a cultura pt-BR não estiver disponível de verdade | Achado no teste em container: sem os dados do ICU o painel exibia "R$1,290.00" e rejeitaria "2,5" |
| A32 | O painel **não usa Areas** (o docs/07 §1 sugeria): são 5 módulos, cada um com controller, modelos e views próprios, e a pasta `Api/` com um cliente tipado por módulo. Reavaliar se o painel crescer nas fases 3 a 5 | Areas só ajudam com muitos módulos; o custo de navegação não compensa agora |
| A33 | Escopo da 2b: clientes, fornecedores, matérias-primas, produtos (com editores de roteiro e de ficha técnica) e pedidos (listar, criar, editar em Aberto, detalhar e cancelar com confirmação). **Fora**, de propósito: Usuários e Minha conta (a API não tem troca de senha), confirmar/expedir pedido e linha do tempo (fases 3 e 4), painel gerencial e telas de estoque (fases 3 e 5) | docs/10 fase 2; uma fase por vez |
| A34 | No formulário de pedido o total na tela é uma **estimativa** calculada em JavaScript com os preços atuais; o valor oficial é o que a API grava, e o item que já estava no pedido mantém o preço congelado (A24) | A tela dá retorno imediato sem duplicar a regra de preço |
| A37 | **Escopo da fase 3 na API:** confirmar pedido (`POST /pedidos/{id}/confirmar`), consulta de OPs e etapas, iniciar/finalizar etapa, perdas, saldos e alertas. Ficam para a fase 4a (docs/06 §4.5): `GET /ordens-producao/qr/{codigoQr}`, `GET /etapas/minhas` e `POST /apontamentos/sincronizar`. **Não implementado:** `POST /ordens-producao/{id}/cancelar` (cancelar OP isolada); a OP é cancelada pelo cancelamento do pedido (`sp_CancelarPedido`) | O prompt da fase 3 não lista o cancelamento de OP e a especificação não define o que acontece com o item do pedido quando só a OP é cancelada (o pedido ficaria sem como ser produzido). Se o grupo quiser, vira decisão de negócio nova |
| A38 | **Quadro de produção:** a API ganhou `GET /ordens-producao/quadro` (não consta no docs/06), que devolve as OPs Planejadas e Em andamento agrupadas pela estação em que estão agora (a primeira etapa não concluída), na ordem do layout da fábrica, com tempo na etapa e atraso. Mostra no máximo 300 OPs, as de previsão mais próxima, e avisa quando truncou. `GET /ordens-producao` aceita `estacaoId` com o mesmo significado | O quadro do docs/07 §3.5 precisa de todas as OPs abertas de uma vez; paginar uma lista por estação no painel seria mais chamadas para o mesmo resultado |
| A39 | **Perdas** (`POST /etapas/{id}/perdas`, só o Operador): somente em etapa **Em andamento** (409 `STATUS_INVALIDO`), com `tipo` por nome (`Refugo` ou `Retrabalho`, sem diferenciar maiúsculas; números são recusados), motivo obrigatório e quantidade de 1 até a quantidade da OP (422 `PERDA_ACIMA_DA_QUANTIDADE`). A perda é um registro simples (EF), sem mexer em estoque | docs/05 §3.4 não restringe o momento nem o limite; sem eles uma perda poderia ser lançada em etapa que nem começou ou com quantidade maior que a produzida. O retrabalho por inspeção reprovada (RN06) continua sendo gravado pela `sp_RegistrarInspecao` |
| A40 | **Reenvio simultâneo** do mesmo `clientRequestId`: o `003` conferia a idempotência antes de travar a etapa, então duas chamadas ao mesmo tempo faziam a segunda falhar com 409 `STATUS_INVALIDO` (reproduzido: 5 de 6 chamadas paralelas). O `007-idempotencia-concorrente.sql` refaz a conferência depois do lock em `sp_IniciarEtapa` e `sp_FinalizarEtapa`; o reenvio, sequencial ou simultâneo, termina com sucesso e sem baixar o estoque duas vezes | O app mobile reenvia apontamentos após queda de rede (docs/08); o `003` não foi alterado porque script aplicado é imutável (CLAUDE.md) |
| A41 | **Escopo por perfil na produção:** o Vendedor enxerga só as OPs (lista, detalhe e quadro) dos próprios pedidos, e a OP de outro responde 404, como em A25. O Operador abre o detalhe da OP mas não lista OPs (docs/06 §4.5). O saldo de matéria-prima (`GET /estoque/materias-primas`) inclui o Operador, o de produto acabado e os movimentos não. Por isso o `EstoqueController` declara os perfis em cada ação: o `[Authorize]` de uma ação soma ao da classe (E, não OU) | Consistência com os pedidos; um perfil a mais só cabe por ação |
| A42 | **Alertas de estoque** (RN08): a lista mostra só os **não lidos**, do mais antigo ao mais novo; marcar como lido é idempotente (o primeiro registro de quem leu e de quando permanece) e só o Almoxarife marca. O banco mantém um alerta aberto por material (índice único filtrado), e um novo nasce quando o saldo volta a ficar no mínimo ou abaixo depois da leitura | docs/06 §4.7 pede "alertas abertos"; histórico de alertas lidos fica para os relatórios da fase 5 |
| A43 | **Estoque de produto acabado** só é preenchido pela inspeção aprovada (fase 4a). Na fase 3 os testes inserem a entrada direto no livro-razão para exercitar a reserva (RN02) e a tela de saldos; em uso real, até a fase 4 todo pedido gera OP | A inspeção ainda não existe; a reserva e o cancelamento já são cobertos pelas procedures |
| A44 | **Escopo da fase 3 no painel:** quadro de produção, lista e detalhe de OP, etiqueta com QR code, saldos (matéria-prima e produto acabado), movimentos, alertas, ajuste de saldo e a confirmação do pedido (com a seção "Produção" nos detalhes do pedido). **Fora:** iniciar e finalizar etapa e registrar perda pela web (são do operador no app mobile, docs/07 §2), compras e expedição (fases 4 e 5) | docs/10 fase 3; uma fase por vez |
| A45 | **QR code da etiqueta:** o conteúdo é o `CodigoQr` (GUID) da OP, não o número, para que o app (`GET /ordens-producao/qr/{codigoQr}`, fase 4a) não dependa de numeração previsível. É gerado no servidor pelo QRCoder 1.6.0 (MIT, biblioteca gerenciada, sem `System.Drawing.Common`) como **SVG embutido no HTML** (`viewBox`, `role="img"` e texto alternativo), correção de erro nível Q. SVG escala para qualquer tamanho de etiqueta e não exige imagem em disco nem endpoint extra. O SVG nasce só de um GUID nosso, nunca de texto digitado, por isso entra no HTML sem escape | A política de conteúdo já proíbe estilo embutido, e o SVG do QRCoder usa só atributos. A geração fica no painel (apresentação) e não na Infraestrutura, ao contrário do que a tabela do README sugeria |
| A46 | O **quadro de produção não se atualiza sozinho**: é recarregado pelo navegador. O docs/07 §3.5 não pede tempo real; atualização automática (polling ou SignalR) fica como melhoria se a banca quiser | Evita complexidade (e conexões abertas) sem requisito |
| A47 | **Perfis no painel:** o menu mostra Produção (Administrador e Gerente) e Estoque (Administrador, Gerente e Almoxarife), como no docs/07 §2. O Vendedor e o Inspetor chegam às OPs pelos links do pedido (o Vendedor só às dos próprios pedidos, A41); o Almoxarife vê o pedido sem a seção de produção, porque a API não lhe dá acesso às OPs. **Ajustar saldo é só do Almoxarife**, nem o Administrador (matriz do docs/06 §3); o Gerente e o Administrador consultam | O painel espelha a matriz da API, que continua sendo a barreira real |

**Pendente da 1b:** a matriz completa de permissões (docs/06 §3) só será testada rota a rota conforme os módulos forem criados; hoje cobre `/usuarios`, `/perfis` e `/auth/*`.

**Testes de integração do banco:** `tests/Aurora.Database.Tests` (xUnit + Testcontainers SQL Server 2022 + DbUp).
Para usar um SQL Server já instalado em vez do container, defina `AURORA_TEST_SQL` com a connection string.
