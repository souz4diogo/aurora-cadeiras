import type { EtapaDaEstacao, EtapaNaTela } from '@/features/etapas/tipos';
import type { ApontamentoPendente } from '@/features/sync/tipos';

/**
 * Sobrepõe ao dado do servidor o que o operador já fez e ainda não foi enviado, para a tela refletir o toque na hora
 * (docs/08 §5, passo 1). Vale o último apontamento de cada etapa, pela hora do aparelho. Item rejeitado não entra:
 * o servidor recusou, então a etapa continua como o servidor a conhece.
 */
export function aplicarPendencias(
  etapas: readonly EtapaDaEstacao[],
  ativos: readonly ApontamentoPendente[],
): EtapaNaTela[] {
  const ultimoPorEtapa = new Map<number, ApontamentoPendente>();
  for (const item of [...ativos].sort((a, b) => a.dataHoraCliente.localeCompare(b.dataHoraCliente))) {
    ultimoPorEtapa.set(item.etapaId, item);
  }

  return etapas.map((etapa) => {
    const pendente = ultimoPorEtapa.get(etapa.etapaId);
    if (!pendente) return { ...etapa, pendenteEnvio: false };
    return pendente.tipo === 'Inicio'
      ? { ...etapa, status: 'EmAndamento', iniciadaEm: etapa.iniciadaEm ?? pendente.dataHoraCliente, pendenteEnvio: true }
      : { ...etapa, status: 'Concluida', pendenteEnvio: true };
  });
}

/** Etapas que ainda precisam de ação na estação: a concluída (inclusive a que só o aparelho sabe) sai da lista. */
export function etapasAbertas(etapas: readonly EtapaNaTela[]): EtapaNaTela[] {
  return etapas.filter((e) => e.status !== 'Concluida');
}
