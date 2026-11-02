import * as Haptics from 'expo-haptics';

/** Vibração de confirmação depois de um apontamento (docs/08 §8). Nunca derruba a ação se o aparelho não vibra. */
export function vibrarSucesso(): void {
  void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => {});
}

export function vibrarAlerta(): void {
  void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Warning).catch(() => {});
}

export function vibrarToque(): void {
  void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light).catch(() => {});
}
