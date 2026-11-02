import type { AxiosInstance } from 'axios';

import { CacheJsonRepository } from '@/core/db/cacheJsonRepository';
import type { Db } from '@/core/db/db';
import { apagarDadosLocais, migrar, MIGRACOES } from '@/core/db/migracoes';
import { ApiError } from '@/core/http/problem';
import { EtapaCacheRepository } from '@/features/etapas/etapaCacheRepository';
import type { EtapasApi } from '@/features/etapas/etapasApi';
import { EstoqueService } from '@/features/estoque/estoqueService';
import { InspecaoService } from '@/features/qualidade/inspecaoService';
import { PerdasService } from '@/features/perdas/perdasService';
import { PreferenciasRepository } from '@/features/preferencias/preferenciasRepository';
import { FilaRepository } from '@/features/sync/filaRepository';
import { consultarComCache } from '@/shared/consultaComCache';
import { formatarData, formatarMoeda, lerDataUtc } from '@/shared/formatar';
import { etapa } from '../helpers/dados';
import { erroDeRede } from '../helpers/servidorFalso';
import { criarBancoDeTeste } from '../helpers/sqliteEmMemoria';

let db: Db;

beforeEach(async () => {
  db = await criarBancoDeTeste();
});

describe('banco local', () => {
  it('migrar é idempotente e registra a versão', async () => {
    await migrar(db);
    await migrar(db);

    expect((await db.primeiro<{ user_version: number }>('PRAGMA user_version'))?.user_version).toBe(MIGRACOES.length);
  });

  it('cria as tabelas da especificação (docs/08 §5)', async () => {
    const tabelas = (await db.consultar<{ name: string }>("SELECT name FROM sqlite_master WHERE type = 'table'")).map((t) => t.name);

    expect(tabelas).toEqual(expect.arrayContaining(['etapa_cache', 'apontamento_pendente', 'cache_json', 'preferencia']));
  });

  it('a fila recusa tipo e estado inválidos (restrições do banco)', async () => {
    await expect(
      db.executar(
        `INSERT INTO apontamento_pendente (client_request_id, tipo, etapa_id, numero_op, estacao_nome, data_hora_cliente, estado)
         VALUES ('x', 'Pausa', 1, 'OP', 'Corte', '2026-11-02T00:00:00Z', 'Pendente')`,
      ),
    ).rejects.toThrow();
  });

  it('o mesmo clientRequestId não entra duas vezes na fila', async () => {
    const fila = new FilaRepository(db);
    const item = {
      clientRequestId: 'a',
      tipo: 'Inicio' as const,
      etapaId: 1,
      numeroOp: 'OP-1',
      estacaoNome: 'Corte',
      dataHoraCliente: '2026-11-02T12:00:00.000Z',
    };
    await fila.enfileirar(item);

    await expect(fila.enfileirar(item)).rejects.toThrow();
  });

  it('logout apaga etapas, fila e caches, mas mantém tamanho da fonte e alto contraste', async () => {
    const preferencias = new PreferenciasRepository(db);
    await new EtapaCacheRepository(db).guardar(etapa(), '2026-11-02T12:00:00.000Z');
    await new FilaRepository(db).enfileirar({
      clientRequestId: 'a',
      tipo: 'Inicio',
      etapaId: 341,
      numeroOp: 'OP-1',
      estacaoNome: 'Corte',
      dataHoraCliente: '2026-11-02T12:00:00.000Z',
    });
    await new CacheJsonRepository(db).guardar('estoque', [1, 2, 3]);
    await preferencias.definir('estacao_id', '1');
    await preferencias.definir('escala_fonte', '1.3');
    await preferencias.definir('alto_contraste', '1');

    await apagarDadosLocais(db);

    expect(await new EtapaCacheRepository(db).obter(341)).toBeNull();
    expect(await new FilaRepository(db).listar()).toEqual([]);
    expect(await new CacheJsonRepository(db).obter('estoque')).toBeNull();
    expect(await preferencias.obter('estacao_id')).toBeNull();
    expect(await preferencias.obter('escala_fonte')).toBe('1.3');
    expect(await preferencias.obter('alto_contraste')).toBe('1');
  });

  it('preferências gravam e sobrescrevem', async () => {
    const preferencias = new PreferenciasRepository(db);

    await preferencias.definir('estacao_id', '1');
    await preferencias.definir('estacao_id', '5');

    expect(await preferencias.obter('estacao_id')).toBe('5');
    await preferencias.remover('estacao_id');
    expect(await preferencias.obter('estacao_id')).toBeNull();
  });
});

