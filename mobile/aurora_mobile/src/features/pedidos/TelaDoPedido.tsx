import { useCallback } from 'react';
import { View } from 'react-native';

import { useServicos } from '@/core/ServicosProvider';
import { rotuloDoStatus } from '@/features/pedidos/TelaDePedidos';
import { Aviso, Cartao, Tela, Texto } from '@/shared/componentes';
import { formatarData, formatarDataEHora, formatarMoeda } from '@/shared/formatar';
import { useCarregamento } from '@/shared/useCarregamento';

/** Detalhe do pedido (RF22): itens e a linha do tempo da produção, do registro à expedição. */
export function TelaDoPedido({ pedidoId }: { pedidoId: number }) {
  const { pedidos } = useServicos();
  const carregar = useCallback(() => pedidos.acompanhar(pedidoId), [pedidos, pedidoId]);
  const detalhe = useCarregamento(carregar);
  const dados = detalhe.dados;

  return (
    <Tela carregando={detalhe.carregando} aoAtualizar={() => void detalhe.recarregar()} atualizando={detalhe.atualizando}>
      {detalhe.erro ? <Aviso tipo="erro" texto={detalhe.erro} /> : null}
      {dados?.doCache ? (
        <Aviso tipo="aviso" texto={`Sem conexão. Andamento de ${formatarDataEHora(dados.atualizadoEm)}.`} />
      ) : null}

      {dados ? (
        <>
          <Cartao>
            <Texto tamanho="titulo" negrito accessibilityRole="header">{dados.dados.pedido.numero}</Texto>
            <Texto negrito>{rotuloDoStatus(dados.dados.pedido.status)}</Texto>
            <Texto secundario>{`Prazo de entrega: ${formatarData(dados.dados.pedido.prazoEntrega)}`}</Texto>
            {dados.dados.pedido.atrasado ? <Texto negrito>⚠ Atrasado</Texto> : null}
          </Cartao>

          <Cartao>
            <Texto negrito accessibilityRole="header">Itens</Texto>
            {dados.dados.pedido.itens.map((item) => (
              <Texto key={item.id}>{`${item.quantidade} × ${item.produtoNome}`}</Texto>
            ))}
            <Texto negrito>{`Total: ${formatarMoeda(dados.dados.pedido.valorTotal)}`}</Texto>
          </Cartao>

          <Cartao>
            <Texto negrito accessibilityRole="header">Andamento da produção</Texto>
            {dados.dados.eventos.map((evento, indice) => (
              <View
                key={`${evento.dataHora}-${indice}`}
                accessible
                accessibilityLabel={`${formatarDataEHora(evento.dataHora)}. ${evento.evento}${evento.numeroOp ? `, ordem ${evento.numeroOp}` : ''}`}
                style={{ paddingVertical: 4 }}
              >
                <Texto>{`● ${evento.evento}`}</Texto>
                <Texto secundario tamanho="pequeno">
                  {`${formatarDataEHora(evento.dataHora)}${evento.numeroOp ? ` · ${evento.numeroOp}` : ''}`}
                </Texto>
              </View>
            ))}
          </Cartao>
        </>
      ) : null}
    </Tela>
  );
}
