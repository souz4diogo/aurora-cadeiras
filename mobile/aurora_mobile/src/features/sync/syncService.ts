import { TAMANHO_DO_LOTE } from '@/core/config';
import { ehErroDeRede } from '@/core/http/problem';
import type { FilaRepository } from '@/features/sync/filaRepository';
import type {
  ApontamentoPendente,
  ItemDoLote,
  ResultadoDoItem,
  ResumoDaSincronizacao,
} from '@/features/sync/tipos';

/** O que o serviço precisa da API (POST /apontamentos/sincronizar). */
export interface SincronizacaoApi {
  sincronizar(itens: ItemDoLote[]): Promise<ResultadoDoItem[]>;
}

export interface DependenciasDaSincronizacao {
  fila: FilaRepository;
  api: SincronizacaoApi;
  /** Depois de enviar, recarrega do servidor o que o app mostra (o servidor é a fonte da verdade, docs/08 §5). */
  aposSincronizar?: () => Promise<void>;
  /** Chamado com os itens confirmados ANTES de saírem da fila, para o estado local não voltar atrás. */
  aoConfirmar?: (itens: ApontamentoPendente[]) => Promise<void>;
  tamanhoDoLote?: number;
}

const RESUMO_VAZIO: ResumoDaSincronizacao = {
  confirmados: 0,
  rejeitados: 0,
  aindaPendentes: 0,
  interrompidaPorRede: false,
};

/** Se a rodada pode seguir para o próximo lote ou deve parar. */
type Andamento = 'seguir' | 'parar';

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
    const { fila } = this.deps;
    const tamanho = this.deps.tamanhoDoLote ?? TAMANHO_DO_LOTE;
    const resumo = { ...RESUMO_VAZIO };

    for (;;) {
      const lote = await fila.proximosPendentes(tamanho);
      if (lote.length === 0) break;
      if ((await this.enviarLote(lote, resumo)) === 'parar') break;
    }

    resumo.aindaPendentes = (await fila.contar()).pendentes;
    if (resumo.confirmados > 0 || resumo.rejeitados > 0) await this.recarregarTela();
    return resumo;
  }

  /** Marca o lote como em envio, manda ao servidor e trata a resposta (ou a falta dela). */
  private async enviarLote(lote: ApontamentoPendente[], resumo: ResumoDaSincronizacao): Promise<Andamento> {
    const { fila } = this.deps;
    const ids = lote.map((i) => i.clientRequestId);
    await fila.marcarEnviando(ids);

    let resultados: ResultadoDoItem[];
    try {
      resultados = await this.deps.api.sincronizar(
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
      if (!ehErroDeRede(erro)) throw erro;
      resumo.interrompidaPorRede = true;
      return 'parar';
    }
    return this.aplicarResultados(lote, resultados, resumo);
  }

  /** Confirmados saem da fila, rejeitados ficam com o motivo e o que o servidor não respondeu volta a pendente. */
  private async aplicarResultados(
    lote: ApontamentoPendente[],
    resultados: ResultadoDoItem[],
    resumo: ResumoDaSincronizacao,
  ): Promise<Andamento> {
    const { fila } = this.deps;
    const porId = new Map(resultados.map((r) => [r.clientRequestId, r]));
    const confirmados: ApontamentoPendente[] = [];
    const semResposta: string[] = [];

    for (const item of lote) {
      const resultado = porId.get(item.clientRequestId);
      if (!resultado) {
        semResposta.push(item.clientRequestId);
      } else if (resultado.status === 'Rejeitado') {
        await fila.marcarRejeitado(item.clientRequestId, resultado.code ?? 'ERRO', resultado.mensagem ?? 'Rejeitado pelo servidor.');
        resumo.rejeitados++;
      } else {
        confirmados.push(item);
      }
    }

    await this.refletirConfirmados(confirmados);
    await fila.remover(confirmados.map((i) => i.clientRequestId));
    await fila.devolverParaFila(semResposta);
    resumo.confirmados += confirmados.length;

    // Item que o servidor não respondeu voltaria no próximo laço: encerra a rodada para não girar em círculo
    return semResposta.length > 0 ? 'parar' : 'seguir';
  }

  /** Falha ao atualizar o cache local não pode impedir de tirar da fila o que o servidor já aceitou. */
  private async refletirConfirmados(itens: ApontamentoPendente[]): Promise<void> {
    if (itens.length === 0) return;
    try {
      await this.deps.aoConfirmar?.(itens);
    } catch {
      // O recarregamento do servidor, logo em seguida, acerta o cache
    }
  }

  /** Atualizar a tela é um bônus: a fila já está correta mesmo que isto falhe. */
  private async recarregarTela(): Promise<void> {
    try {
      await this.deps.aposSincronizar?.();
    } catch {
      // ignorado de propósito
    }
  }
}
