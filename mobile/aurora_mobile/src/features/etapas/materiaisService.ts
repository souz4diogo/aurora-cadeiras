import type { CacheJsonRepository } from '@/core/db/cacheJsonRepository';
import type { EtapasApi } from '@/features/etapas/etapasApi';
import type { MaterialDaEtapa } from '@/features/etapas/tipos';
import { consultarComCache, type ResultadoDaConsulta } from '@/shared/consultaComCache';

/** Materiais que a etapa consome (RF13), tirados da ficha técnica do produto e guardados para uso sem rede. */
export class MateriaisService {
  constructor(
    private readonly api: EtapasApi,
    private readonly cache: CacheJsonRepository,
  ) {}

  async daEtapa(produtoId: number, estacaoId: number): Promise<ResultadoDaConsulta<MaterialDaEtapa[]>> {
    const ficha = await consultarComCache(this.cache, `ficha:${produtoId}`, () => this.api.materiaisDoProduto(produtoId));
    return { ...ficha, dados: ficha.dados.filter((m) => m.estacaoId === estacaoId) };
  }
}
