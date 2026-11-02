import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { urlDaApiInsegura } from '@/core/config';

describe('HTTPS obrigatório fora do desenvolvimento (RNF03, docs/08 §9)', () => {
  it.each([
    ['https://api.aurora.com.br/api/v1', false, false],
    ['HTTPS://api.aurora.com.br/api/v1', false, false],
    ['http://api.aurora.com.br/api/v1', false, true],
    ['http://10.0.2.2:5011/api/v1', false, true], // o padrão de desenvolvimento não pode ir para produção
    ['ftp://api.aurora.com.br', false, true],
    ['api.aurora.com.br', false, true],
    ['', false, true],
    ['  http://x', false, true],
    ['http://10.0.2.2:5011/api/v1', true, false], // em desenvolvimento HTTP é permitido
    ['https://localhost:7026/api/v1', true, false],
  ])('%s (desenvolvimento=%s) é inseguro? %s', (url, desenvolvimento, esperado) => {
    expect(urlDaApiInsegura(url, desenvolvimento)).toBe(esperado);
  });
});

describe('configuração do aplicativo (menor privilégio)', () => {
  const app = JSON.parse(readFileSync(join(__dirname, '../../app.json'), 'utf-8')).expo;

  it('não deixa o Android copiar o banco local nem a sessão para o backup na nuvem', () => {
    expect(app.android.allowBackup).toBe(false);
  });

  it('pede só as permissões de que precisa: câmera (QR) e vibração', () => {
    expect([...app.android.permissions].sort()).toEqual(['CAMERA', 'VIBRATE']);
  });

  it('não embute chave nem segredo no app.json', () => {
    const texto = JSON.stringify(app).toLowerCase();
    for (const suspeito of ['secret', 'password', 'senha', 'apikey', 'api_key', 'token', 'bearer']) {
      expect(texto).not.toContain(suspeito);
    }
  });
});