describe('cache de etapas', () => {
  it('ordena em andamento, depois liberadas, depois pela previsão mais próxima', async () => {
    const cache = new EtapaCacheRepository(db);
    await cache.substituirDaEstacao(
      1,
      [
        etapa({ etapaId: 1, previsaoConclusao: '2026-12-20' }),
        etapa({ etapaId: 2, previsaoConclusao: '2026-12-10' }),
        etapa({ etapaId: 3, liberada: false, previsaoConclusao: '2026-12-01' }),
        etapa({ etapaId: 4, status: 'EmAndamento', previsaoConclusao: '2027-01-30' }),
        etapa({ etapaId: 5, previsaoConclusao: null }),
      ],
      '2026-11-02T12:00:00.000Z',
    );

    expect((await cache.listarDaEstacao(1)).map((e) => e.etapaId)).toEqual([4, 2, 1, 5, 3]);
  });

  it('guardar uma etapa avulsa substitui a anterior sem duplicar', async () => {
    const cache = new EtapaCacheRepository(db);
    await cache.guardar(etapa({ status: 'Pendente' }), '2026-11-02T12:00:00.000Z');

    await cache.guardar(etapa({ status: 'EmAndamento' }), '2026-11-02T13:00:00.000Z');

    expect((await cache.listarDaEstacao(1)).map((e) => e.status)).toEqual(['EmAndamento']);
    expect(await cache.ultimaAtualizacao(1)).toBe('2026-11-02T13:00:00.000Z');
  });

  it('estação sem cache não tem data de atualização', async () => {
    expect(await new EtapaCacheRepository(db).ultimaAtualizacao(7)).toBeNull();
  });
});

describe('consultarComCache', () => {
  it('guarda a resposta do servidor e a devolve sem marcar como cache', async () => {
    const cache = new CacheJsonRepository(db, () => new Date('2026-11-02T12:00:00Z'));

    const resultado = await consultarComCache(cache, 'k', async () => ({ a: 1 }));

    expect(resultado).toEqual({ dados: { a: 1 }, doCache: false, atualizadoEm: '2026-11-02T12:00:00.000Z' });
  });

  it('sem rede devolve o último dado conhecido e a hora dele', async () => {
    const cache = new CacheJsonRepository(db, () => new Date('2026-11-02T12:00:00Z'));
    await consultarComCache(cache, 'k', async () => ({ a: 1 }));

    const resultado = await consultarComCache(cache, 'k', async () => {
      throw erroDeRede();
    });

    expect(resultado).toEqual({ dados: { a: 1 }, doCache: true, atualizadoEm: '2026-11-02T12:00:00.000Z' });
  });

  it('sem rede e sem cache explica que falta conexão', async () => {
    const cache = new CacheJsonRepository(db);

    await expect(
      consultarComCache(cache, 'k', async () => {
        throw erroDeRede();
      }),
    ).rejects.toMatchObject({ code: 'SEM_CONEXAO' });
  });

  it('erro de regra não cai no cache: o usuário precisa saber', async () => {
    const cache = new CacheJsonRepository(db);
    await cache.guardar('k', { velho: true });

    await expect(
      consultarComCache(cache, 'k', async () => {
        throw new ApiError('SEM_PERMISSAO', 'Seu perfil não tem permissão para isso.', 403);
      }),
    ).rejects.toMatchObject({ code: 'SEM_PERMISSAO' });
  });
});

