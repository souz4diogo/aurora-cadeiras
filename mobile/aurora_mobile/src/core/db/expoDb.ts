import * as SQLite from 'expo-sqlite';

import type { Db, ParametroSql } from '@/core/db/db';
import { migrar } from '@/core/db/migracoes';

const NOME_DO_BANCO = 'aurora.db';

/** Adaptador do expo-sqlite para a interface Db. */
export class ExpoDb implements Db {
  constructor(private readonly banco: SQLite.SQLiteDatabase) {}

  async executar(sql: string, parametros: ParametroSql[] = []): Promise<void> {
    await this.banco.runAsync(sql, parametros);
  }

  consultar<T>(sql: string, parametros: ParametroSql[] = []): Promise<T[]> {
    return this.banco.getAllAsync<T>(sql, parametros);
  }

  primeiro<T>(sql: string, parametros: ParametroSql[] = []): Promise<T | null> {
    return this.banco.getFirstAsync<T>(sql, parametros);
  }

  script(sql: string): Promise<void> {
    return this.banco.execAsync(sql);
  }

  async transacao<T>(operacao: () => Promise<T>): Promise<T> {
    let resultado!: T;
    await this.banco.withTransactionAsync(async () => {
      resultado = await operacao();
    });
    return resultado;
  }
}

/** Abre o banco do aparelho e aplica as migrações pendentes. */
export async function abrirBancoLocal(): Promise<Db> {
  const banco = await SQLite.openDatabaseAsync(NOME_DO_BANCO);
  // WAL deixa a leitura da tela correr enquanto a sincronização grava
  await banco.execAsync('PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;');
  const db = new ExpoDb(banco);
  await migrar(db);
  return db;
}
