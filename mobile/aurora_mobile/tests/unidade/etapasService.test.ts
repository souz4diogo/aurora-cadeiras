import type { Db } from '@/core/db/db';
import { ApiError } from '@/core/http/problem';
import { EtapaCacheRepository } from '@/features/etapas/etapaCacheRepository';
import type { EtapasApi } from '@/features/etapas/etapasApi';
import { EtapasService } from '@/features/etapas/etapasService';
import type { EtapaDaEstacao, OrdemProducaoDetalhe } from '@/features/etapas/tipos';
import { FilaRepository } from '@/features/sync/filaRepository';
import { etapa } from '../helpers/dados';
import { erroDeRede } from '../helpers/servidorFalso';
import { criarBancoDeTeste } from '../helpers/sqliteEmMemoria';

const AGORA = new Date('2026-11-02T12:00:00.000Z');

let db: Db;
let cache: EtapaCacheRepository;
let fila: FilaRepository;
let minhas: jest.Mock<Promise<EtapaDaEstacao[]>, [number]>;
let ordemPorQr: jest.Mock<Promise<OrdemProducaoDetalhe>, [string]>;
let contador: number;

function servico() {
  const api = { minhas, ordemPorQr } as unknown as EtapasApi;
  return new EtapasService({ api, cache, fila, agora: () => AGORA, novoId: () => `id-${++contador}` });
}

function ordemComTresEtapas(statuses: [string, string, string] = ['Concluida', 'Pendente', 'Pendente']): OrdemProducaoDetalhe {
  const estacoes = [
    { id: 1, nome: 'Corte' },
    { id: 2, nome: 'Montagem da Estrutura' },
    { id: 5, nome: 'Qualidade' },
  ];
  return {
    ordem: {
      id: 58,
      numero: 'OP-2026-00058',
      pedidoNumero: 'PED-2026-00031',
      produtoId: 5,
      produtoCodigo: 'CE-100',
      produtoNome: 'Cadeira Ergonômica Presidente',
      quantidade: 40,
      status: 'EmAndamento',
      previsaoConclusao: '2026-12-13',
    },
    codigoQr: 'qr-58',
    clienteNome: 'Móveis Lima',
    etapas: estacoes.map((e, i) => ({
      id: 300 + i,
      estacaoId: e.id,
      estacaoNome: e.nome,
      sequencia: i + 1,
      status: statuses[i] as 'Pendente',
      iniciadaEm: null,
      operadorId: null,
    })),
  };
}

beforeEach(async () => {
  db = await criarBancoDeTeste();
  cache = new EtapaCacheRepository(db);
  fila = new FilaRepository(db);
  contador = 0;
  minhas = jest.fn(async (_estacaoId: number) => [etapa()]);
  ordemPorQr = jest.fn(async (_codigo: string) => ordemComTresEtapas());
});

describe('EtapasService.carregarFila', () => {
  it('traz do servidor e guarda no cache', async () => {
    const resultado = await servico().carregarFila(1);

    expect(resultado.doCache).toBe(false);
    expect(resultado.etapas.map((e) => e.etapaId)).toEqual([341]);
    expect(resultado.atualizadoEm).toBe(AGORA.toISOString());
    expect(await cache.listarDaEstacao(1)).toHaveLength(1);
  });

  it('sem rede mostra o que estava guardado e avisa que vem do cache', async () => {
    await servico().carregarFila(1);
    minhas.mockRejectedValue(erroDeRede());

    const resultado = await servico().carregarFila(1);

    expect(resultado.doCache).toBe(true);
    expect(resultado.etapas.map((e) => e.etapaId)).toEqual([341]);
  });

  it('sem rede e sem cache devolve lista vazia, não erro', async () => {
    minhas.mockRejectedValue(erroDeRede());

    const resultado = await servico().carregarFila(1);

    expect(resultado).toMatchObject({ doCache: true, etapas: [], atualizadoEm: null });
  });

  it('erro que não é de rede (ex.: sem permissão) é repassado em português', async () => {
    minhas.mockRejectedValue(new ApiError('SEM_PERMISSAO', 'Seu perfil não tem permissão para isso.', 403));

    await expect(servico().carregarFila(1)).rejects.toMatchObject({ code: 'SEM_PERMISSAO' });
  });

  it('a lista nova do servidor substitui a antiga da estação', async () => {
    await servico().carregarFila(1);
    minhas.mockResolvedValue([etapa({ etapaId: 999 })]);

    const resultado = await servico().carregarFila(1);

    expect(resultado.etapas.map((e) => e.etapaId)).toEqual([999]);
  });

  it('não mistura estações', async () => {
    const daMontagem = etapa({ etapaId: 7, estacaoId: 2, estacaoNome: 'Montagem da Estrutura' });
    minhas.mockImplementation(async (estacaoId) => (estacaoId === 2 ? [daMontagem] : [etapa()]));

    await servico().carregarFila(1);
    await servico().carregarFila(2);

    expect((await servico().carregarFila(1)).etapas.map((e) => e.etapaId)).toEqual([341]);
    expect((await servico().carregarFila(2)).etapas.map((e) => e.etapaId)).toEqual([7]);
  });
});

