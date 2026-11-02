import { AxiosError } from 'axios';

import { MemoryTokenStore, type Sessao } from '@/core/auth/tokenStore';
import type { TokenResponse } from '@/core/http/apiClient';
import type { AuthApi } from '@/features/auth/authApi';
import { AuthService, type ArmazenamentoDaSessao } from '@/features/auth/authService';

const resposta: TokenResponse = {
  accessToken: 'acesso',
  expiresInSeconds: 1800,
  refreshToken: 'refresh',
  usuario: { id: 12, nome: 'Carlos', perfil: 'Operador' },
};

class ArmazenamentoFalso extends MemoryTokenStore implements ArmazenamentoDaSessao {
  persistir: boolean | null = null;

  definirPersistencia(persistir: boolean): void {
    this.persistir = persistir;
  }
}

function montar(api: Partial<AuthApi> = {}) {
  const tokenStore = new ArmazenamentoFalso();
  const limparDadosLocais = jest.fn(async () => {});
  const apiCompleta = { login: jest.fn(async () => resposta), logout: jest.fn(async () => {}), ...api } as unknown as AuthApi;
  return { servico: new AuthService({ api: apiCompleta, tokenStore, limparDadosLocais }), tokenStore, limparDadosLocais, api: apiCompleta };
}

describe('AuthService', () => {
  it('login guarda a sessão e devolve o usuário; a senha não é guardada', async () => {
    const { servico, tokenStore, api } = montar();

    const usuario = await servico.entrar('  carlos@aurora.local ', 'segredo-123', true);

    expect(usuario).toEqual(resposta.usuario);
    expect(api.login).toHaveBeenCalledWith('carlos@aurora.local', 'segredo-123');
    const guardada = (await tokenStore.carregar()) as Sessao;
    expect(guardada).toEqual({ accessToken: 'acesso', refreshToken: 'refresh', usuario: resposta.usuario });
    expect(JSON.stringify(guardada)).not.toContain('segredo-123');
  });

  it.each([true, false])('repassa a escolha "manter conectado" (%s) ao armazenamento', async (manter) => {
    const { servico, tokenStore } = montar();

    await servico.entrar('a@b.c', 'x', manter);

    expect(tokenStore.persistir).toBe(manter);
  });

  it('senha errada vira mensagem em português e não guarda nada', async () => {
    const erro = new AxiosError('x', 'ERR_BAD_REQUEST', undefined, null, {
      status: 401,
      statusText: '401',
      headers: {},
      config: {} as never,
      data: { code: 'CREDENCIAIS_INVALIDAS' },
    });
    const { servico, tokenStore } = montar({ login: jest.fn(async () => Promise.reject(erro)) });

    await expect(servico.entrar('a@b.c', 'errada', false)).rejects.toMatchObject({
      code: 'CREDENCIAIS_INVALIDAS',
      message: 'E-mail ou senha inválidos.',
    });
    expect(await tokenStore.carregar()).toBeNull();
  });

  it('avisa o aparelho do login para detectar troca de usuário', async () => {
    const aoAutenticar = jest.fn(async () => {});
    const tokenStore = new ArmazenamentoFalso();
    const api = { login: jest.fn(async () => resposta), logout: jest.fn() } as unknown as AuthApi;
    const servico = new AuthService({ api, tokenStore, limparDadosLocais: async () => {}, aoAutenticar });

    await servico.entrar('a@b.c', 'x', false);

    expect(aoAutenticar).toHaveBeenCalledWith(resposta.usuario);
  });

  it('se a limpeza do usuário anterior falha, o login falha e NENHUMA sessão fica guardada', async () => {
    const aoAutenticar = jest.fn(async () => {
      throw new Error('banco ocupado');
    });
    const tokenStore = new ArmazenamentoFalso();
    const api = { login: jest.fn(async () => resposta), logout: jest.fn() } as unknown as AuthApi;
    const servico = new AuthService({ api, tokenStore, limparDadosLocais: async () => {}, aoAutenticar });

    await expect(servico.entrar('a@b.c', 'x', true)).rejects.toBeDefined();

    expect(await tokenStore.carregar()).toBeNull();
    expect(await servico.restaurar()).toBeNull();
  });

  it('restaurar devolve o usuário da sessão guardada, ou nada', async () => {
    const { servico } = montar();
    expect(await servico.restaurar()).toBeNull();

    await servico.entrar('a@b.c', 'x', true);

    expect(await servico.restaurar()).toEqual(resposta.usuario);
  });

  it('sair revoga o refresh token, apaga a sessão e o banco local', async () => {
    const { servico, tokenStore, limparDadosLocais, api } = montar();
    await servico.entrar('a@b.c', 'x', true);

    await servico.sair();

    expect(api.logout).toHaveBeenCalledWith('refresh');
    expect(await tokenStore.carregar()).toBeNull();
    expect(limparDadosLocais).toHaveBeenCalledTimes(1);
  });

  it('sair sem rede limpa o aparelho do mesmo jeito', async () => {
    const { servico, tokenStore, limparDadosLocais } = montar({
      logout: jest.fn(async () => Promise.reject(new AxiosError('Network Error', 'ERR_NETWORK'))),
    });
    await servico.entrar('a@b.c', 'x', true);

    await expect(servico.sair()).resolves.toBeUndefined();

    expect(await tokenStore.carregar()).toBeNull();
    expect(limparDadosLocais).toHaveBeenCalledTimes(1);
  });

  it('sair sem sessão também limpa o banco local', async () => {
    const { servico, limparDadosLocais, api } = montar();

    await servico.sair();

    expect(api.logout).not.toHaveBeenCalled();
    expect(limparDadosLocais).toHaveBeenCalledTimes(1);
  });
});
