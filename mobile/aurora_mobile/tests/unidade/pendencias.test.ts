import { aplicarPendencias, etapasAbertas } from '@/features/etapas/pendencias';
import type { ApontamentoPendente } from '@/features/sync/tipos';
import { etapa } from '../helpers/dados';

function pendente(tipo: 'Inicio' | 'Fim', etapaId: number, minuto: number, estado: ApontamentoPendente['estado'] = 'Pendente'): ApontamentoPendente {
  return {
    clientRequestId: `${tipo}-${etapaId}-${minuto}`,
    tipo,
    etapaId,
    numeroOp: 'OP-2026-00058',
    estacaoNome: 'Corte',
    dataHoraCliente: new Date(Date.UTC(2026, 10, 2, 12, minuto)).toISOString(),
    tentativas: 0,
    estado,
    codigo: null,
    mensagem: null,
  };
}

describe('aplicarPendencias', () => {
  it('etapa sem apontamento pendente fica como o servidor a conhece', () => {
    const [resultado] = aplicarPendencias([etapa()], []);

    expect(resultado).toMatchObject({ status: 'Pendente', pendenteEnvio: false });
  });

  it('início pendente mostra a etapa em andamento, marcada como não enviada', () => {
    const [resultado] = aplicarPendencias([etapa()], [pendente('Inicio', 341, 5)]);

    expect(resultado).toMatchObject({ status: 'EmAndamento', pendenteEnvio: true });
    expect(resultado.iniciadaEm).toBe(new Date(Date.UTC(2026, 10, 2, 12, 5)).toISOString());
  });

  it('fim pendente mostra a etapa concluída', () => {
    const [resultado] = aplicarPendencias([etapa({ status: 'EmAndamento' })], [pendente('Fim', 341, 9)]);

    expect(resultado).toMatchObject({ status: 'Concluida', pendenteEnvio: true });
  });

  it('vale o último apontamento pela hora do aparelho, mesmo se chegar fora de ordem', () => {
    const [resultado] = aplicarPendencias([etapa()], [pendente('Fim', 341, 20), pendente('Inicio', 341, 5)]);

    expect(resultado.status).toBe('Concluida');
  });

  it('item rejeitado não altera a etapa (o servidor recusou)', () => {
    // A fila só entrega os ativos (pendentes e em envio); este teste documenta o contrato
    const ativos = [pendente('Inicio', 341, 5, 'Rejeitado')].filter((i) => i.estado !== 'Rejeitado');

    const [resultado] = aplicarPendencias([etapa()], ativos);

    expect(resultado).toMatchObject({ status: 'Pendente', pendenteEnvio: false });
  });

  it('só mexe nas etapas que têm pendência', () => {
    const resultado = aplicarPendencias([etapa({ etapaId: 1 }), etapa({ etapaId: 2 })], [pendente('Inicio', 2, 1)]);

    expect(resultado.map((e) => e.status)).toEqual(['Pendente', 'EmAndamento']);
  });
});

describe('etapasAbertas', () => {
  it('tira da lista a etapa já concluída', () => {
    const lista = aplicarPendencias(
      [etapa({ etapaId: 1 }), etapa({ etapaId: 2, status: 'EmAndamento' })],
      [pendente('Fim', 2, 3)],
    );

    expect(etapasAbertas(lista).map((e) => e.etapaId)).toEqual([1]);
  });
});