describe('EtapasService.apontar', () => {
  it('grava na fila local com um UUID e a hora do aparelho, sem falar com a rede', async () => {
    const id = await servico().apontar('Inicio', etapa());

    expect(id).toBe('id-1');
    expect(minhas).not.toHaveBeenCalled();
    expect(await fila.listar()).toEqual([
      expect.objectContaining({
        clientRequestId: 'id-1',
        tipo: 'Inicio',
        etapaId: 341,
        numeroOp: 'OP-2026-00058',
        estacaoNome: 'Corte',
        dataHoraCliente: AGORA.toISOString(),
        estado: 'Pendente',
      }),
    ]);
  });

  it('cada toque gera um identificador diferente', async () => {
    const s = servico();

    const a = await s.apontar('Inicio', etapa());
    const b = await s.apontar('Fim', etapa());

    expect(a).not.toBe(b);
  });

  it('o toque aparece na hora na etapa, marcado como não enviado', async () => {
    const s = servico();
    await s.carregarFila(1);

    await s.apontar('Inicio', etapa());

    expect(await s.obterEtapa(341)).toMatchObject({ status: 'EmAndamento', pendenteEnvio: true });
  });
});

describe('EtapasService.abrirPorQr', () => {
  it('abre a etapa da estação escolhida e a guarda para a tela de detalhe', async () => {
    const etapaId = await servico().abrirPorQr('qr-58', 2);

    expect(ordemPorQr).toHaveBeenCalledWith('qr-58');
    expect(etapaId).toBe(301);
    expect(await cache.obter(301)).toMatchObject({
      numeroOp: 'OP-2026-00058',
      estacaoId: 2,
      produtoId: 5,
      quantidade: 40,
      liberada: true, // o Corte (etapa anterior) já está concluído
    });
  });

  it('marca como travada a etapa cuja anterior ainda não terminou', async () => {
    ordemPorQr.mockResolvedValue(ordemComTresEtapas(['EmAndamento', 'Pendente', 'Pendente']));

    const etapaId = await servico().abrirPorQr('qr-58', 2);

    expect((await cache.obter(etapaId))?.liberada).toBe(false);
  });

  it.each([
    ['Cancelada', /cancelada e não aceita apontamentos/],
    ['Concluida', /concluída e não aceita apontamentos/],
  ])('OP %s não abre etapa pelo QR', async (status, mensagem) => {
    ordemPorQr.mockResolvedValue({ ...ordemComTresEtapas(), ordem: { ...ordemComTresEtapas().ordem, status } });

    await expect(servico().abrirPorQr('qr-58', 2)).rejects.toMatchObject({ code: 'ORDEM_ENCERRADA', message: mensagem });
    expect(await cache.obter(301)).toBeNull();
  });

  it('OP planejada, que ainda não começou, abre normalmente', async () => {
    ordemPorQr.mockResolvedValue({ ...ordemComTresEtapas(), ordem: { ...ordemComTresEtapas().ordem, status: 'Planejada' } });

    await expect(servico().abrirPorQr('qr-58', 2)).resolves.toBe(301);
  });

  it('OP sem etapa em aberto na estação avisa com mensagem clara', async () => {
    ordemPorQr.mockResolvedValue(ordemComTresEtapas(['Concluida', 'Concluida', 'Pendente']));

    await expect(servico().abrirPorQr('qr-58', 2)).rejects.toMatchObject({
      code: 'SEM_ETAPA_NA_ESTACAO',
      message: expect.stringContaining('OP-2026-00058'),
    });
  });

  it('QR de OP inexistente devolve o erro da API em português', async () => {
    ordemPorQr.mockRejectedValue(new ApiError('ORDEM_PRODUCAO_NAO_ENCONTRADA', 'Ordem de produção não encontrada.', 404));

    await expect(servico().abrirPorQr('qr-x', 1)).rejects.toMatchObject({ code: 'ORDEM_PRODUCAO_NAO_ENCONTRADA' });
  });

  it('sem rede, avisa que ler o QR exige conexão', async () => {
    ordemPorQr.mockRejectedValue(erroDeRede());

    await expect(servico().abrirPorQr('qr-58', 1)).rejects.toMatchObject({ code: 'SEM_CONEXAO' });
  });
});
