import { useFocusEffect } from 'expo-router';
import { useCallback, useRef, useState } from 'react';

import { interpretarErro } from '@/core/http/problem';

export interface EstadoDeCarregamento<T> {
  dados: T | null;
  /** Primeira carga, sem nada para mostrar ainda. */
  carregando: boolean;
  /** "Puxar para atualizar" em andamento. */
  atualizando: boolean;
  erro: string | null;
  recarregar(): Promise<void>;
  /** Troca os dados sem ir à rede (a tela relê o aparelho quando a fila muda). */
  definir(atualizar: (anteriores: T | null) => T | null): void;
}

/**
 * Carrega dados quando a tela ganha foco e de novo a cada "puxar para atualizar". A mensagem de erro já vem em
 * português. Se a tela perder o foco no meio, a resposta atrasada não sobrescreve a mais nova.
 */
export function useCarregamento<T>(carregar: () => Promise<T>): EstadoDeCarregamento<T> {
  const [dados, setDados] = useState<T | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [atualizando, setAtualizando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const numeroDaCarga = useRef(0);

  const executar = useCallback(
    async (puxando: boolean) => {
      const minhaCarga = ++numeroDaCarga.current;
      if (puxando) setAtualizando(true);
      try {
        const resultado = await carregar();
        if (minhaCarga !== numeroDaCarga.current) return;
        setDados(resultado);
        setErro(null);
      } catch (falha) {
        if (minhaCarga !== numeroDaCarga.current) return;
        setErro(interpretarErro(falha).message);
      } finally {
        if (minhaCarga === numeroDaCarga.current) {
          setCarregando(false);
          setAtualizando(false);
        }
      }
    },
    [carregar],
  );

  useFocusEffect(
    useCallback(() => {
      void executar(false);
    }, [executar]),
  );

  const recarregar = useCallback(() => executar(true), [executar]);
  return { dados, carregando, atualizando, erro, recarregar, definir: setDados };
}
