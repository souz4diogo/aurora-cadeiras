# Aurora Cadeiras: app mobile

App do chão de fábrica e do acompanhamento de pedidos (PIM IV). **React Native (Expo SDK 58) + TypeScript**,
autorizado pelo professor de mobile. Especificação: [`docs/08-mobile.md`](../../docs/08-mobile.md). Decisões: A55 a A62
em [`docs/DECISOES.md`](../../docs/DECISOES.md).

| Perfil | O que faz no app |
|---|---|
| Operador | Escolhe a estação, vê a fila, lê o QR da OP, inicia e finaliza etapas (mesmo sem internet), registra perda, consulta estoque |
| Inspetor | Inicia a etapa de Qualidade e a conclui com a inspeção (aprova ou reprova com retrabalho) |
| Vendedor | Acompanha os pedidos que criou |
| Cliente | Acompanha o andamento dos próprios pedidos (somente leitura) |

Administrador, Gerente e Almoxarife trabalham no painel web; no app veem só um aviso.

## Como rodar

Pré-requisitos: Node 22.13 ou mais novo (o CI usa 24) e a API no ar (`dotnet run --project src/Aurora.Api`, porta 5011).

```bash
cd mobile/aurora_mobile
npm install
npx expo start --android          # emulador Android ou Expo Go
```

A URL da API vem de `EXPO_PUBLIC_API_BASE_URL` (definida em tempo de build). Sem ela, o app usa
`http://10.0.2.2:5011/api/v1`, onde `10.0.2.2` é a máquina hospedeira vista pelo emulador Android.

```bash
# aparelho físico na mesma rede (troque pelo IP da sua máquina)
EXPO_PUBLIC_API_BASE_URL=http://192.168.0.10:5011/api/v1 npx expo start
# produção: sempre HTTPS
EXPO_PUBLIC_API_BASE_URL=https://api.exemplo.com/api/v1 npx expo start
```

> HTTP simples só serve para desenvolvimento. **Fora dele o app exige HTTPS**: um build com `EXPO_PUBLIC_API_BASE_URL`
> em `http://` mostra um aviso e não conecta (A66). Não há segredo no app: a URL é a única configuração.

## Verificações

```bash
npm run typecheck   # TypeScript estrito
npm run lint        # ESLint (expo lint)
npm test            # Jest: unidade, componente e fluxo offline
npm run check       # os três de uma vez
npx expo-doctor     # confere dependências e configuração
```

Os testes rodam o SQL de verdade no SQLite embutido do Node (`node:sqlite`); só a rede é simulada. Eles cobrem a fila
e a sincronização (aplicado, rejeitado, rede caindo no meio, reenvio do mesmo `clientRequestId`), o interceptor de
renovação do token, o cache e as telas. **O que os testes não cobrem** é o aparelho de verdade: câmera, vibração,
TalkBack e o `expo-sqlite` nativo precisam do roteiro manual abaixo.

## Roteiro manual (docs/08 §10)

1. Entre como Operador, escolha a estação **Corte** e abra uma etapa liberada.
2. Ative o **modo avião**. Toque em **Iniciar etapa**: a tela diz "salvo no aparelho" e o selo no topo mostra
   *Offline · 1 apontamento a enviar*. Toque em **Finalizar etapa**.
3. Abra **Fila de sincronização**: os dois itens aparecem como *Aguardando envio*.
4. Desative o modo avião. Em instantes a fila esvazia e o selo volta a *Online*.
5. Repita com a matéria-prima sem saldo: o início é **rejeitado** na sincronização, a fila mostra o motivo (RN04) e
   oferece *Tentar de novo* e *Descartar*.
6. Ligue o TalkBack e percorra o fluxo do operador.

## Organização

```
src/
├─ app/            rotas do Expo Router (arquivos finos: só montam a tela)
├─ core/           cliente HTTP + renovação de token, sessão segura, SQLite e migrações, serviços
├─ features/       auth · estacoes · etapas · qr · sync · perdas · qualidade · estoque · pedidos · preferencias
│                  (cada uma com serviço/API, ViewModel em hook e tela)
└─ shared/         componentes acessíveis, tema, formatação, cache de leitura
tests/             unidade · componentes · fluxo (offline)
```

Padrão MVVM: a **tela** só desenha; o **ViewModel** (hook) guarda estado e chama os **serviços**; os serviços falam com
a API e com o SQLite. Telas nunca montam serviços: recebem tudo por contexto (`useServicos`), o que permite testar com
serviços de verdade sobre um banco em memória.

## Banco local (SQLite)

| Tabela | Para quê |
|---|---|
| `apontamento_pendente` | Fila de início e fim de etapa; a chave é o `client_request_id` (idempotência) |
| `etapa_cache` | Etapas da estação já carregadas, para ver e apontar sem rede |
| `cache_json` | Último saldo de estoque, pedidos, estações e fichas técnicas, com a hora da atualização |
| `preferencia` | Estação escolhida, tamanho do texto, alto contraste |

Ao **sair**, o app tenta enviar a fila e, se sobrar algo, pede confirmação antes de apagar o banco.
