import { useLocalSearchParams } from 'expo-router';

import { TelaDeDetalheDaEtapa } from '@/features/etapas/TelaDeDetalheDaEtapa';

export default function DetalheDaEtapa() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <TelaDeDetalheDaEtapa etapaId={Number(id)} />;
}
