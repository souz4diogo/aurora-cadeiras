import type { Db } from '@/core/db/db';

/** Preferências do aparelho (tabela preferencia): estação escolhida, tamanho da fonte, tema. */
export class PreferenciasRepository {
  constructor(private readonly db: Db) {}

  async obter(chave: string): Promise<string | null> {
    const linha = await this.db.primeiro<{ valor: string }>('SELECT valor FROM preferencia WHERE chave = ?', [chave]);
    return linha?.valor ?? null;
  }

  async definir(chave: string, valor: string): Promise<void> {
    await this.db.executar(
      'INSERT INTO preferencia (chave, valor) VALUES (?, ?) ON CONFLICT (chave) DO UPDATE SET valor = excluded.valor',
      [chave, valor],
    );
  }

  async remover(chave: string): Promise<void> {
    await this.db.executar('DELETE FROM preferencia WHERE chave = ?', [chave]);
  }
}

export const CHAVES = {
  estacao: 'estacao_id',
  escalaDaFonte: 'escala_fonte',
  altoContraste: 'alto_contraste',
} as const;

export const ESCALAS_DE_FONTE = [1, 1.15, 1.3] as const;
export type EscalaDeFonte = (typeof ESCALAS_DE_FONTE)[number];
