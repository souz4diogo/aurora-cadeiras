import { useLocalSearchParams } from 'expo-router';

import { TelaDeInspecao } from '@/features/qualidade/TelaDeInspecao';

export default function Inspecao() {
  const { ordemId } = useLocalSearchParams<{ ordemId: string }>();
  return <TelaDeInspecao ordemId={Number(ordemId)} />;
}
