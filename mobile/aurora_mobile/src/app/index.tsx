import { Redirect } from 'expo-router';
import { useCallback } from 'react';

import { useServicos } from '@/core/ServicosProvider';
import { useUsuario } from '@/features/auth/AuthProvider';
import { useCarregamento } from '@/shared/useCarregamento';
import { Tela } from '@/shared/componentes';

/** Porta de entrada depois do login: cada perfil cai na sua tela inicial (docs/08 §3). */
export default function Inicio() {
  const usuario = useUsuario();
  const { estacoes } = useServicos();
  const carregarEstacao = useCallback(() => estacoes.estacaoEscolhida(), [estacoes]);
  const estacao = useCarregamento(carregarEstacao);

  if (usuario.perfil === 'Vendedor' || usuario.perfil === 'Cliente') return <Redirect href="/pedidos" />;
  if (usuario.perfil !== 'Operador' && usuario.perfil !== 'Inspetor') return <Redirect href="/nao-suportado" />;
  if (estacao.carregando) return <Tela carregando>{null}</Tela>;
  return <Redirect href={estacao.dados === null ? '/estacao' : '/etapas'} />;
}
