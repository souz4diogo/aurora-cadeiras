import type { AxiosInstance } from 'axios';

import type { Perfil } from '@/core/auth/tokenStore';
import type { CacheJsonRepository } from '@/core/db/cacheJsonRepository';
import { consultarComCache, type ResultadoDaConsulta } from '@/shared/consultaComCache';

export interface PedidoResumo {
  id: number;
  numero: string;
  clienteNome: string;
  dataPedido: string;
  prazoEntrega: string;
  status: string;
  valorTotal: number;
  quantidadeItens: number;
  atrasado: boolean;
}

export interface PedidoItem {
  id: number;
  produtoCodigo: string;
  produtoNome: string;
  quantidade: number;
  precoUnitario: number;
  subtotal: number;
}

export interface EventoDoPedido {
  dataHora: string;
  evento: string;
  numeroOp: string | null;
}

/** Pedido completo, como GET /pedidos/{id}/acompanhamento o devolve. */
export interface PedidoDetalhe {
  id: number;
  numero: string;
  clienteNome: string;
  dataPedido: string;
  prazoEntrega: string;
  status: string;
  observacao: string | null;
  valorTotal: number;
  atrasado: boolean;
  itens: PedidoItem[];
}

export interface Acompanhamento {
  pedido: PedidoDetalhe;
  eventos: EventoDoPedido[];
}

/** Pedidos para o vendedor (os que criou) e para o cliente (os dele), com linha do tempo da produção (RF22). */
export class PedidosService {
  constructor(
    private readonly http: AxiosInstance,
    private readonly cache: CacheJsonRepository,
  ) {}

  /** O cliente usa /meus-pedidos; o vendedor, /pedidos (a API já restringe o vendedor aos próprios pedidos). */
  listar(perfil: Perfil): Promise<ResultadoDaConsulta<PedidoResumo[]>> {
    const rota = perfil === 'Cliente' ? '/meus-pedidos' : '/pedidos';
    return consultarComCache(this.cache, `pedidos:${rota}`, async () => {
      const { data } = await this.http.get<{ items: PedidoResumo[] }>(rota, { params: { page: 1, pageSize: 50 } });
      return data.items;
    });
  }

  acompanhar(pedidoId: number): Promise<ResultadoDaConsulta<Acompanhamento>> {
    return consultarComCache(this.cache, `pedido:${pedidoId}`, async () => {
      const { data } = await this.http.get<Acompanhamento>(`/pedidos/${pedidoId}/acompanhamento`);
      return data;
    });
  }
}
