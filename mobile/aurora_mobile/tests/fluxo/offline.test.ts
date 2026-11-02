import type { Db } from '@/core/db/db';
import { EtapaCacheRepository } from '@/features/etapas/etapaCacheRepository';
import type { EtapasApi } from '@/features/etapas/etapasApi';
import { EtapasService } from '@/features/etapas/etapasService';
import type { EtapaDaEstacao } from '@/features/etapas/tipos';
import { FilaRepository } from '@/features/sync/filaRepository';
import { SyncService } from '@/features/sync/syncService';
import { etapa } from '../helpers/dados';
import { ServidorDeSincronizacaoFalso, erroDeRede } from '../helpers/servidorFalso';
import { criarBancoDeTeste } from '../helpers/sqliteEmMemoria';

/**
 * Roteiro offline do docs/08 §10: modo avião → iniciar → finalizar → reconectar → conferir a fila.
 * Junta o serviço de etapas, a fila e o cache em SQLite e a sincronização; só a rede é de mentira.
 */
describe('fluxo offline do operador', () => {
  let db: Db;
  let fila: FilaRepository;
  let servidor: ServidorDeSincronizacaoFalso;
  let etapasNoServidor: EtapaDaEstacao[];
  let online: boolean;
  let relogio: number;
  let etapas: EtapasService;
  let sync: SyncService;

  beforeEach(async () => {
    db = await criarBancoDeTeste();
    fila = new FilaRepository(db);
    servidor = new ServidorDeSincronizacaoFalso();
    etapasNoServidor = [etapa()];
    online = true;
    relogio = 0;

    const api = {
      minhas: async () => {
        if (!online) throw erroDeRede();
        return etapasNoServidor;
      },
    } as unknown as EtapasApi;
    etapas = new EtapasService({
      api,
      cache: new EtapaCacheRepository(db),
      fila,
      agora: () => new Date(Date.UTC(2026, 10, 2, 12, relogio++)),
      novoId: (() => {
        let n = 0;
        return () => `req-${++n}`;
      })(),
    });
    // Sem rede o servidor falso também fica fora do ar
    const enviar = servidor.sincronizar.bind(servidor);
    servidor.sincronizar = async (itens) => {
      servidor.online = online;
      return enviar(itens);
    };
    sync = new SyncService({ fila, api: servidor, aposSincronizar: () => etapas.atualizarCache(1) });
  });

  it('iniciar e finalizar sem rede ficam na fila e vão ao servidor quando a conexão volta', async () => {
    await etapas.carregarFila(1); // com rede: o aparelho aprende a etapa
    online = false; // modo avião

    const cortada = (await etapas.carregarFila(1)).etapas[0];
    await etapas.apontar('Inicio', cortada);
    const depoisDeIniciar = (await etapas.carregarFila(1)).etapas[0];
    await etapas.apontar('Fim', depoisDeIniciar);
    const semRede = await sync.sincronizar();
    const lista = await etapas.carregarFila(1);

    expect(depoisDeIniciar).toMatchObject({ status: 'EmAndamento', pendenteEnvio: true });
    expect(lista.doCache).toBe(true);
    expect(lista.etapas).toEqual([]); // a etapa concluída saiu da lista, mesmo sem servidor
    expect(semRede).toMatchObject({ interrompidaPorRede: true, aindaPendentes: 2 });
    expect(await fila.contar()).toEqual({ pendentes: 2, enviando: 0, rejeitados: 0 });

    // Reconecta: o servidor passa a conhecer a etapa como concluída
    online = true;
    etapasNoServidor = [];
    const comRede = await sync.sincronizar();

    expect(comRede).toMatchObject({ confirmados: 2, rejeitados: 0, aindaPendentes: 0, interrompidaPorRede: false });
    expect(servidor.lotes).toEqual([['req-1', 'req-2']]); // Inicio antes de Fim
    expect(servidor.totalDeEfeitos).toBe(2);
    expect(await fila.contar()).toEqual({ pendentes: 0, enviando: 0, rejeitados: 0 });
    expect((await etapas.carregarFila(1)).etapas).toEqual([]);
  });

  it('com saldo insuficiente o início é rejeitado, fica na fila com o motivo e a etapa volta a pendente', async () => {
    await etapas.carregarFila(1);
    online = false;
    await etapas.apontar('Inicio', (await etapas.carregarFila(1)).etapas[0]);
    servidor.regra = () => ({ code: 'RN04', mensagem: 'Madeira maciça: necessário 0,450 m3, disponível 0,200 m3.' });
    online = true;

    const resumo = await sync.sincronizar();

    expect(resumo).toMatchObject({ confirmados: 0, rejeitados: 1 });
    const [item] = await fila.listar();
    expect(item).toMatchObject({ estado: 'Rejeitado', codigo: 'RN04', tipo: 'Inicio' });
    expect(item.mensagem).toContain('Madeira maciça');
    // O servidor recusou: a tela volta a mostrar a etapa como pendente, não como em andamento
    expect((await etapas.carregarFila(1)).etapas[0]).toMatchObject({ status: 'Pendente', pendenteEnvio: false });
  });

  it('repetir a sincronização depois de uma resposta perdida não duplica o efeito no servidor', async () => {
    await etapas.carregarFila(1);
    await etapas.apontar('Inicio', (await etapas.carregarFila(1)).etapas[0]);
    servidor.perderRespostaDaProximaChamada = true;

    const primeira = await sync.sincronizar();
    const segunda = await sync.sincronizar();

    expect(primeira.interrompidaPorRede).toBe(true);
    expect(segunda.confirmados).toBe(1);
    expect(servidor.totalDeEfeitos).toBe(1);
  });

  it('o item corrigido pode ser reenviado com o mesmo identificador', async () => {
    await etapas.carregarFila(1);
    await etapas.apontar('Inicio', (await etapas.carregarFila(1)).etapas[0]);
    servidor.regra = () => ({ code: 'RN05', mensagem: 'Etapa anterior não concluída' });
    await sync.sincronizar();
    servidor.regra = () => undefined; // a etapa anterior foi concluída enquanto isso

    await fila.reenviar('req-1');
    const resumo = await sync.sincronizar();

    expect(resumo.confirmados).toBe(1);
    expect(servidor.efeitos.get('req-1')).toBe(1);
  });
});
