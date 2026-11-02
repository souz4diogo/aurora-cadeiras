# Auditoria de acessibilidade do painel web

Roda **axe-core** (WCAG 2.1 A e AA, mais boas práticas) e **Lighthouse** (acessibilidade, boas práticas e SEO)
em uma lista de telas, logado como o usuário informado. A meta do projeto é nota **≥ 90** (docs/07 §5).

```bash
# 1. suba a pilha: docker compose --env-file docker/.env -f docker/docker-compose.yml up --build -d
# 2. instale e rode (precisa do Google Chrome; ajuste o caminho em auditar.mjs se for outro)
cd tools/acessibilidade && npm install
echo '["/", "/Clientes", "/Pedidos", "/Painel"]' > paginas.json
node auditar.mjs admin@aurora.local "$SEED_ADMIN_PASSWORD" paginas.json resultado.json
```

Use `-` no lugar do e-mail para auditar páginas anônimas (`/Conta/Login`). No Git Bash do Windows, caminhos
passados direto na linha de comando viram caminhos do Windows; por isso as páginas vêm de um arquivo JSON.
O resultado completo (notas, auditorias que falharam e violações do axe com o trecho de HTML) vai para o JSON de saída.
