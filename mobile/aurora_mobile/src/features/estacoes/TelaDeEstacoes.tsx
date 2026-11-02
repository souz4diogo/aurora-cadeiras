import { router } from 'expo-router';
import { useCallback } from 'react';

import { useServicos } from '@/core/ServicosProvider';
import type { Estacao } from '@/features/etapas/tipos';
import { Aviso, Botao, EstadoVazio, Tela, Texto } from '@/shared/componentes';
import { formatarHora } from '@/shared/formatar';
import { useCarregamento } from '@/shared/useCarregamento';

/** Escolher estação (RF11): a escolha fica salva no aparelho e define a fila de etapas. */
export function TelaDeEstacoes() {
  const { estacoes } = useServicos();
  const carregar = useCallback(() => estacoes.listar(), [estacoes]);
  const lista = useCarregamento(carregar);

  async function escolher(estacao: Estacao): Promise<void> {
    await estacoes.escolher(estacao.id);
    router.replace('/etapas');
  }

  return (
    <Tela carregando={lista.carregando} aoAtualizar={() => void lista.recarregar()} atualizando={lista.atualizando}>
      <Texto tamanho="grande" negrito accessibilityRole="header">
        Em qual estação você está?
      </Texto>
      {lista.erro ? <Aviso tipo="erro" texto={lista.erro} /> : null}
      {lista.dados?.doCache ? (
        <Aviso tipo="aviso" texto={`Sem conexão. Lista de ${formatarHora(lista.dados.atualizadoEm)}.`} />
      ) : null}
      {lista.dados?.dados.map((estacao) => (
        <Botao
          key={estacao.id}
          titulo={estacao.nome}
          variante="secundario"
          dica="Define a estação e abre as etapas dela"
          aoPressionar={() => void escolher(estacao)}
        />
      ))}
      {lista.dados && lista.dados.dados.length === 0 ? (
        <EstadoVazio titulo="Nenhuma estação" texto="Peça ao gerente para cadastrar as estações da fábrica." />
      ) : null}
    </Tela>
  );
}
