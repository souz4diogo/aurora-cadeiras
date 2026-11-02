import { randomUUID } from 'expo-crypto';

import { ApiError, ehErroDeRede, interpretarErro } from '@/core/http/problem';
import type { EtapaCacheRepository } from '@/features/etapas/etapaCacheRepository';
import type { EtapasApi } from '@/features/etapas/etapasApi';
import { aplicarPendencias, etapasAbertas } from '@/features/etapas/pendencias';
import type { EtapaDaEstacao, EtapaNaTela, OrdemProducaoDetalhe } from '@/features/etapas/tipos';
import type { FilaRepository } from '@/features/sync/filaRepository';
import type { TipoDeApontamento } from '@/features/sync/tipos';

export interface FilaDaEstacao {
  etapas: EtapaNaTela[];
  /** Os dados vieram do cache do aparelho porque o servidor não respondeu. */
  doCache: boolean;
  /** Última vez em que o servidor atualizou a lista (ISO), se já houve alguma. */
  atualizadoEm: string | null;
}

export interface DependenciasDeEtapas {
  api: EtapasApi;
  cache: EtapaCacheRepository;
  fila: FilaRepository;
  agora?: () => Date;
  novoId?: () => string;
}

/**
 * Regras do operador no aparelho: ver a fila da estação (com cache), abrir uma etapa pelo QR e apontar início e fim.
 * Apontar sempre grava primeiro na fila local; o envio é feito pelo SyncService (docs/08 §5).
 */
export class EtapasService {
  private readonly agora: () => Date;
  private readonly novoId: () => string;

  constructor(private readonly deps: DependenciasDeEtapas) {
    this.agora = deps.agora ?? (() => new Date());
    this.novoId = deps.novoId ?? randomUUID;
  }

  /** Servidor primeiro; sem rede, o que estava guardado. Sempre sobrepõe o que ainda não foi enviado. */
  async carregarFila(estacaoId: number): Promise<FilaDaEstacao> {
    const { api, cache, fila } = this.deps;
    let doCache = false;
    try {
      const doServidor = await api.minhas(estacaoId);
      await cache.substituirDaEstacao(estacaoId, doServidor, this.agora().toISOString());
    } catch (erro) {
      if (!ehErroDeRede(erro)) throw interpretarErro(erro);
      doCache = true;
    }

    const guardadas = await cache.listarDaEstacao(estacaoId);
    const etapas = etapasAbertas(aplicarPendencias(guardadas, await fila.listarAtivos()));
    return { etapas, doCache, atualizadoEm: await cache.ultimaAtualizacao(estacaoId) };
  }

  /** A etapa como a tela mostra, com o que ainda não foi enviado; nula se o aparelho não a conhece. */
  async obterEtapa(etapaId: number): Promise<EtapaNaTela | null> {
    const etapa = await this.deps.cache.obter(etapaId);
    if (!etapa) return null;
    return aplicarPendencias([etapa], await this.deps.fila.listarAtivos())[0];
  }

  /**
   * Abre pelo QR da etiqueta a etapa da OP que pertence à estação escolhida. Exige conexão (consulta a OP).
   * Devolve o id da etapa, já guardada no cache para a tela de detalhe.
   */
  async abrirPorQr(codigoQr: string, estacaoId: number): Promise<number> {
    let ordem: OrdemProducaoDetalhe;
    try {
      ordem = await this.deps.api.ordemPorQr(codigoQr);
    } catch (erro) {
      throw interpretarErro(erro);
    }

    const etapa = this.etapaDaEstacao(ordem, estacaoId);
    await this.deps.cache.guardar(etapa, this.agora().toISOString());
    return etapa.etapaId;
  }

  /**
   * Grava o apontamento na fila (com o UUID que o torna idempotente) e devolve o id. Não fala com a rede:
   * quem chama dispara a sincronização em seguida.
   */
  async apontar(tipo: TipoDeApontamento, etapa: EtapaDaEstacao): Promise<string> {
    const clientRequestId = this.novoId();
    await this.deps.fila.enfileirar({
      clientRequestId,
      tipo,
      etapaId: etapa.etapaId,
      numeroOp: etapa.numeroOp,
      estacaoNome: etapa.estacaoNome,
      dataHoraCliente: this.agora().toISOString(),
    });
    return clientRequestId;
  }

  /** Atualiza o cache da estação depois de uma sincronização; falha de rede é silenciosa. */
  async atualizarCache(estacaoId: number): Promise<void> {
    try {
      const doServidor = await this.deps.api.minhas(estacaoId);
      await this.deps.cache.substituirDaEstacao(estacaoId, doServidor, this.agora().toISOString());
    } catch (erro) {
      if (!ehErroDeRede(erro)) throw interpretarErro(erro);
    }
  }

  private etapaDaEstacao(ordem: OrdemProducaoDetalhe, estacaoId: number): EtapaDaEstacao {
    const etapas = [...ordem.etapas].sort((a, b) => a.sequencia - b.sequencia);
    const alvo = etapas.find((e) => e.estacaoId === estacaoId && e.status !== 'Concluida');
    if (!alvo) {
      throw new ApiError(
        'SEM_ETAPA_NA_ESTACAO',
        `A ordem ${ordem.ordem.numero} não tem etapa em aberto nesta estação.`,
      );
    }

    return {
      etapaId: alvo.id,
      ordemProducaoId: ordem.ordem.id,
      numeroOp: ordem.ordem.numero,
      codigoQr: ordem.codigoQr,
      pedidoNumero: ordem.ordem.pedidoNumero,
      produtoId: ordem.ordem.produtoId,
      produtoCodigo: ordem.ordem.produtoCodigo,
      produtoNome: ordem.ordem.produtoNome,
      quantidade: ordem.ordem.quantidade,
      previsaoConclusao: ordem.ordem.previsaoConclusao,
      estacaoId: alvo.estacaoId,
      estacaoNome: alvo.estacaoNome,
      sequencia: alvo.sequencia,
      status: alvo.status,
      iniciadaEm: alvo.iniciadaEm,
      operadorId: alvo.operadorId,
      liberada: etapas.filter((e) => e.sequencia < alvo.sequencia).every((e) => e.status === 'Concluida'),
    };
  }
}
