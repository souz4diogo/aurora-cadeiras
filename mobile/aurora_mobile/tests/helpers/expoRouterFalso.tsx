import { useEffect } from 'react';
import { Text } from 'react-native';

/** Expo Router de mentira: as telas só usam router.* e useFocusEffect (que roda ao montar). */
export const router = { push: jest.fn(), replace: jest.fn(), back: jest.fn(), dismissTo: jest.fn() };

export function useFocusEffect(efeito: () => void): void {
  useEffect(efeito, [efeito]);
}

export function Redirect({ href }: { href: string }) {
  return <Text testID="redirect">{String(href)}</Text>;
}
