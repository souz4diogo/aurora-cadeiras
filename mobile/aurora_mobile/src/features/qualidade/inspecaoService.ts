import type { AxiosInstance } from 'axios';

import { ApiError, interpretarErro } from '@/core/http/problem';
import type { EtapasApi } from '@/features/etapas/etapasApi';

export type ResultadoDaInspecao = 'Aprovada' | 'Reprovada';

export interface DadosDaInspecao {
  resultado: ResultadoDaInspecao;
  estacaoRetrabalhoId?: number;
  observacao?: string;
}

export interface InspecaoRegistrada {
  id: number;
  numeroOp: string;
  resultado: ResultadoDaInspecao;
  statusOrdem: string;
  statusPedido: string;
}

export interface OpcaoDeRetrabalho {
  estacaoId: number;
  nome: string;
}

const TAMANHO_MAXIMO_DA_OBSERVACAO = 300;

/** Inspeção de qualidade (RF18). Exige conexão: aprovar conclui a OP e reprovar reabre etapas, decisões do servidor. */
export class InspecaoService {
  constructor(
    private readonly http: AxiosInstance,
    private readonly etapasApi: EtapasApi,
  ) {}

  /**
   * Estações para onde a OP pode voltar: as do roteiro dela que vêm antes da Qualidade (RN06). Sai da própria OP,
   * então o inspetor só vê opções válidas e o servidor continua sendo a barreira.
   */
  async opcoesDeRetrabalho(ordemProducaoId: number): Promise<OpcaoDeRetrabalho[]> {
    try {
      const ordem = await this.etapasApi.ordem(ordemProducaoId);
      const etapas = [...ordem.etapas].sort((a, b) => a.sequencia - b.sequencia);
      const qualidade = etapas.find((e) => e.estacaoNome === 'Qualidade');
      const limite = qualidade?.sequencia ?? Number.MAX_SAFE_INTEGER;
      return etapas.filter((e) => e.sequencia < limite).map((e) => ({ estacaoId: e.estacaoId, nome: e.estacaoNome }));
    } catch (erro) {
      throw interpretarErro(erro);
    }
  }

  async registrar(ordemProducaoId: number, dados: DadosDaInspecao): Promise<InspecaoRegistrada> {
    if (dados.resultado === 'Reprovada' && dados.estacaoRetrabalhoId === undefined) {
      throw new ApiError('RN06', 'Escolha a estação de retrabalho para reprovar.');
    }
    if ((dados.observacao ?? '').length > TAMANHO_MAXIMO_DA_OBSERVACAO) {
      throw new ApiError('VALIDACAO', `A observação pode ter no máximo ${TAMANHO_MAXIMO_DA_OBSERVACAO} caracteres.`);
    }
    try {
      const { data } = await this.http.post<InspecaoRegistrada>(`/ordens-producao/${ordemProducaoId}/inspecoes`, {
        resultado: dados.resultado,
        estacaoRetrabalhoId: dados.resultado === 'Reprovada' ? dados.estacaoRetrabalhoId : null,
        observacao: dados.observacao?.trim() || null,
      });
      return data;
    } catch (erro) {
      throw interpretarErro(erro);
    }
  }
}
