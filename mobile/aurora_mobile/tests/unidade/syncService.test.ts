import { AxiosError } from 'axios';

import type { Db } from '@/core/db/db';
import { FilaRepository } from '@/features/sync/filaRepository';
import { SyncService } from '@/features/sync/syncService';
import type { NovoApontamento } from '@/features/sync/tipos';
import { criarBancoDeTeste } from '../helpers/sqliteEmMemoria';
import { ServidorDeSincronizacaoFalso } from '../helpers/servidorFalso';

let db: Db;
let fila: FilaRepository;
let servidor: ServidorDeSincronizacaoFalso;

/** Aponta no minuto `minuto` de uma hora fixa, para a ordem dos itens ser previsível. */
function apontamento(id: string, tipo: 'Inicio' | 'Fim', minuto: number, etapaId = 341): NovoApontamento {
  return {
    clientRequestId: id,
    tipo,
    etapaId,
    numeroOp: 'OP-2026-00012',
    estacaoNome: 'Corte',
    dataHoraCliente: new Date(Date.UTC(2026, 10, 2, 12, minuto)).toISOString(),
  };
}

function criarServico(extras: { tamanhoDoLote?: number; aposSincronizar?: () => Promise<void> } = {}) {
  return new SyncService({ fila, api: servidor, ...extras });
}

beforeEach(async () => {
  db = await criarBancoDeTeste();
  fila = new FilaRepository(db);
  servidor = new ServidorDeSincronizacaoFalso();
});

