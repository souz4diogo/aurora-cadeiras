import { ApiError, interpretarErro } from '@/core/http/problem';
import type { EtapasApi, TipoDePerda } from '@/features/etapas/etapasApi';

export interface DadosDaPerda {
  tipo: TipoDePerda;
  quantidade: number;
  motivo: string;
}

/** Limite igual ao da API (PerdaRequestValidator). */
export const QUANTIDADE_MAXIMA_DA_PERDA = 100_000;
export const TAMANHO_MAXIMO_DO_MOTIVO = 200;

/** Valida o formulário no aparelho, antes de gastar uma chamada; devolve a mensagem do primeiro problema. */
export function validarPerda(quantidade: string, motivo: string): string | null {
  const numero = Number(quantidade);
  if (!/^\d+$/.test(quantidade.trim()) || numero < 1 || numero > QUANTIDADE_MAXIMA_DA_PERDA) {
    return `Informe a quantidade, um número inteiro de 1 a ${QUANTIDADE_MAXIMA_DA_PERDA}.`;
  }
  if (motivo.trim().length === 0) return 'Informe o motivo da perda.';
  if (motivo.trim().length > TAMANHO_MAXIMO_DO_MOTIVO) {
    return `O motivo pode ter no máximo ${TAMANHO_MAXIMO_DO_MOTIVO} caracteres.`;
  }
  return null;
}

/** Registro de refugo ou retrabalho (RF12). Exige conexão: o servidor confere se a etapa está em andamento. */
export class PerdasService {
  constructor(private readonly api: EtapasApi) {}

  async registrar(etapaId: number, dados: DadosDaPerda): Promise<void> {
    const problema = validarPerda(String(dados.quantidade), dados.motivo);
    if (problema) throw new ApiError('VALIDACAO', problema);
    try {
      await this.api.registrarPerda(etapaId, {
        tipo: dados.tipo,
        quantidade: dados.quantidade,
        motivo: dados.motivo.trim(),
      });
    } catch (erro) {
      throw interpretarErro(erro);
    }
  }
}
