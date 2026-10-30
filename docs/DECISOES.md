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

**SQL Server confirmado pelo grupo em 2026-10-30.**

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

**Pendente da 1b:** a matriz completa de permissões (docs/06 §3) só será testada rota a rota conforme os módulos forem criados; hoje cobre `/usuarios`, `/perfis` e `/auth/*`.

**Testes de integração do banco:** `tests/Aurora.Database.Tests` (xUnit + Testcontainers SQL Server 2022 + DbUp).
Para usar um SQL Server já instalado em vez do container, defina `AURORA_TEST_SQL` com a connection string.
