# Relatório de acessibilidade do painel web (fase 6)

Gerado em 02/11/2026 com `tools/acessibilidade` contra a pilha do `docker compose` (Chrome headless, Lighthouse 12,
axe-core 4). Meta: **nota ≥ 90** (docs/07 §5). O axe rodou as regras WCAG 2.1 A e AA e as boas práticas.

| Tela | Perfil | Acessibilidade | Boas práticas | SEO | Violações do axe |
|---|---|---|---|---|---|
| /Conta/Login | anônimo | 100 | 100 | 100 | 0 |
| / | Administrador | 100 | 100 | 100 | 0 |
| /Clientes | Administrador | 100 | 100 | 100 | 0 |
| /Clientes/Novo | Administrador | 100 | 100 | 100 | 0 |
| /Fornecedores | Administrador | 100 | 100 | 100 | 0 |
| /Fornecedores/Novo | Administrador | 100 | 100 | 100 | 0 |
| /MateriasPrimas | Administrador | 100 | 100 | 100 | 0 |
| /Produtos | Administrador | 100 | 100 | 100 | 0 |
| /Pedidos | Administrador | 100 | 100 | 100 | 0 |
| /Producao | Administrador | 100 | 100 | 100 | 0 |
| /Producao/Ordens | Administrador | 100 | 100 | 100 | 0 |
| /Estoque | Administrador | 100 | 100 | 100 | 0 |
| /Estoque/Produtos | Administrador | 100 | 100 | 100 | 0 |
| /Estoque/Movimentos | Administrador | 100 | 100 | 100 | 0 |
| /Estoque/Alertas | Administrador | 100 | 100 | 100 | 0 |
| /Compras | Administrador | 100 | 100 | 100 | 0 |
| /Expedicao | Administrador | 100 | 100 | 100 | 0 |
| /Expedicao/Historico | Administrador | 100 | 100 | 100 | 0 |
| /Painel | Administrador | 100 | 100 | 100 | 0 |
| /Relatorios | Administrador | 100 | 100 | 100 | 0 |
| /Auditoria | Administrador | 100 | 100 | 100 | 0 |
| /Compras | Almoxarife | 100 | 100 | 100 | 0 |
| /Compras/Novo | Almoxarife | 100 | 100 | 100 | 0 |
| /Estoque/Ajustar | Almoxarife | 100 | 100 | 100 | 0 |
| /Estoque/Alertas | Almoxarife | 100 | 100 | 100 | 0 |
| /Expedicao | Almoxarife | 100 | 100 | 100 | 0 |
| /Relatorios | Almoxarife | 100 | 100 | 100 | 0 |

## Problemas encontrados e corrigidos

| Achado | Onde | Correção |
|---|---|---|
| Contraste 4,1:1 no texto do perfil ao lado do nome (`text-white-50` sobre o marrom) | Cabeçalho, todas as telas | Texto branco, sem transparência |
| Menu do administrador (12 itens) estourava a largura: o botão **Sair** saía da tela e o axe media o contraste do botão errado | Cabeçalho em 1280 a 1440 px | O menu só se expande a partir de 1400 px (abaixo disso vira o botão de menu), links sem quebra de linha e espaçamento menor |
| Região rolável sem acesso por teclado (`scrollable-region-focusable`) | Quadro de produção | `tabindex="0"`, `role="region"` e `aria-label` |
| Sem `meta description` | Todas as telas | Adicionada ao layout |
| Sem `robots.txt` (Lighthouse acusava arquivo inválido) | Login | Arquivo criado |
| Erro no console: `<li style="display:none">` do resumo de erros violava a política de conteúdo | Login e formulários | Validação de cliente desligada nas views (os formulários são `novalidate` e não carregam o jQuery Validation) |

## Limites desta verificação

- Lighthouse e axe não substituem teste com leitor de tela: **ainda falta** percorrer os fluxos principais com NVDA
  (web) e TalkBack (app), como pedem docs/07 §5 e docs/08 §10.
- As telas dependem de dados: as páginas de detalhe, o painel com dados reais e os formulários de outros perfis
  (Vendedor, Gerente) não foram todos varridos; a lista acima é o que foi auditado.
- O app mobile só teve revisão estática (rótulos, papéis, alvo de 48 dp, tema de alto contraste); falta TalkBack e
  escala de fonte de 200% num aparelho.
