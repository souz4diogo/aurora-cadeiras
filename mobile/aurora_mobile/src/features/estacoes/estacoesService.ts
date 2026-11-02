import type { CacheJsonRepository } from '@/core/db/cacheJsonRepository';
import type { EtapasApi } from '@/features/etapas/etapasApi';
import type { Estacao } from '@/features/etapas/tipos';
import { CHAVES, type PreferenciasRepository } from '@/features/preferencias/preferenciasRepository';
import { consultarComCache, type ResultadoDaConsulta } from '@/shared/consultaComCache';

export class EstacoesService {
  constructor(
    private readonly api: EtapasApi,
    private readonly cache: CacheJsonRepository,
    private readonly preferencias: PreferenciasRepository,
  ) {}

  /** Estações que geram etapa, na ordem do layout da fábrica (RF10). Cada uma é um lugar onde o operador trabalha. */
  async listar(): Promise<ResultadoDaConsulta<Estacao[]>> {
    const resultado = await consultarComCache(this.cache, 'estacoes', () => this.api.estacoes());
    return {
      ...resultado,
      dados: resultado.dados.filter((e) => e.geraEtapa).sort((a, b) => a.ordem - b.ordem),
    };
  }

  async estacaoEscolhida(): Promise<number | null> {
    const valor = await this.preferencias.obter(CHAVES.estacao);
    const id = valor === null ? NaN : Number(valor);
    return Number.isInteger(id) ? id : null;
  }

  async escolher(estacaoId: number): Promise<void> {
    await this.preferencias.definir(CHAVES.estacao, String(estacaoId));
  }
}
