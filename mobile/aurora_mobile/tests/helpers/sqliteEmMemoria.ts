import { DatabaseSync, type SQLInputValue } from 'node:sqlite';

import type { Db, ParametroSql } from '@/core/db/db';
import { migrar } from '@/core/db/migracoes';

/**
 * Db de teste sobre o SQLite embutido do Node: o SQL dos repositórios e das migrações roda de verdade, só não
 * passa pelo expo-sqlite (que precisa do aparelho). Cada chamada cria um banco novo e isolado, em memória.
 */
class SqliteEmMemoria implements Db {
  private readonly banco = new DatabaseSync(':memory:');
  private profundidade = 0;

  async executar(sql: string, parametros: ParametroSql[] = []): Promise<void> {
    this.banco.prepare(sql).run(...(parametros as SQLInputValue[]));
  }

  async consultar<T>(sql: string, parametros: ParametroSql[] = []): Promise<T[]> {
    return this.banco
      .prepare(sql)
      .all(...(parametros as SQLInputValue[]))
      .map((linha) => ({ ...linha }) as T);
  }

  async primeiro<T>(sql: string, parametros: ParametroSql[] = []): Promise<T | null> {
    const linha = this.banco.prepare(sql).get(...(parametros as SQLInputValue[]));
    return linha ? ({ ...linha } as T) : null;
  }

  async script(sql: string): Promise<void> {
    this.banco.exec(sql);
  }

  async transacao<T>(operacao: () => Promise<T>): Promise<T> {
    // O expo-sqlite não aninha transações; aqui a mais externa é a que vale
    if (this.profundidade > 0) return operacao();
    this.banco.exec('BEGIN');
    this.profundidade++;
    try {
      const resultado = await operacao();
      this.banco.exec('COMMIT');
      return resultado;
    } catch (erro) {
      this.banco.exec('ROLLBACK');
      throw erro;
    } finally {
      this.profundidade--;
    }
  }
}

export async function criarBancoDeTeste(): Promise<Db> {
  const db = new SqliteEmMemoria();
  await migrar(db);
  return db;
}
