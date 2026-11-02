import { router } from 'expo-router';
import { useCallback } from 'react';

import { useServicos } from '@/core/ServicosProvider';
import { useUsuario } from '@/features/auth/AuthProvider';
import { Aviso, Botao, Cartao, EstadoVazio, Tela, Texto } from '@/shared/componentes';
import { formatarData, formatarDataEHora, formatarMoeda } from '@/shared/formatar';
import { useCarregamento } from '@/shared/useCarregamento';

const ROTULOS: Record<string, string> = {
  Aberto: '📝 Aberto',
  Confirmado: '✔ Confirmado',
  EmProducao: '🏭 Em produção',
  Pronto: '📦 Pronto para expedir',
  Expedido: '🚚 Expedido',
  Cancelado: '✖ Cancelado',
};

export function rotuloDoStatus(status: string): string {
  return ROTULOS[status] ?? status;
}

/** Meus pedidos (RF22): o vendedor vê os que criou, e o cliente, os dele. Somente leitura. */
export function TelaDePedidos() {
  const usuario = useUsuario();
  const { pedidos } = useServicos();
  const carregar = useCallback(() => pedidos.listar(usuario.perfil), [pedidos, usuario.perfil]);
  const lista = useCarregamento(carregar);
  const dados = lista.dados;

  return (
    <Tela carregando={lista.carregando} aoAtualizar={() => void lista.recarregar()} atualizando={lista.atualizando}>
      <Texto secundario>{`Olá, ${usuario.nome}.`}</Texto>
      {lista.erro ? <Aviso tipo="erro" texto={lista.erro} /> : null}
      {dados?.doCache ? (
        <Aviso tipo="aviso" texto={`Sem conexão. Lista de ${formatarDataEHora(dados.atualizadoEm)}.`} />
      ) : null}

      {dados?.dados.map((pedido) => (
        <Cartao
          key={pedido.id}
          aoPressionar={() => router.push({ pathname: '/pedido/[id]', params: { id: String(pedido.id) } })}
          rotuloDeAcessibilidade={`Pedido ${pedido.numero}, ${rotuloDoStatus(pedido.status)}, prazo ${formatarData(pedido.prazoEntrega)}${pedido.atrasado ? ', atrasado' : ''}`}
          dica="Abre o andamento do pedido"
        >
          <Texto negrito tamanho="grande">{pedido.numero}</Texto>
          {usuario.perfil === 'Vendedor' ? <Texto>{pedido.clienteNome}</Texto> : null}
          <Texto negrito>{rotuloDoStatus(pedido.status)}</Texto>
          <Texto secundario tamanho="pequeno">
            {`Prazo ${formatarData(pedido.prazoEntrega)} · ${pedido.quantidadeItens} item(ns) · ${formatarMoeda(pedido.valorTotal)}`}
          </Texto>
          {pedido.atrasado ? <Texto negrito>⚠ Atrasado</Texto> : null}
        </Cartao>
      ))}

      {dados && dados.dados.length === 0 ? (
        <EstadoVazio
          titulo="Nenhum pedido"
          texto={usuario.perfil === 'Cliente' ? 'Quando você fizer um pedido, ele aparece aqui.' : 'Os pedidos que você criar aparecem aqui.'}
        />
      ) : null}

      <Botao titulo="Configurações" variante="secundario" aoPressionar={() => router.push('/configuracoes')} />
    </Tela>
  );
}
