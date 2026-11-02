import type { EtapaDaEstacao } from '@/features/etapas/tipos';

/** Uma etapa de Corte liberada, com valores razoáveis; cada teste sobrescreve o que importa. */
export function etapa(sobrescrever: Partial<EtapaDaEstacao> = {}): EtapaDaEstacao {
  return {
    etapaId: 341,
    ordemProducaoId: 58,
    numeroOp: 'OP-2026-00058',
    codigoQr: 'b6d1c0f2-1d1e-4a7e-9d3b-3f7e5c2a9a10',
    pedidoNumero: 'PED-2026-00031',
    produtoId: 5,
    produtoCodigo: 'CE-100',
    produtoNome: 'Cadeira Ergonômica Presidente',
    quantidade: 40,
    previsaoConclusao: '2026-12-13',
    estacaoId: 1,
    estacaoNome: 'Corte',
    sequencia: 1,
    status: 'Pendente',
    iniciadaEm: null,
    operadorId: null,
    liberada: true,
    ...sobrescrever,
  };
}
