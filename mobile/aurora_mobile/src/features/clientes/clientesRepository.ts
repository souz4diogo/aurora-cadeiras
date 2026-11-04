import type { Db } from '@/core/db/db';
import type { ClientePendente, ContagemDeClientes } from '@/features/clientes/tipos';

interface Linha {
  id: string;
  nome: string;
  cpf_cnpj: string;
  email: string | null;
  telefone: string | null;
  cidade: string | null;
  uf: string | null;
  criado_em: string;
  tentativas: number;
  estado: ClientePendente['estado'];
  codigo: string | null;
  mensagem: string | null;
}

const COLUNAS = 'id, nome, cpf_cnpj, email, telefone, cidade, uf, criado_em, tentativas, estado, codigo, mensagem';

function paraCliente(l: Linha): ClientePendente {
  return {
    id: l.id,
    nome: l.nome,
    cpfCnpj: l.cpf_cnpj,
    email: l.email,
    telefone: l.telefone,
    cidade: l.cidade,
    uf: l.uf,
    criadoEm: l.criado_em,
    tentativas: l.tentativas,
    estado: l.estado,
    codigo: l.codigo,
    mensagem: l.mensagem,
  };
}

export type NovoClientePendente = Pick<
  ClientePendente,
  'id' | 'nome' | 'cpfCnpj' | 'email' | 'telefone' | 'cidade' | 'uf' | 'criadoEm'
>;

/** Clientes cadastrados no aparelho (tabela cliente_pendente): grava antes de enviar, remove só ao confirmar. */
export class ClientesRepository {
  constructor(private readonly db: Db) {}

  async gravar(cliente: NovoClientePendente): Promise<void> {
    await this.db.executar(
      `INSERT INTO cliente_pendente (id, nome, cpf_cnpj, email, telefone, cidade, uf, criado_em, estado)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Pendente')`,
      [cliente.id, cliente.nome, cliente.cpfCnpj, cliente.email, cliente.telefone, cliente.cidade, cliente.uf, cliente.criadoEm],
    );
  }

  async existeDocumento(cpfCnpj: string): Promise<boolean> {
    return (await this.db.primeiro('SELECT 1 AS um FROM cliente_pendente WHERE cpf_cnpj = ?', [cpfCnpj])) !== null;
  }

  /** Do mais antigo ao mais novo: a ordem em que o Vendedor cadastrou. */
  async proximosPendentes(limite: number): Promise<ClientePendente[]> {
    const linhas = await this.db.consultar<Linha>(
      `SELECT ${COLUNAS} FROM cliente_pendente WHERE estado = 'Pendente' ORDER BY criado_em, rowid LIMIT ?`,
      [limite],
    );
    return linhas.map(paraCliente);
  }

  async listar(): Promise<ClientePendente[]> {
    const linhas = await this.db.consultar<Linha>(
      `SELECT ${COLUNAS} FROM cliente_pendente ORDER BY criado_em DESC, rowid DESC`,
    );
    return linhas.map(paraCliente);
  }

  async contar(): Promise<ContagemDeClientes> {
    const linhas = await this.db.consultar<{ estado: string; total: number }>(
      'SELECT estado, COUNT(*) AS total FROM cliente_pendente GROUP BY estado',
    );
    const por = (estado: string) => linhas.find((l) => l.estado === estado)?.total ?? 0;
    return { pendentes: por('Pendente'), enviando: por('Enviando'), rejeitados: por('Rejeitado') };
  }

  async marcarEnviando(id: string): Promise<void> {
    await this.db.executar(`UPDATE cliente_pendente SET estado = 'Enviando' WHERE id = ?`, [id]);
  }

  /** Sem resposta (rede caiu ou erro do servidor): volta para a fila, sem perder o cliente. */
  async devolverParaFila(id: string): Promise<void> {
    await this.db.executar(
      `UPDATE cliente_pendente SET estado = 'Pendente', tentativas = tentativas + 1 WHERE id = ? AND estado = 'Enviando'`,
      [id],
    );
  }

  /** O servidor aceitou: sai do aparelho (a fonte da verdade passa a ser o servidor). */
  async remover(id: string): Promise<void> {
    await this.db.executar('DELETE FROM cliente_pendente WHERE id = ?', [id]);
  }

  async marcarRejeitado(id: string, codigo: string, mensagem: string): Promise<void> {
    await this.db.executar(
      `UPDATE cliente_pendente SET estado = 'Rejeitado', codigo = ?, mensagem = ?, tentativas = tentativas + 1 WHERE id = ?`,
      [codigo, mensagem, id],
    );
  }

  async reenviar(id: string): Promise<void> {
    await this.db.executar(
      `UPDATE cliente_pendente SET estado = 'Pendente', codigo = NULL, mensagem = NULL WHERE id = ? AND estado = 'Rejeitado'`,
      [id],
    );
  }

  async descartar(id: string): Promise<void> {
    await this.db.executar(`DELETE FROM cliente_pendente WHERE id = ? AND estado = 'Rejeitado'`, [id]);
  }

  async recuperarEnvioInterrompido(): Promise<void> {
    await this.db.executar(`UPDATE cliente_pendente SET estado = 'Pendente' WHERE estado = 'Enviando'`);
  }
}
