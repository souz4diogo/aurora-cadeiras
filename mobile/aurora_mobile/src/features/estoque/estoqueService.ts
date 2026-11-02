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

/** Consulta de saldo de matéria-prima para o operador (RF14), com o último dado conhecido quando está sem rede. */
export class EstoqueService {
  constructor(
    private readonly http: AxiosInstance,
    private readonly cache: CacheJsonRepository,
  ) {}

  saldos(): Promise<ResultadoDaConsulta<SaldoDeMateriaPrima[]>> {
    return consultarComCache(this.cache, 'estoque:materias-primas', async () => {
      const { data } = await this.http.get<{ items: SaldoDeMateriaPrima[] }>('/estoque/materias-primas', {
        params: { page: 1, pageSize: 100 },
      });
      return data.items;
    });
  }
}
