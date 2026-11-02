import type { AxiosInstance } from 'axios';

import type { CacheJsonRepository } from '@/core/db/cacheJsonRepository';
import { consultarComCache, type ResultadoDaConsulta } from '@/shared/consultaComCache';

export interface SaldoDeMateriaPrima {
  id: number;
  codigo: string;
  nome: string;
  unidade: string;
  estoqueAtual: number;
  estoqueMinimo: number;
  critico: boolean;
}

/** Os saldos devolvidos e quantos materiais existem ao todo (a API entrega no máximo 100 por página). */
export interface ListaDeSaldos {
  itens: SaldoDeMateriaPrima[];
  total: number;
}

/** Consulta de saldo de matéria-prima para o operador (RF14), com o último dado conhecido quando está sem rede. */
export class EstoqueService {
  constructor(
    private readonly http: AxiosInstance,
    private readonly cache: CacheJsonRepository,
  ) {}

  saldos(): Promise<ResultadoDaConsulta<ListaDeSaldos>> {
    return consultarComCache(this.cache, 'estoque:materias-primas', async () => {
      const { data } = await this.http.get<{ items: SaldoDeMateriaPrima[]; total?: number }>('/estoque/materias-primas', {
        params: { page: 1, pageSize: 100 },
      });
      return { itens: data.items, total: data.total ?? data.items.length };
    });
  }
}
