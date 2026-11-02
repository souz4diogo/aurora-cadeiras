import type { AxiosInstance } from 'axios';

import type { SincronizacaoApi } from '@/features/sync/syncService';
import type { ItemDoLote, ResultadoDoItem } from '@/features/sync/tipos';

/** POST /apontamentos/sincronizar: lote idempotente com um resultado por item (docs/06 §5). */
export class SincronizacaoHttpApi implements SincronizacaoApi {
  constructor(private readonly http: AxiosInstance) {}

  async sincronizar(itens: ItemDoLote[]): Promise<ResultadoDoItem[]> {
    const { data } = await this.http.post<{ resultados: ResultadoDoItem[] }>('/apontamentos/sincronizar', { itens });
    return data.resultados;
  }
}
