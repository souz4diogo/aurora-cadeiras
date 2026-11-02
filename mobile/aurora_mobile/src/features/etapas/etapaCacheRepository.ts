import type { Db } from '@/core/db/db';
import type { EtapaDaEstacao, StatusDaEtapa } from '@/features/etapas/tipos';

interface Linha {
  etapa_id: number;
  estacao_id: number;
  ordem_producao_id: number;
  numero_op: string;
  codigo_qr: string;
  pedido_numero: string;
  produto_id: number;
  produto_codigo: string;
  produto_nome: string;
  quantidade: number;
  previsao_conclusao: string | null;
  estacao_nome: string;
  sequencia: number;
  status: StatusDaEtapa;
  liberada: number;
  iniciada_em: string | null;
  atualizado_em: string;
}

const COLUNAS = `etapa_id, estacao_id, ordem_producao_id, numero_op, codigo_qr, pedido_numero, produto_id,
  produto_codigo, produto_nome, quantidade, previsao_conclusao, estacao_nome, sequencia, status, liberada,
  iniciada_em, atualizado_em`;

function paraEtapa(l: Linha): EtapaDaEstacao {
  return {
    etapaId: l.etapa_id,
    ordemProducaoId: l.ordem_producao_id,
    numeroOp: l.numero_op,
    codigoQr: l.codigo_qr,
    pedidoNumero: l.pedido_numero,
    produtoId: l.produto_id,
    produtoCodigo: l.produto_codigo,
    produtoNome: l.produto_nome,
    quantidade: l.quantidade,
    previsaoConclusao: l.previsao_conclusao,
    estacaoId: l.estacao_id,
    estacaoNome: l.estacao_nome,
    sequencia: l.sequencia,
    status: l.status,
    iniciadaEm: l.iniciada_em,
    operadorId: null,
    liberada: l.liberada === 1,
  };
}

/** Cache local das etapas já carregadas (tabela etapa_cache): permite ver e apontar sem rede. */
export class EtapaCacheRepository {
  constructor(private readonly db: Db) {}

  /** A lista nova do servidor substitui a da estação inteira: o servidor é a fonte da verdade. */
  async substituirDaEstacao(estacaoId: number, etapas: readonly EtapaDaEstacao[], agoraIso: string): Promise<void> {
    await this.db.transacao(async () => {
      await this.db.executar('DELETE FROM etapa_cache WHERE estacao_id = ?', [estacaoId]);
      for (const etapa of etapas) {
        await this.gravar(etapa, agoraIso);
      }
    });
  }

  /** Grava uma etapa avulsa (a que o QR code abriu). */
  async guardar(etapa: EtapaDaEstacao, agoraIso: string): Promise<void> {
    await this.db.executar('DELETE FROM etapa_cache WHERE etapa_id = ?', [etapa.etapaId]);
    await this.gravar(etapa, agoraIso);
  }

  /**
   * Reflete no cache o que o servidor acabou de confirmar. Sem isto, no instante em que o item sai da fila a tela
   * perderia a sobreposição e voltaria a mostrar a etapa como pendente (até o próximo recarregamento do servidor).
   */
  async aplicarApontamento(etapaId: number, tipo: 'Inicio' | 'Fim', dataHoraIso: string): Promise<void> {
    if (tipo === 'Inicio') {
      await this.db.executar(
        `UPDATE etapa_cache SET status = 'EmAndamento', iniciada_em = COALESCE(iniciada_em, ?)
          WHERE etapa_id = ? AND status = 'Pendente'`,
        [dataHoraIso, etapaId],
      );
    } else {
      await this.db.executar(`UPDATE etapa_cache SET status = 'Concluida' WHERE etapa_id = ?`, [etapaId]);
    }
  }

  async listarDaEstacao(estacaoId: number): Promise<EtapaDaEstacao[]> {
    const linhas = await this.db.consultar<Linha>(
      `SELECT ${COLUNAS} FROM etapa_cache WHERE estacao_id = ?
        ORDER BY (status = 'EmAndamento') DESC, liberada DESC, previsao_conclusao IS NULL, previsao_conclusao, etapa_id`,
      [estacaoId],
    );
    return linhas.map(paraEtapa);
  }

  async obter(etapaId: number): Promise<EtapaDaEstacao | null> {
    const linha = await this.db.primeiro<Linha>(`SELECT ${COLUNAS} FROM etapa_cache WHERE etapa_id = ?`, [etapaId]);
    return linha ? paraEtapa(linha) : null;
  }

  /** Quando a lista da estação foi atualizada pela última vez (ISO), para mostrar "atualizado às ...". */
  async ultimaAtualizacao(estacaoId: number): Promise<string | null> {
    const linha = await this.db.primeiro<{ quando: string | null }>(
      'SELECT MAX(atualizado_em) AS quando FROM etapa_cache WHERE estacao_id = ?',
      [estacaoId],
    );
    return linha?.quando ?? null;
  }

  private async gravar(e: EtapaDaEstacao, agoraIso: string): Promise<void> {
    await this.db.executar(
      `INSERT INTO etapa_cache (${COLUNAS})
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      [
        e.etapaId,
        e.estacaoId,
        e.ordemProducaoId,
        e.numeroOp,
        e.codigoQr,
        e.pedidoNumero,
        e.produtoId,
        e.produtoCodigo,
        e.produtoNome,
        e.quantidade,
        e.previsaoConclusao,
        e.estacaoNome,
        e.sequencia,
        e.status,
        e.liberada ? 1 : 0,
        e.iniciadaEm,
        agoraIso,
      ],
    );
  }
}
