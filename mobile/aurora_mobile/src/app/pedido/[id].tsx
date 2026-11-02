import { useLocalSearchParams } from 'expo-router';

import { TelaDoPedido } from '@/features/pedidos/TelaDoPedido';

export default function Pedido() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <TelaDoPedido pedidoId={Number(id)} />;
}