describe('perdas, inspeção e estoque', () => {
  it('perda inválida nem chega à API', async () => {
    const registrarPerda = jest.fn();
    const servico = new PerdasService({ registrarPerda } as unknown as EtapasApi);

    await expect(servico.registrar(1, { tipo: 'Refugo', quantidade: 0, motivo: 'x' })).rejects.toMatchObject({ code: 'VALIDACAO' });
    expect(registrarPerda).not.toHaveBeenCalled();
  });

  it('perda válida vai com o motivo aparado; sem rede avisa que exige conexão', async () => {
    const registrarPerda = jest.fn().mockResolvedValueOnce(undefined).mockRejectedValueOnce(erroDeRede());
    const servico = new PerdasService({ registrarPerda } as unknown as EtapasApi);

    await servico.registrar(341, { tipo: 'Retrabalho', quantidade: 2, motivo: '  costura torta ' });
    await expect(servico.registrar(341, { tipo: 'Refugo', quantidade: 1, motivo: 'x' })).rejects.toMatchObject({
      code: 'SEM_CONEXAO',
    });

    expect(registrarPerda).toHaveBeenNthCalledWith(1, 341, { tipo: 'Retrabalho', quantidade: 2, motivo: 'costura torta' });
  });

  it('inspeção reprovada exige a estação de retrabalho antes de chamar a API', async () => {
    const post = jest.fn();
    const servico = new InspecaoService({ post } as unknown as AxiosInstance, {} as EtapasApi);

    await expect(servico.registrar(58, { resultado: 'Reprovada' })).rejects.toMatchObject({ code: 'RN06' });
    expect(post).not.toHaveBeenCalled();
  });

  it('aprovada não leva estação de retrabalho; reprovada leva', async () => {
    const post = jest.fn().mockResolvedValue({ data: { id: 1 } });
    const servico = new InspecaoService({ post } as unknown as AxiosInstance, {} as EtapasApi);

    await servico.registrar(58, { resultado: 'Aprovada', estacaoRetrabalhoId: 2, observacao: '  ok ' });
    await servico.registrar(58, { resultado: 'Reprovada', estacaoRetrabalhoId: 2, observacao: '' });

    expect(post).toHaveBeenNthCalledWith(1, '/ordens-producao/58/inspecoes', {
      resultado: 'Aprovada',
      estacaoRetrabalhoId: null,
      observacao: 'ok',
    });
    expect(post).toHaveBeenNthCalledWith(2, '/ordens-producao/58/inspecoes', {
      resultado: 'Reprovada',
      estacaoRetrabalhoId: 2,
      observacao: null,
    });
  });

  it('as opções de retrabalho são só as estações do roteiro anteriores à Qualidade', async () => {
    const ordem = jest.fn().mockResolvedValue({
      etapas: [
        { id: 3, estacaoId: 5, estacaoNome: 'Qualidade', sequencia: 3, status: 'EmAndamento' },
        { id: 1, estacaoId: 1, estacaoNome: 'Corte', sequencia: 1, status: 'Concluida' },
        { id: 2, estacaoId: 2, estacaoNome: 'Montagem da Estrutura', sequencia: 2, status: 'Concluida' },
      ],
    });
    const servico = new InspecaoService({} as AxiosInstance, { ordem } as unknown as EtapasApi);

    expect(await servico.opcoesDeRetrabalho(58)).toEqual([
      { estacaoId: 1, nome: 'Corte' },
      { estacaoId: 2, nome: 'Montagem da Estrutura' },
    ]);
  });

  it('estoque sem rede mostra o último saldo conhecido', async () => {
    const saldo = { id: 9, codigo: 'MAD-01', nome: 'Madeira', unidade: 'm3', estoqueAtual: 3, estoqueMinimo: 1, critico: false };
    const get = jest.fn().mockResolvedValueOnce({ data: { items: [saldo] } }).mockRejectedValueOnce(erroDeRede());
    const servico = new EstoqueService({ get } as unknown as AxiosInstance, new CacheJsonRepository(db));

    const online = await servico.saldos();
    const offline = await servico.saldos();

    expect(online.doCache).toBe(false);
    expect(offline).toMatchObject({ doCache: true, dados: [saldo] });
  });
});

describe('formatação', () => {
  it('data sem fuso da API é UTC (acrescenta o Z), data com fuso é preservada', () => {
    expect(lerDataUtc('2026-11-02T12:00:00').toISOString()).toBe('2026-11-02T12:00:00.000Z');
    expect(lerDataUtc('2026-11-02T12:00:00Z').toISOString()).toBe('2026-11-02T12:00:00.000Z');
    expect(lerDataUtc('2026-11-02T09:00:00-03:00').toISOString()).toBe('2026-11-02T12:00:00.000Z');
  });

  it('formata data de prazo e moeda em pt-BR', () => {
    expect(formatarData('2026-12-13')).toBe('13/12/2026');
    expect(formatarData('2026-12-13T00:00:00')).toBe('13/12/2026');
    expect(formatarMoeda(1290)).toMatch(/R\$\s?1\.290,00/);
  });
});
