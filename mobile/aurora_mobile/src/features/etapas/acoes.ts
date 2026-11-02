import type { Perfil } from '@/core/auth/tokenStore';
import type { EtapaNaTela } from '@/features/etapas/tipos';

export interface AcoesDaEtapa {
  iniciar: boolean;
  finalizar: boolean;
  registrarPerda: boolean;
  inspecionar: boolean;
  /** Por que não há botão de avanço (etapa travada, concluída ou fora do perfil), para a tela explicar. */
  explicacao: string | null;
  /** Perda e inspeção falam direto com o servidor; antes disso ele precisa ter recebido o início. */
  precisaEnviarAntes: boolean;
}

/**
 * Quais botões a etapa mostra para o perfil (docs/08 §2 e §3):
 * - Operador inicia e finaliza, e registra perda enquanto a etapa está em andamento;
 * - Inspetor inicia a etapa de Qualidade e a conclui pela inspeção (nunca por "Finalizar", RN: só a inspeção a fecha).
 * A API continua sendo a barreira real; isto só evita oferecer o que será recusado.
 */
export function acoesDaEtapa(etapa: EtapaNaTela, perfil: Perfil): AcoesDaEtapa {
  const nenhuma: AcoesDaEtapa = {
    iniciar: false,
    finalizar: false,
    registrarPerda: false,
    inspecionar: false,
    explicacao: null,
    precisaEnviarAntes: false,
  };
  const ehOperador = perfil === 'Operador';
  const ehInspetor = perfil === 'Inspetor';
  if (!ehOperador && !ehInspetor) return { ...nenhuma, explicacao: 'Seu perfil não aponta etapas.' };

  if (etapa.status === 'Concluida') return { ...nenhuma, explicacao: 'Esta etapa já foi concluída.' };

  if (etapa.status === 'Pendente') {
    return etapa.liberada
      ? { ...nenhuma, iniciar: true }
      : { ...nenhuma, explicacao: 'Aguardando a etapa anterior da ordem ser concluída.' };
  }

  // Em andamento
  const noServidor = !etapa.pendenteEnvio;
  const ehQualidade = etapa.estacaoNome === 'Qualidade';
  if (ehOperador) {
    return { ...nenhuma, finalizar: true, registrarPerda: true, precisaEnviarAntes: !noServidor };
  }
  return ehQualidade
    ? { ...nenhuma, inspecionar: true, precisaEnviarAntes: !noServidor }
    : { ...nenhuma, explicacao: 'Só o operador finaliza esta etapa.' };
}
