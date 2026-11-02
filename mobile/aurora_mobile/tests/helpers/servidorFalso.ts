import { ApiError, SEM_CONEXAO } from '@/core/http/problem';
import type { SincronizacaoApi } from '@/features/sync/syncService';
import type { ItemDoLote, ResultadoDoItem } from '@/features/sync/tipos';

export function erroDeRede(): ApiError {
  return new ApiError(SEM_CONEXAO, 'Sem conexão com o servidor.');
}

/**
 * Servidor de mentira para a sincronização, com as mesmas garantias do real (docs/06 §5): reenviar um
 * `clientRequestId` já aplicado devolve `JaProcessado` e não repete o efeito; um item rejeitado não derruba os demais.
 */
export class ServidorDeSincronizacaoFalso implements SincronizacaoApi {
  /** Chamadas recebidas, uma lista de ids por lote. */
  readonly lotes: string[][] = [];
  /** Quantas vezes cada id teve efeito de verdade no servidor (deve ficar em 0 ou 1). */
  readonly efeitos = new Map<string, number>();

  online = true;
  /** O servidor aplica o lote, mas a resposta se perde no caminho (a rede cai depois do processamento). */
  perderRespostaDaProximaChamada = false;
  /** Regra do servidor por item: devolve o motivo da recusa, ou nada se aceita. */
  regra: (item: ItemDoLote) => { code: string; mensagem: string } | undefined = () => undefined;
  /** Falha inesperada (não é rede) na próxima chamada. */
  falharComErro: Error | null = null;
  /** Omite estes ids da resposta, como um servidor com defeito. */
  omitirDaResposta = new Set<string>();

  async sincronizar(itens: ItemDoLote[]): Promise<ResultadoDoItem[]> {
    if (!this.online) throw erroDeRede();
    if (this.falharComErro) {
      const erro = this.falharComErro;
      this.falharComErro = null;
      throw erro;
    }
    this.lotes.push(itens.map((i) => i.clientRequestId));

    const resultados: ResultadoDoItem[] = itens.map((item) => {
      if ((this.efeitos.get(item.clientRequestId) ?? 0) > 0) {
        return { clientRequestId: item.clientRequestId, status: 'JaProcessado' };
      }
      const recusa = this.regra(item);
      if (recusa) {
        return { clientRequestId: item.clientRequestId, status: 'Rejeitado', code: recusa.code, mensagem: recusa.mensagem };
      }
      this.efeitos.set(item.clientRequestId, 1);
      return { clientRequestId: item.clientRequestId, status: 'Aplicado' };
    });

    if (this.perderRespostaDaProximaChamada) {
      this.perderRespostaDaProximaChamada = false;
      throw erroDeRede();
    }
    return resultados.filter((r) => !this.omitirDaResposta.has(r.clientRequestId));
  }

  get totalDeEfeitos(): number {
    return [...this.efeitos.values()].reduce((a, b) => a + b, 0);
  }
}
