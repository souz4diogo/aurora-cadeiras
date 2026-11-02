export type TipoDeApontamento = 'Inicio' | 'Fim';

/** Situação de um apontamento na fila local (docs/08 §5). */
export type EstadoDoApontamento = 'Pendente' | 'Enviando' | 'Rejeitado';

export interface ApontamentoPendente {
  clientRequestId: string;
  tipo: TipoDeApontamento;
  etapaId: number;
  numeroOp: string;
  estacaoNome: string;
  /** Quando o operador tocou no botão, pelo relógio do aparelho (ISO 8601 UTC). */
  dataHoraCliente: string;
  tentativas: number;
  estado: EstadoDoApontamento;
  codigo: string | null;
  mensagem: string | null;
}

export type NovoApontamento = Pick<
  ApontamentoPendente,
  'clientRequestId' | 'tipo' | 'etapaId' | 'numeroOp' | 'estacaoNome' | 'dataHoraCliente'
>;

export interface ContagemDaFila {
  pendentes: number;
  enviando: number;
  rejeitados: number;
}

/** Item do lote enviado a POST /apontamentos/sincronizar. */
export interface ItemDoLote {
  clientRequestId: string;
  tipo: TipoDeApontamento;
  etapaId: number;
  dataHoraCliente: string;
}

export type StatusDoItem = 'Aplicado' | 'JaProcessado' | 'Rejeitado';

export interface ResultadoDoItem {
  clientRequestId: string;
  status: StatusDoItem;
  code?: string | null;
  mensagem?: string | null;
}

export interface ResumoDaSincronizacao {
  /** Itens aceitos pelo servidor (aplicados agora ou já processados antes). */
  confirmados: number;
  rejeitados: number;
  /** Itens que continuam na fila esperando rede. */
  aindaPendentes: number;
  /** A rodada parou porque a rede caiu. */
  interrompidaPorRede: boolean;
}
