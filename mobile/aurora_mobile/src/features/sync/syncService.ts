import { TAMANHO_DO_LOTE } from '@/core/config';
import { ehErroDeRede } from '@/core/http/problem';
import type { FilaRepository } from '@/features/sync/filaRepository';
import type { ItemDoLote, ResultadoDoItem, ResumoDaSincronizacao } from '@/features/sync/tipos';

/** O que o serviço precisa da API (POST /apontamentos/sincronizar). */
export interface SincronizacaoApi {
  sincronizar(itens: ItemDoLote[]): Promise<ResultadoDoItem[]>;
}

export interface DependenciasDaSincronizacao {
  fila: FilaRepository;
  api: SincronizacaoApi;
  /** Depois de enviar, recarrega do servidor o que o app mostra (o servidor é a fonte da verdade, docs/08 §5). */
  aposSincronizar?: () => Promise<void>;
  tamanhoDoLote?: number;
}

const RESUMO_VAZIO: ResumoDaSincronizacao = {
  confirmados: 0,
  rejeitados: 0,
  aindaPendentes: 0,
  interrompidaPorRede: false,
};

/**
 * Envia a fila de apontamentos ao servidor (docs/08 §5): em ordem de hora do aparelho, em lotes, sem perder item.
 * Aplicado e JaProcessado saem da fila; Rejeitado fica com o motivo; falha de rede devolve o lote à fila.
 */
export class SyncService {
  private emAndamento: Promise<ResumoDaSincronizacao> | null = null;

  constructor(private readonly deps: DependenciasDaSincronizacao) {}

  get sincronizando(): boolean {
    return this.emAndamento !== null;
  }

  /** Uma rodada por vez: quem pedir enquanto outra roda recebe o resultado dela. */
  sincronizar(): Promise<ResumoDaSincronizacao> {
    this.emAndamento ??= this.executar().finally(() => {
      this.emAndamento = null;
    });
    return this.emAndamento;
  }

  private async executar(): Promise<ResumoDaSincronizacao> {
    const { fila, api, aposSincronizar } = this.deps;
    const tamanho = this.deps.tamanhoDoLote ?? TAMANHO_DO_LOTE;
    const resumo = { ...RESUMO_VAZIO };

    for (;;) {
      const lote = await fila.proximosPendentes(tamanho);
      if (lote.length === 0) break;

      const ids = lote.map((i) => i.clientRequestId);
      await fila.marcarEnviando(ids);

      let resultados: ResultadoDoItem[];
      try {
        resultados = await api.sincronizar(
          lote.map(({ clientRequestId, tipo, etapaId, dataHoraCliente }) => ({
            clientRequestId,
            tipo,
            etapaId,
            dataHoraCliente,
          })),
        );
      } catch (erro) {
        // Sem resposta não dá para saber o que o servidor aplicou: devolve tudo; o reenvio é idempotente
        await fila.devolverParaFila(ids);
        if (ehErroDeRede(erro)) {
          resumo.interrompidaPorRede = true;
          break;
        }
        throw erro;
      }

      const porId = new Map(resultados.map((r) => [r.clientRequestId, r]));
      const confirmados: string[] = [];
      const semResposta: string[] = [];
      for (const id of ids) {
        const resultado = porId.get(id);
        if (!resultado) {
          semResposta.push(id);
        } else if (resultado.status === 'Rejeitado') {
          await fila.marcarRejeitado(id, resultado.code ?? 'ERRO', resultado.mensagem ?? 'Rejeitado pelo servidor.');
          resumo.rejeitados++;
        } else {
          confirmados.push(id);
        }
      }
      await fila.remover(confirmados);
      await fila.devolverParaFila(semResposta);
      resumo.confirmados += confirmados.length;

      // Item que o servidor não respondeu voltaria no próximo laço: para evitar laço infinito, encerra a rodada
      if (semResposta.length > 0) break;
    }

    resumo.aindaPendentes = (await fila.contar()).pendentes;

    if (resumo.confirmados > 0 || resumo.rejeitados > 0) {
      try {
        await aposSincronizar?.();
      } catch {
        // Atualizar a tela é um bônus; a fila já está correta
      }
    }
    return resumo;
  }
}
