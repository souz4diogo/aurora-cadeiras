export type StatusDaEtapa = 'Pendente' | 'EmAndamento' | 'Concluida';

/** Etapa da fila de uma estação (GET /etapas/minhas), igual ao DTO da API. */
export interface EtapaDaEstacao {
  etapaId: number;
  ordemProducaoId: number;
  numeroOp: string;
  codigoQr: string;
  pedidoNumero: string;
  produtoId: number;
  produtoCodigo: string;
  produtoNome: string;
  quantidade: number;
  previsaoConclusao: string | null;
  estacaoId: number;
  estacaoNome: string;
  sequencia: number;
  status: StatusDaEtapa;
  iniciadaEm: string | null;
  operadorId: number | null;
  /** As etapas anteriores da OP já terminaram: só então esta pode ser iniciada (RN05). */
  liberada: boolean;
}

/** A etapa como a tela mostra: o dado do servidor mais o que o operador já fez e ainda não foi enviado. */
export interface EtapaNaTela extends EtapaDaEstacao {
  pendenteEnvio: boolean;
}

export interface Estacao {
  id: number;
  nome: string;
  ordem: number;
  geraEtapa: boolean;
}

export interface MaterialDaEtapa {
  materiaPrimaId: number;
  materiaPrimaCodigo: string;
  materiaPrimaNome: string;
  unidade: string;
  estacaoId: number;
  quantidade: number;
}

/** Parte do detalhe da OP (GET /ordens-producao/{id} e /qr/{codigo}) que o app usa. */
export interface OrdemProducaoDetalhe {
  ordem: {
    id: number;
    numero: string;
    pedidoNumero: string;
    produtoId: number;
    produtoCodigo: string;
    produtoNome: string;
    quantidade: number;
    status: string;
    previsaoConclusao: string | null;
  };
  codigoQr: string;
  clienteNome: string;
  etapas: {
    id: number;
    estacaoId: number;
    estacaoNome: string;
    sequencia: number;
    status: StatusDaEtapa;
    iniciadaEm: string | null;
    operadorId: number | null;
  }[];
}
