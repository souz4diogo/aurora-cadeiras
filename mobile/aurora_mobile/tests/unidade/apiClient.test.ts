import { MemoryTokenStore, type Sessao } from '@/core/auth/tokenStore';
import { criarApiClient } from '@/core/http/apiClient';
import { adaptadorFalso, cabecalho, type Tratador } from '../helpers/adaptador';

const usuario = { id: 12, nome: 'Carlos', perfil: 'Operador' } as const;
const sessaoInicial: Sessao = { accessToken: 'token-velho', refreshToken: 'refresh-1', usuario };

async function montar(tratador: Tratador, comSessao = true) {
  const tokenStore = new MemoryTokenStore();
  if (comSessao) await tokenStore.salvar(sessaoInicial);
  const aoExpirarSessao = jest.fn();
  const { adapter, chamadas } = adaptadorFalso(tratador);
  const http = criarApiClient({ baseURL: 'http://api.teste/api/v1', tokenStore, aoExpirarSessao, adapter });
  return { http, tokenStore, aoExpirarSessao, chamadas };
}

const renovacoes = (chamadas: { url?: string }[]) => chamadas.filter((c) => c.url?.includes('/auth/refresh')).length;

describe('cliente HTTP da API', () => {
  it('anexa o token de acesso às chamadas', async () => {
    const { http, chamadas } = await montar(() => ({ status: 200, data: { ok: true } }));

    await http.get('/etapas/minhas');

    expect(cabecalho(chamadas[0], 'Authorization')).toBe('Bearer token-velho');
  });

  it('não manda Authorization quando não há sessão', async () => {
    const { http, chamadas } = await montar(() => ({ status: 200 }), false);

    await http.get('/estacoes');

    expect(cabecalho(chamadas[0], 'Authorization')).toBeUndefined();
  });

  it('ao receber 401, renova o token uma vez e repete a chamada com o token novo', async () => {
    const { http, tokenStore, chamadas } = await montar((config) => {
      if (config.url?.includes('/auth/refresh')) {
        return {
          status: 200,
          data: { accessToken: 'token-novo', expiresInSeconds: 1800, refreshToken: 'refresh-2', usuario },
        };
      }
      return cabecalho(config, 'Authorization') === 'Bearer token-novo'
        ? { status: 200, data: { dado: 1 } }
        : { status: 401 };
    });

    const resposta = await http.get('/etapas/minhas');

    expect(resposta.data).toEqual({ dado: 1 });
    expect(renovacoes(chamadas)).toBe(1);
    expect(chamadas).toHaveLength(3);
    expect(await tokenStore.carregar()).toMatchObject({ accessToken: 'token-novo', refreshToken: 'refresh-2' });
  });

  it('o refresh token antigo é o que vai na renovação (ele é rotativo)', async () => {
    const { http, chamadas } = await montar((config) => {
      if (config.url?.includes('/auth/refresh')) {
        return { status: 200, data: { accessToken: 'n', expiresInSeconds: 1800, refreshToken: 'r2', usuario } };
      }
      return cabecalho(config, 'Authorization') === 'Bearer n' ? { status: 200 } : { status: 401 };
    });

    await http.get('/x');

    const corpo = JSON.parse(String(chamadas.find((c) => c.url?.includes('/auth/refresh'))?.data));
    expect(corpo).toEqual({ refreshToken: 'refresh-1' });
  });

  it('chamadas simultâneas com 401 esperam a MESMA renovação', async () => {
    const { http, chamadas } = await montar(async (config) => {
      if (config.url?.includes('/auth/refresh')) {
        await new Promise((resolver) => setTimeout(resolver, 20));
        return { status: 200, data: { accessToken: 'token-novo', expiresInSeconds: 1800, refreshToken: 'refresh-2', usuario } };
      }
      return cabecalho(config, 'Authorization') === 'Bearer token-novo' ? { status: 200 } : { status: 401 };
    });

    const respostas = await Promise.all([http.get('/a'), http.get('/b'), http.get('/c')]);

    expect(respostas.map((r) => r.status)).toEqual([200, 200, 200]);
    expect(renovacoes(chamadas)).toBe(1);
  });

  it('se a renovação falha, encerra a sessão: limpa os tokens e avisa o app uma vez', async () => {
    const { http, tokenStore, aoExpirarSessao } = await montar((config) =>
      config.url?.includes('/auth/refresh') ? { status: 401, data: { code: 'REFRESH_INVALIDO' } } : { status: 401 },
    );

    await expect(http.get('/etapas/minhas')).rejects.toMatchObject({ response: { status: 401 } });

    expect(aoExpirarSessao).toHaveBeenCalledTimes(1);
    expect(await tokenStore.carregar()).toBeNull();
  });

  it('401 do login (senha errada) não tenta renovar nada', async () => {
    const { http, chamadas, aoExpirarSessao } = await montar(
      () => ({ status: 401, data: { code: 'CREDENCIAIS_INVALIDAS' } }),
      false,
    );

    await expect(http.post('/auth/login', { email: 'a@b.c', senha: 'x' })).rejects.toMatchObject({
      response: { status: 401 },
    });

    expect(chamadas).toHaveLength(1);
    expect(aoExpirarSessao).not.toHaveBeenCalled();
  });

  it('se a chamada repetida volta a dar 401, desiste em vez de renovar em laço', async () => {
    const { http, chamadas } = await montar((config) =>
      config.url?.includes('/auth/refresh')
        ? { status: 200, data: { accessToken: 'n', expiresInSeconds: 1800, refreshToken: 'r2', usuario } }
        : { status: 401 },
    );

    await expect(http.get('/x')).rejects.toMatchObject({ response: { status: 401 } });

    expect(renovacoes(chamadas)).toBe(1);
  });

  it('erros que não são 401 passam direto, sem renovação', async () => {
    const { http, chamadas, aoExpirarSessao } = await montar(() => ({
      status: 409,
      data: { code: 'RN04' },
    }));

    await expect(http.post('/etapas/1/iniciar')).rejects.toMatchObject({ response: { status: 409 } });

    expect(chamadas).toHaveLength(1);
    expect(aoExpirarSessao).not.toHaveBeenCalled();
  });
});
