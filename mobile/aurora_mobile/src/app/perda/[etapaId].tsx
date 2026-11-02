import { useLocalSearchParams } from 'expo-router';

import { TelaDePerda } from '@/features/perdas/TelaDePerda';

export default function Perda() {
  const { etapaId } = useLocalSearchParams<{ etapaId: string }>();
  return <TelaDePerda etapaId={Number(etapaId)} />;
}
