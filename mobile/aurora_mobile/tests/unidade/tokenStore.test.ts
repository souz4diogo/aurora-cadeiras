import { MemoryTokenStore, TokenStoreEscolhivel, type Sessao } from '@/core/auth/tokenStore';

const sessao: Sessao = {
  accessToken: 'acesso',
  refreshToken: 'refresh',
  usuario: { id: 1, nome: 'Carlos', perfil: 'Operador' },
};

describe('TokenStoreEscolhivel', () => {
  it('"manter conectado" grava no armazenamento seguro e não na memória', async () => {
    const seguro = new MemoryTokenStore();
    const memoria = new MemoryTokenStore();
    const store = new TokenStoreEscolhivel(seguro, memoria);

    store.definirPersistencia(true);
    await store.salvar(sessao);

    expect(await seguro.carregar()).toEqual(sessao);
    expect(await memoria.carregar()).toBeNull();
  });

  it('sem "manter conectado" fica só em memória, e nada sobra no armazenamento seguro', async () => {
    const seguro = new MemoryTokenStore();
    const memoria = new MemoryTokenStore();
    const store = new TokenStoreEscolhivel(seguro, memoria);

    store.definirPersistencia(false);
    await store.salvar(sessao);

    expect(await memoria.carregar()).toEqual(sessao);
    expect(await seguro.carregar()).toBeNull();
  });

  it('o token renovado vai para o mesmo lugar em que a sessão já estava', async () => {
    const seguro = new MemoryTokenStore();
    const store = new TokenStoreEscolhivel(seguro, new MemoryTokenStore());
    await seguro.salvar(sessao); // sessão guardada por uma abertura anterior do app

    const carregada = await store.carregar();
    await store.salvar({ ...sessao, accessToken: 'novo' });

    expect(carregada).toEqual(sessao);
    expect((await seguro.carregar())?.accessToken).toBe('novo');
  });

  it('limpar apaga dos dois lugares', async () => {
    const seguro = new MemoryTokenStore();
    const memoria = new MemoryTokenStore();
    const store = new TokenStoreEscolhivel(seguro, memoria);
    await seguro.salvar(sessao);
    await memoria.salvar(sessao);

    await store.limpar();

    expect(await seguro.carregar()).toBeNull();
    expect(await memoria.carregar()).toBeNull();
  });
});
