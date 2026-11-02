export type ParametroSql = string | number | null;

/**
 * Acesso mínimo ao SQLite local. Os repositórios só conhecem esta interface: no aparelho ela é o expo-sqlite
 * (ver expoDb.ts) e nos testes é o SQLite embutido do Node, então o SQL dos repositórios roda de verdade.
 * Todo valor vai como parâmetro, nunca concatenado no texto do comando.
 */
export interface Db {
  executar(sql: string, parametros?: ParametroSql[]): Promise<void>;
  consultar<T>(sql: string, parametros?: ParametroSql[]): Promise<T[]>;
  primeiro<T>(sql: string, parametros?: ParametroSql[]): Promise<T | null>;
  /** Várias instruções de uma vez, sem parâmetros (usado pelas migrações). */
  script(sql: string): Promise<void>;
  /** Tudo ou nada: se o callback lançar, as alterações são desfeitas. */
  transacao<T>(operacao: () => Promise<T>): Promise<T>;
}
