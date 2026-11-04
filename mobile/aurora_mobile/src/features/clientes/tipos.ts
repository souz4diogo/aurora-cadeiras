export type EstadoDoCliente = 'Pendente' | 'Enviando' | 'Rejeitado';

/** Campos do formulário, como o Vendedor digita (tudo texto). */
export interface DadosDoCliente {
  nome: string;
  cpfCnpj: string;
  email: string;
  telefone: string;
  cidade: string;
  uf: string;
}

/** Cliente gravado no aparelho, esperando envio (tabela cliente_pendente). */
export interface ClientePendente {
  id: string;
  nome: string;
  /** Só dígitos. */
  cpfCnpj: string;
  email: string | null;
  telefone: string | null;
  cidade: string | null;
  uf: string | null;
  criadoEm: string;
  tentativas: number;
  estado: EstadoDoCliente;
  codigo: string | null;
  mensagem: string | null;
}

export interface ContagemDeClientes {
  pendentes: number;
  enviando: number;
  rejeitados: number;
}

export interface ResumoDosClientes {
  confirmados: number;
  rejeitados: number;
  interrompidaPorRede: boolean;
}
