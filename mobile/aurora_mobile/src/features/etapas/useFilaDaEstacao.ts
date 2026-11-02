import { useCallback, useEffect } from 'react';

import { useServicos } from '@/core/ServicosProvider';
import type { FilaDaEstacao } from '@/features/etapas/etapasService';
import { useSync } from '@/features/sync/SyncProvider';
import { useCarregamento } from '@/shared/useCarregamento';

export interface FilaComEstacao extends FilaDaEstacao {
  /** Estação escolhida no aparelho; nula se o operador ainda não escolheu. */
  estacaoId: number | null;
}

/**
 * ViewModel de "Minhas etapas": busca a fila no servidor (com cache se estiver sem rede) ao abrir e ao puxar para
 * atualizar, e relê só o aparelho quando a fila de sincronização muda (o toque aparece na hora, sem ir à rede).
 */
export function useFilaDaEstacao() {
  const { estacoes, etapas } = useServicos();
  const { contagem, ultimaRodada } = useSync();

  const carregar = useCallback(async (): Promise<FilaComEstacao> => {
    const estacaoId = await estacoes.estacaoEscolhida();
    if (estacaoId === null) return { estacaoId, etapas: [], doCache: false, atualizadoEm: null };
    return { estacaoId, ...(await etapas.carregarFila(estacaoId)) };
  }, [estacoes, etapas]);
  const estado = useCarregamento(carregar);
  const { definir } = estado;

  useEffect(() => {
    void (async () => {
      const estacaoId = await estacoes.estacaoEscolhida();
      if (estacaoId === null) return;
      const local = await etapas.lerFilaLocal(estacaoId);
      definir((anterior) => (anterior ? { ...anterior, ...local } : anterior));
    })();
  }, [estacoes, etapas, definir, contagem.pendentes, contagem.enviando, contagem.rejeitados, ultimaRodada]);

  return estado;
}
