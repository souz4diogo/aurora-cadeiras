import { useCallback } from 'react';

import { useServicos } from '@/core/ServicosProvider';
import { Aviso, Cartao, EstadoVazio, Tela, Texto } from '@/shared/componentes';
import { formatarDataEHora, formatarQuantidade } from '@/shared/formatar';
import { useCarregamento } from '@/shared/useCarregamento';
import { useTema } from '@/shared/tema';

/**
 * Consulta de estoque (RF14): saldo de matéria-prima com o último dado conhecido e a hora da atualização, para
 * o operador não confiar num saldo velho sem saber (docs/08 §5, "Prevenção").
 */
export function TelaDeEstoque() {
  const { estoque } = useServicos();
  const { cores } = useTema();
  const carregar = useCallback(() => estoque.saldos(), [estoque]);
  const lista = useCarregamento(carregar);
  const dados = lista.dados;

  return (
    <Tela carregando={lista.carregando} aoAtualizar={() => void lista.recarregar()} atualizando={lista.atualizando}>
      {lista.erro ? <Aviso tipo="erro" texto={lista.erro} /> : null}
      {dados ? (
        <Aviso
          tipo={dados.doCache ? 'aviso' : 'info'}
          texto={`${dados.doCache ? 'Sem conexão. Último saldo conhecido, de ' : 'Atualizado em '}${formatarDataEHora(dados.atualizadoEm)}.`}
        />
      ) : null}

      {dados && dados.dados.total > dados.dados.itens.length ? (
        <Aviso tipo="aviso" texto={`Mostrando ${dados.dados.itens.length} de ${dados.dados.total} materiais. Use o painel web para ver todos.`} />
      ) : null}

      {dados?.dados.itens.map((m) => (
        <Cartao
          key={m.id}
          rotuloDeAcessibilidade={`${m.nome}. Saldo ${formatarQuantidade(m.estoqueAtual)} ${m.unidade}. Mínimo ${formatarQuantidade(m.estoqueMinimo)}. ${m.critico ? 'Estoque crítico.' : 'Estoque normal.'}`}
        >
          <Texto negrito>{m.nome}</Texto>
          <Texto secundario tamanho="pequeno">{m.codigo}</Texto>
          <Texto tamanho="grande">{`${formatarQuantidade(m.estoqueAtual)} ${m.unidade}`}</Texto>
          <Texto secundario tamanho="pequeno">{`Mínimo: ${formatarQuantidade(m.estoqueMinimo)} ${m.unidade}`}</Texto>
          {m.critico ? (
            <Texto negrito cor={cores.perigo}>⚠ Estoque crítico (no mínimo ou abaixo)</Texto>
          ) : (
            <Texto cor={cores.sucesso}>✔ Estoque normal</Texto>
          )}
        </Cartao>
      ))}

      {dados && dados.dados.itens.length === 0 ? (
        <EstadoVazio titulo="Nenhum material" texto="Não há matéria-prima cadastrada." />
      ) : null}
    </Tela>
  );
}
