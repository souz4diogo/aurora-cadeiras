import type { AxiosInstance } from 'axios';

import type { Estacao, EtapaDaEstacao, MaterialDaEtapa, OrdemProducaoDetalhe } from '@/features/etapas/tipos';

export type TipoDePerda = 'Refugo' | 'Retrabalho';

export interface NovaPerda {
  tipo: TipoDePerda;
  quantidade: number;
  motivo: string;
}

export class EtapasApi {
  constructor(private readonly http: AxiosInstance) {}

  async estacoes(): Promise<Estacao[]> {
    const { data } = await this.http.get<Estacao[]>('/estacoes');
    return data;
  }

  /** Fila da estação: etapas pendentes ou em andamento (GET /etapas/minhas). */
  async minhas(estacaoId: number): Promise<EtapaDaEstacao[]> {
    const { data } = await this.http.get<EtapaDaEstacao[]>('/etapas/minhas', { params: { estacaoId } });
    return data;
  }

  /** Localiza a OP pelo código do QR da etiqueta. */
  async ordemPorQr(codigoQr: string): Promise<OrdemProducaoDetalhe> {
    const { data } = await this.http.get<OrdemProducaoDetalhe>(`/ordens-producao/qr/${encodeURIComponent(codigoQr)}`);
    return data;
  }

  async ordem(id: number): Promise<OrdemProducaoDetalhe> {
    const { data } = await this.http.get<OrdemProducaoDetalhe>(`/ordens-producao/${id}`);
    return data;
  }

  /** Materiais que o produto consome, por estação (ficha técnica). */
  async materiaisDoProduto(produtoId: number): Promise<MaterialDaEtapa[]> {
    const { data } = await this.http.get<{ itens: MaterialDaEtapa[] }>(`/produtos/${produtoId}/ficha-tecnica`);
    return data.itens;
  }

  async registrarPerda(etapaId: number, perda: NovaPerda): Promise<void> {
    await this.http.post(`/etapas/${etapaId}/perdas`, perda);
  }
}
