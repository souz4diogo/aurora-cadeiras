import type { Db } from '@/core/db/db';

/**
 * Migrações do banco local, em ordem. Nunca altere uma que já saiu: acrescente a próxima.
 * A versão aplicada fica em PRAGMA user_version.
 */
export const MIGRACOES: readonly string[] = [
  `
  CREATE TABLE etapa_cache (
    etapa_id           INTEGER PRIMARY KEY,
    estacao_id         INTEGER NOT NULL,
    ordem_producao_id  INTEGER NOT NULL,
    numero_op          TEXT    NOT NULL,
    codigo_qr          TEXT    NOT NULL,
    pedido_numero      TEXT    NOT NULL,
    produto_id         INTEGER NOT NULL,
    produto_codigo     TEXT    NOT NULL,
    produto_nome       TEXT    NOT NULL,
    quantidade         INTEGER NOT NULL,
    previsao_conclusao TEXT,
    estacao_nome       TEXT    NOT NULL,
    sequencia          INTEGER NOT NULL,
    status             TEXT    NOT NULL,
    liberada           INTEGER NOT NULL,
    iniciada_em        TEXT,
    atualizado_em      TEXT    NOT NULL
  );
  CREATE INDEX ix_etapa_cache_estacao ON etapa_cache (estacao_id);

  CREATE TABLE apontamento_pendente (
    client_request_id TEXT    PRIMARY KEY,
    tipo              TEXT    NOT NULL CHECK (tipo IN ('Inicio', 'Fim')),
    etapa_id          INTEGER NOT NULL,
    numero_op         TEXT    NOT NULL,
    estacao_nome      TEXT    NOT NULL,
    data_hora_cliente TEXT    NOT NULL,
    tentativas        INTEGER NOT NULL DEFAULT 0,
    estado            TEXT    NOT NULL CHECK (estado IN ('Pendente', 'Enviando', 'Rejeitado')),
    codigo            TEXT,
    mensagem          TEXT
  );
  CREATE INDEX ix_apontamento_pendente_estado ON apontamento_pendente (estado, data_hora_cliente);

  CREATE TABLE cache_json (
    chave         TEXT PRIMARY KEY,
    json          TEXT NOT NULL,
    atualizado_em TEXT NOT NULL
  );

  CREATE TABLE preferencia (
    chave TEXT PRIMARY KEY,
    valor TEXT NOT NULL
  );
  `,
  // 2: cadastro de cliente pelo Vendedor, gravado primeiro no aparelho e enviado depois (A60, docs/08 §1.1)
  `
  CREATE TABLE cliente_pendente (
    id         TEXT    PRIMARY KEY,
    nome       TEXT    NOT NULL,
    cpf_cnpj   TEXT    NOT NULL UNIQUE,
    email      TEXT,
    telefone   TEXT,
    cidade     TEXT,
    uf         TEXT,
    criado_em  TEXT    NOT NULL,
    tentativas INTEGER NOT NULL DEFAULT 0,
    estado     TEXT    NOT NULL CHECK (estado IN ('Pendente', 'Enviando', 'Rejeitado')),
    codigo     TEXT,
    mensagem   TEXT
  );
  CREATE INDEX ix_cliente_pendente_estado ON cliente_pendente (estado, criado_em);
  `,
];

export async function migrar(db: Db): Promise<void> {
  const atual = (await db.primeiro<{ user_version: number }>('PRAGMA user_version'))?.user_version ?? 0;
  for (let versao = atual; versao < MIGRACOES.length; versao++) {
    await db.transacao(async () => {
      await db.script(MIGRACOES[versao]);
      // PRAGMA não aceita parâmetro; o número vem do laço, não de entrada externa
      await db.script(`PRAGMA user_version = ${versao + 1}`);
    });
  }
}

/** Preferências que sobrevivem ao logout (não são dados da sessão). */
export const PREFERENCIAS_QUE_FICAM = ['escala_fonte', 'alto_contraste'];

/** Logout: apaga os dados da sessão do aparelho (docs/08 §4 e §9). */
export async function apagarDadosLocais(db: Db): Promise<void> {
  const marcas = PREFERENCIAS_QUE_FICAM.map(() => '?').join(', ');
  await db.transacao(async () => {
    await db.executar('DELETE FROM etapa_cache');
    await db.executar('DELETE FROM apontamento_pendente');
    await db.executar('DELETE FROM cliente_pendente');
    await db.executar('DELETE FROM cache_json');
    await db.executar(`DELETE FROM preferencia WHERE chave NOT IN (${marcas})`, PREFERENCIAS_QUE_FICAM);
  });
}
