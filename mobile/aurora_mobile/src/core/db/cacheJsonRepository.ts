import type { Db } from '@/core/db/db';

export interface Guardado<T> {
  valor: T;
  /** Quando o servidor devolveu esse dado (ISO), para a tela mostrar "atualizado às ...". */
  atualizadoEm: string;
}

/**
 * Cache de leitura (tabela cache_json): guarda a última resposta conhecida de listas e consultas, como o saldo de
 * estoque e os pedidos, para a tela mostrar algo útil sem rede (docs/08 §5, "Prevenção").
 */
export class CacheJsonRepository {
  constructor(
    private readonly db: Db,
    private readonly agora: () => Date = () => new Date(),
  ) {}

  async guardar<T>(chave: string, valor: T): Promise<string> {
    const atualizadoEm = this.agora().toISOString();
    await this.db.executar(
      `INSERT INTO cache_json (chave, json, atualizado_em) VALUES (?, ?, ?)
       ON CONFLICT (chave) DO UPDATE SET json = excluded.json, atualizado_em = excluded.atualizado_em`,
      [chave, JSON.stringify(valor), atualizadoEm],
    );
    return atualizadoEm;
  }

  async obter<T>(chave: string): Promise<Guardado<T> | null> {
    const linha = await this.db.primeiro<{ json: string; atualizado_em: string }>(
      'SELECT json, atualizado_em FROM cache_json WHERE chave = ?',
      [chave],
    );
    if (!linha) return null;
    try {
      return { valor: JSON.parse(linha.json) as T, atualizadoEm: linha.atualizado_em };
    } catch {
      return null;
    }
  }
}
