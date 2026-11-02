import type { Db } from '@/core/db/db';
import type { ApontamentoPendente, ContagemDaFila, NovoApontamento } from '@/features/sync/tipos';

interface Linha {
  client_request_id: string;
  tipo: ApontamentoPendente['tipo'];
  etapa_id: number;
  numero_op: string;
  estacao_nome: string;
  data_hora_cliente: string;
  tentativas: number;
  estado: ApontamentoPendente['estado'];
  codigo: string | null;
  mensagem: string | null;
}

const COLUNAS =
  'client_request_id, tipo, etapa_id, numero_op, estacao_nome, data_hora_cliente, tentativas, estado, codigo, mensagem';

function paraApontamento(l: Linha): ApontamentoPendente {
  return {
    clientRequestId: l.client_request_id,
    tipo: l.tipo,
    etapaId: l.etapa_id,
    numeroOp: l.numero_op,
    estacaoNome: l.estacao_nome,
    dataHoraCliente: l.data_hora_cliente,
    tentativas: l.tentativas,
    estado: l.estado,
    codigo: l.codigo,
    mensagem: l.mensagem,
  };
}

/** `?, ?, ?` para uma lista; a lista nunca vai dentro do texto do comando. */
function marcas(valores: readonly unknown[]): string {
  return valores.map(() => '?').join(', ');
}

/** Fila local de apontamentos (tabela apontamento_pendente): grava antes de enviar, remove só ao confirmar. */
export class FilaRepository {
  constructor(private readonly db: Db) {}

  async enfileirar(item: NovoApontamento): Promise<void> {
    await this.db.executar(
      `INSERT INTO apontamento_pendente
         (client_request_id, tipo, etapa_id, numero_op, estacao_nome, data_hora_cliente, estado)
       VALUES (?, ?, ?, ?, ?, ?, 'Pendente')`,
      [item.clientRequestId, item.tipo, item.etapaId, item.numeroOp, item.estacaoNome, item.dataHoraCliente],
    );
  }

  /** Os pendentes mais antigos primeiro, pela hora em que o operador agiu (docs/08 §5). */
  async proximosPendentes(limite: number): Promise<ApontamentoPendente[]> {
    const linhas = await this.db.consultar<Linha>(
      `SELECT ${COLUNAS} FROM apontamento_pendente
        WHERE estado = 'Pendente'
        ORDER BY data_hora_cliente, rowid
        LIMIT ?`,
      [limite],
    );
    return linhas.map(paraApontamento);
  }

  /** Tudo o que a tela da fila mostra: pendentes, em envio e rejeitados, do mais recente ao mais antigo. */
  async listar(): Promise<ApontamentoPendente[]> {
    const linhas = await this.db.consultar<Linha>(
      `SELECT ${COLUNAS} FROM apontamento_pendente ORDER BY data_hora_cliente DESC, rowid DESC`,
    );
    return linhas.map(paraApontamento);
  }

  /** Itens que ainda valem para o estado local da etapa (pendentes e em envio; rejeitado não vale). */
  async listarAtivos(): Promise<ApontamentoPendente[]> {
    const linhas = await this.db.consultar<Linha>(
      `SELECT ${COLUNAS} FROM apontamento_pendente
        WHERE estado IN ('Pendente', 'Enviando')
        ORDER BY data_hora_cliente, rowid`,
    );
    return linhas.map(paraApontamento);
  }

  async contar(): Promise<ContagemDaFila> {
    const linhas = await this.db.consultar<{ estado: string; total: number }>(
      'SELECT estado, COUNT(*) AS total FROM apontamento_pendente GROUP BY estado',
    );
    const por = (estado: string) => linhas.find((l) => l.estado === estado)?.total ?? 0;
    return { pendentes: por('Pendente'), enviando: por('Enviando'), rejeitados: por('Rejeitado') };
  }

  async marcarEnviando(ids: readonly string[]): Promise<void> {
    if (ids.length === 0) return;
    await this.db.executar(
      `UPDATE apontamento_pendente SET estado = 'Enviando' WHERE client_request_id IN (${marcas(ids)})`,
      [...ids],
    );
  }

  /** A rede caiu antes da resposta: volta para a fila, sem perder o item. */
  async devolverParaFila(ids: readonly string[]): Promise<void> {
    if (ids.length === 0) return;
    await this.db.executar(
      `UPDATE apontamento_pendente SET estado = 'Pendente', tentativas = tentativas + 1
        WHERE client_request_id IN (${marcas(ids)}) AND estado = 'Enviando'`,
      [...ids],
    );
  }

  /** O servidor confirmou (aplicado ou já processado): sai da fila. */
  async remover(ids: readonly string[]): Promise<void> {
    if (ids.length === 0) return;
    await this.db.executar(
      `DELETE FROM apontamento_pendente WHERE client_request_id IN (${marcas(ids)})`,
      [...ids],
    );
  }

  async marcarRejeitado(id: string, codigo: string, mensagem: string): Promise<void> {
    await this.db.executar(
      `UPDATE apontamento_pendente
          SET estado = 'Rejeitado', codigo = ?, mensagem = ?, tentativas = tentativas + 1
        WHERE client_request_id = ?`,
      [codigo, mensagem, id],
    );
  }

  /** O usuário corrigiu a causa (ex.: entrou saldo) e quer tentar o mesmo item de novo, com o mesmo id. */
  async reenviar(id: string): Promise<void> {
    await this.db.executar(
      `UPDATE apontamento_pendente SET estado = 'Pendente', codigo = NULL, mensagem = NULL
        WHERE client_request_id = ? AND estado = 'Rejeitado'`,
      [id],
    );
  }

  /** O usuário desistiu de um item rejeitado. */
  async descartar(id: string): Promise<void> {
    await this.db.executar(
      `DELETE FROM apontamento_pendente WHERE client_request_id = ? AND estado = 'Rejeitado'`,
      [id],
    );
  }

  /** Ao abrir o app: o que ficou "Enviando" por um fechamento no meio do envio volta a Pendente. O servidor é idempotente. */
  async recuperarEnvioInterrompido(): Promise<void> {
    await this.db.executar(`UPDATE apontamento_pendente SET estado = 'Pendente' WHERE estado = 'Enviando'`);
  }
}
