// Auditoria de acessibilidade: axe-core (WCAG 2.1 A/AA) + Lighthouse em cada tela, logado como o perfil informado.
// Uso: node auditar.mjs <email> <senha> <arquivo-de-paginas.json> [saida.json]
import { readFileSync, writeFileSync } from 'node:fs';
import puppeteer from 'puppeteer-core';
import lighthouse from 'lighthouse';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const axeFonte = readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8');
const [, , email, senha, arquivoDePaginas, saida = 'resultado.json'] = process.argv;
const base = 'http://localhost:8081';
const paginas = JSON.parse(readFileSync(arquivoDePaginas, 'utf8'));

const navegador = await puppeteer.launch({
  executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  headless: 'new',
  args: ['--remote-debugging-port=9333', '--no-sandbox'],
});
const aba = await navegador.newPage();
await aba.setViewport({ width: 1280, height: 900 });

if (email !== '-') {
  await aba.goto(`${base}/Conta/Login`, { waitUntil: 'networkidle0' });
  await aba.type('#Email', email);
  await aba.type('#Senha', senha);
  await Promise.all([aba.waitForNavigation({ waitUntil: 'networkidle0' }), aba.click('button[type=submit]')]);
}

await aba.mouse.move(2, 2);
const resultados = [];
for (const caminho of paginas) {
  const url = base + caminho;
  await aba.goto(url, { waitUntil: 'networkidle0' });
  await aba.mouse.move(2, 2);
  if (!aba.url().startsWith(url.split('?')[0])) {
    resultados.push({ caminho, erro: `redirecionou para ${aba.url()}` });
    continue;
  }

  await aba.evaluate(axeFonte);
  const axe = await aba.evaluate(() =>
    // eslint-disable-next-line no-undef
    axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'best-practice'] } }),
  );
  const violacoes = axe.violations.map((v) => ({
    id: v.id,
    impacto: v.impact,
    ajuda: v.help,
    elementos: v.nodes.slice(0, 3).map((n) => n.html.slice(0, 160)),
    total: v.nodes.length,
  }));

  const lh = await lighthouse(
    url,
    { port: 9333, output: 'json', logLevel: 'error', onlyCategories: ['accessibility', 'best-practices', 'seo'], disableStorageReset: true },
    undefined,
  );
  const notas = Object.fromEntries(Object.entries(lh.lhr.categories).map(([k, v]) => [k, Math.round((v.score ?? 0) * 100)]));
  const falhas = Object.values(lh.lhr.audits)
    .filter((a) => a.score !== null && a.score < 1 && a.scoreDisplayMode === 'binary')
    .map((a) => a.id);
  resultados.push({ caminho, notas, falhasDoLighthouse: falhas, violacoesAxe: violacoes });
  console.log(caminho, JSON.stringify(notas), `axe:${violacoes.length}`);
}

writeFileSync(saida, JSON.stringify(resultados, null, 2));
await navegador.close();
