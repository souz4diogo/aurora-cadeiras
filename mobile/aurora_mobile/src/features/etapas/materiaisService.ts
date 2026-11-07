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

  /**
   * Guarda de antemão a ficha técnica de cada produto da fila, para a etapa mostrar os materiais mesmo se for aberta
   * pela primeira vez já sem rede. É um esforço em segundo plano: uma falha não atrapalha a fila e a etapa ainda
   * tenta buscar a ficha quando for aberta.
   */
  async guardarFichas(produtoIds: readonly number[]): Promise<void> {
    const unicos = [...new Set(produtoIds)];
    await Promise.allSettled(
      unicos.map(async (produtoId) => this.cache.guardar(`ficha:${produtoId}`, await this.api.materiaisDoProduto(produtoId))),
    );
  }
}