describe('SyncService', () => {
  it('envia a fila, remove os aplicados e zera a contagem', async () => {
    await fila.enfileirar(apontamento('a', 'Inicio', 1));
    await fila.enfileirar(apontamento('b', 'Fim', 2));

    const resumo = await criarServico().sincronizar();

    expect(resumo).toEqual({ confirmados: 2, rejeitados: 0, aindaPendentes: 0, interrompidaPorRede: false });
    expect(await fila.listar()).toEqual([]);
    expect(servidor.totalDeEfeitos).toBe(2);
  });

  it('envia em ordem da hora do aparelho, e não da ordem em que foram gravados', async () => {
    await fila.enfileirar(apontamento('tarde', 'Fim', 30));
    await fila.enfileirar(apontamento('cedo', 'Inicio', 5));

    await criarServico().sincronizar();

    expect(servidor.lotes).toEqual([['cedo', 'tarde']]);
  });

  it('divide a fila em lotes do tamanho configurado, mantendo a ordem', async () => {
    for (let i = 1; i <= 5; i++) await fila.enfileirar(apontamento(`i${i}`, 'Inicio', i, 300 + i));

    await criarServico({ tamanhoDoLote: 2 }).sincronizar();

    expect(servidor.lotes).toEqual([['i1', 'i2'], ['i3', 'i4'], ['i5']]);
  });

  it('usa lotes de 50 por padrão (docs/08 §5)', async () => {
    for (let i = 0; i < 120; i++) await fila.enfileirar(apontamento(`x${i}`, 'Inicio', i % 59, 1000 + i));

    await criarServico().sincronizar();

    expect(servidor.lotes.map((l) => l.length)).toEqual([50, 50, 20]);
  });

  it('item rejeitado fica na fila com o código e o motivo, e não impede os demais', async () => {
    servidor.regra = (item) =>
      item.clientRequestId === 'sem-saldo'
        ? { code: 'RN04', mensagem: 'Saldo insuficiente de matéria-prima.' }
        : undefined;
    await fila.enfileirar(apontamento('sem-saldo', 'Inicio', 1, 1));
    await fila.enfileirar(apontamento('ok', 'Inicio', 2, 2));

    const resumo = await criarServico().sincronizar();

    expect(resumo).toMatchObject({ confirmados: 1, rejeitados: 1, aindaPendentes: 0 });
    const restante = await fila.listar();
    expect(restante).toHaveLength(1);
    expect(restante[0]).toMatchObject({
      clientRequestId: 'sem-saldo',
      estado: 'Rejeitado',
      codigo: 'RN04',
      mensagem: 'Saldo insuficiente de matéria-prima.',
    });
  });

  it('rejeitado não é reenviado sozinho: só depois de o usuário pedir', async () => {
    let recusar = true;
    servidor.regra = () => (recusar ? { code: 'RN05', mensagem: 'Etapa anterior não concluída.' } : undefined);
    await fila.enfileirar(apontamento('a', 'Inicio', 1));
    const servico = criarServico();
    await servico.sincronizar();

    await servico.sincronizar();
    expect(servidor.lotes).toHaveLength(1);

    recusar = false;
    await fila.reenviar('a');
    const resumo = await servico.sincronizar();

    expect(resumo.confirmados).toBe(1);
    expect(servidor.lotes[1]).toEqual(['a']);
    expect(await fila.listar()).toEqual([]);
  });

  it('descartar remove só o rejeitado', async () => {
    servidor.regra = (item) => (item.clientRequestId === 'r' ? { code: 'RN05', mensagem: 'x' } : undefined);
    await fila.enfileirar(apontamento('r', 'Inicio', 1, 1));
    await fila.enfileirar(apontamento('p', 'Inicio', 2, 2));
    servidor.online = false;
    await criarServico().sincronizar();
    servidor.online = true;
    await fila.marcarRejeitado('r', 'RN05', 'x');

    await fila.descartar('r');
    await fila.descartar('p'); // pendente: não pode ser descartado por engano

    expect((await fila.listar()).map((i) => i.clientRequestId)).toEqual(['p']);
  });

  it('sem rede nada se perde: o lote volta para a fila e a rodada avisa que foi interrompida', async () => {
    servidor.online = false;
    await fila.enfileirar(apontamento('a', 'Inicio', 1));
    await fila.enfileirar(apontamento('b', 'Fim', 2));

    const resumo = await criarServico().sincronizar();

    expect(resumo).toEqual({ confirmados: 0, rejeitados: 0, aindaPendentes: 2, interrompidaPorRede: true });
    const itens = await fila.listar();
    expect(itens.map((i) => i.estado)).toEqual(['Pendente', 'Pendente']);
    expect(itens.every((i) => i.tentativas === 1)).toBe(true);
  });

  it('reconectar envia o que ficou: o fluxo offline completo', async () => {
    servidor.online = false;
    await fila.enfileirar(apontamento('inicio', 'Inicio', 1));
    await fila.enfileirar(apontamento('fim', 'Fim', 20));
    const servico = criarServico();
    await servico.sincronizar();

    servidor.online = true;
    const resumo = await servico.sincronizar();

    expect(resumo).toMatchObject({ confirmados: 2, aindaPendentes: 0, interrompidaPorRede: false });
    expect(servidor.lotes).toEqual([['inicio', 'fim']]);
    expect(await fila.contar()).toEqual({ pendentes: 0, enviando: 0, rejeitados: 0 });
  });

  it('a rede cai entre dois lotes: o primeiro fica confirmado e o segundo continua na fila', async () => {
    await fila.enfileirar(apontamento('a', 'Inicio', 1, 1));
    await fila.enfileirar(apontamento('b', 'Inicio', 2, 2));
    await fila.enfileirar(apontamento('c', 'Inicio', 3, 3));
    const original = servidor.sincronizar.bind(servidor);
    let chamadas = 0;
    servidor.sincronizar = async (itens) => {
      if (++chamadas === 2) servidor.online = false;
      return original(itens);
    };

    const resumo = await criarServico({ tamanhoDoLote: 2 }).sincronizar();

    expect(resumo).toMatchObject({ confirmados: 2, aindaPendentes: 1, interrompidaPorRede: true });
    expect((await fila.listar()).map((i) => i.clientRequestId)).toEqual(['c']);
  });

  it('resposta perdida: o servidor aplicou, o reenvio devolve JaProcessado e o efeito não duplica', async () => {
    servidor.perderRespostaDaProximaChamada = true;
    await fila.enfileirar(apontamento('a', 'Inicio', 1));
    const servico = criarServico();

    const primeira = await servico.sincronizar();
    expect(primeira.interrompidaPorRede).toBe(true);
    expect(servidor.efeitos.get('a')).toBe(1);

    const segunda = await servico.sincronizar();

    expect(segunda.confirmados).toBe(1);
    expect(servidor.efeitos.get('a')).toBe(1);
    expect(servidor.totalDeEfeitos).toBe(1);
    expect(await fila.listar()).toEqual([]);
  });

  it('erro de rede do axios (sem resposta) também é tratado como rede', async () => {
    await fila.enfileirar(apontamento('a', 'Inicio', 1));
    servidor.sincronizar = async () => {
      throw new AxiosError('Network Error', 'ERR_NETWORK');
    };

    const resumo = await criarServico().sincronizar();

    expect(resumo).toMatchObject({ interrompidaPorRede: true, aindaPendentes: 1 });
  });

  it('erro inesperado devolve o lote à fila e é repassado a quem chamou', async () => {
    await fila.enfileirar(apontamento('a', 'Inicio', 1));
    servidor.falharComErro = new Error('500');

    await expect(criarServico().sincronizar()).rejects.toThrow('500');

    expect((await fila.listar())[0]).toMatchObject({ estado: 'Pendente', tentativas: 1 });
  });

  it('duas chamadas ao mesmo tempo compartilham uma única rodada', async () => {
    await fila.enfileirar(apontamento('a', 'Inicio', 1));
    const servico = criarServico();

    const [r1, r2] = await Promise.all([servico.sincronizar(), servico.sincronizar()]);

    expect(servidor.lotes).toHaveLength(1);
    expect(r1).toBe(r2);
    expect(servico.sincronizando).toBe(false);
  });

  it('item que o servidor não respondeu continua pendente e a rodada não entra em laço', async () => {
    servidor.omitirDaResposta.add('b');
    await fila.enfileirar(apontamento('a', 'Inicio', 1, 1));
    await fila.enfileirar(apontamento('b', 'Inicio', 2, 2));

    const resumo = await criarServico().sincronizar();

    expect(resumo).toMatchObject({ confirmados: 1, aindaPendentes: 1 });
    expect(servidor.lotes).toHaveLength(1);
  });

  it('fila vazia não chama o servidor nem recarrega a tela', async () => {
    const apos = jest.fn(async () => {});

    const resumo = await criarServico({ aposSincronizar: apos }).sincronizar();

    expect(resumo.confirmados).toBe(0);
    expect(servidor.lotes).toEqual([]);
    expect(apos).not.toHaveBeenCalled();
  });

  it('recarrega o que a tela mostra depois de enviar, e uma falha nesse passo não estraga a rodada', async () => {
    await fila.enfileirar(apontamento('a', 'Inicio', 1));
    const apos = jest.fn(async () => {
      throw new Error('sem rede para recarregar');
    });

    const resumo = await criarServico({ aposSincronizar: apos }).sincronizar();

    expect(apos).toHaveBeenCalledTimes(1);
    expect(resumo.confirmados).toBe(1);
  });

  it('ao abrir o app, o que ficou "Enviando" por um fechamento no meio volta para a fila', async () => {
    await fila.enfileirar(apontamento('a', 'Inicio', 1));
    await fila.marcarEnviando(['a']);

    await fila.recuperarEnvioInterrompido();

    expect((await fila.listar())[0].estado).toBe('Pendente');
    expect((await criarServico().sincronizar()).confirmados).toBe(1);
  });
});
