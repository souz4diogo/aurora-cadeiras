import type { EtapaNaTela } from '@/features/etapas/tipos';
import { Texto } from '@/shared/componentes';
import { useTema } from '@/shared/tema';

/** Frase do estado de uma etapa; o estado nunca depende só de cor: leva ícone e texto (docs/08 §8). */
export function descreverEtapa(etapa: Pick<EtapaNaTela, 'status' | 'liberada' | 'pendenteEnvio'>): string {
  const base =
    etapa.status === 'Concluida'
      ? '✔ Concluída'
      : etapa.status === 'EmAndamento'
        ? '▶ Em andamento'
        : etapa.liberada
          ? '⏳ Pronta para iniciar'
          : '🔒 Aguardando etapa anterior';
  return etapa.pendenteEnvio ? `${base} · ⬆ a enviar` : base;
}

export function StatusDaEtapa({ etapa }: { etapa: EtapaNaTela }) {
  const { cores } = useTema();
  const cor =
    etapa.status === 'EmAndamento' ? cores.sucesso : etapa.status === 'Pendente' && !etapa.liberada ? cores.aviso : cores.texto;
  return (
    <Texto negrito cor={cor}>
      {descreverEtapa(etapa)}
    </Texto>
  );
}
