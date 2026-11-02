import type { Perfil } from '@/core/auth/tokenStore';
import { acoesDaEtapa } from '@/features/etapas/acoes';
import { descreverEtapa } from '@/features/etapas/StatusDaEtapa';
import type { EtapaNaTela } from '@/features/etapas/tipos';
import { mensagemDoApontamento } from '@/features/etapas/useDetalheDaEtapa';
import { extrairCodigoDaOrdem } from '@/features/qr/leituraDeQr';
import { validarPerda } from '@/features/perdas/perdasService';
import { descreverSincronizacao } from '@/features/sync/SeloDeSincronizacao';
import { etapa } from '../helpers/dados';

function naTela(sobrescrever: Partial<EtapaNaTela> = {}): EtapaNaTela {
  return { ...etapa(), pendenteEnvio: false, ...sobrescrever };
}

describe('acoesDaEtapa', () => {
  it('operador inicia a etapa liberada', () => {
    expect(acoesDaEtapa(naTela(), 'Operador')).toMatchObject({ iniciar: true, finalizar: false, registrarPerda: false });
  });

  it('etapa travada pela anterior não pode ser iniciada e explica o motivo', () => {
    const acoes = acoesDaEtapa(naTela({ liberada: false }), 'Operador');

    expect(acoes.iniciar).toBe(false);
    expect(acoes.explicacao).toMatch(/etapa anterior/);
  });

  it('operador finaliza e registra perda enquanto a etapa está em andamento', () => {
    expect(acoesDaEtapa(naTela({ status: 'EmAndamento' }), 'Operador')).toMatchObject({
      iniciar: false,
      finalizar: true,
      registrarPerda: true,
    });
  });

  it('perda e inspeção esperam o início chegar ao servidor', () => {
    const emAndamento = naTela({ status: 'EmAndamento', pendenteEnvio: true });

    expect(acoesDaEtapa(emAndamento, 'Operador').precisaEnviarAntes).toBe(true);
    expect(acoesDaEtapa({ ...emAndamento, estacaoNome: 'Qualidade' }, 'Inspetor').precisaEnviarAntes).toBe(true);
    expect(acoesDaEtapa(naTela({ status: 'EmAndamento' }), 'Operador').precisaEnviarAntes).toBe(false);
  });

  it('inspetor inicia a Qualidade e a conclui pela inspeção, sem botão Finalizar', () => {
    const qualidade = naTela({ estacaoNome: 'Qualidade', status: 'EmAndamento' });

    expect(acoesDaEtapa(naTela({ estacaoNome: 'Qualidade' }), 'Inspetor').iniciar).toBe(true);
    expect(acoesDaEtapa(qualidade, 'Inspetor')).toMatchObject({ inspecionar: true, finalizar: false, registrarPerda: false });
  });

  it('inspetor não finaliza etapa de outra estação', () => {
    const acoes = acoesDaEtapa(naTela({ status: 'EmAndamento' }), 'Inspetor');

    expect(acoes).toMatchObject({ finalizar: false, inspecionar: false });
    expect(acoes.explicacao).toMatch(/operador/);
  });

  it('etapa concluída não oferece ação', () => {
    const acoes = acoesDaEtapa(naTela({ status: 'Concluida' }), 'Operador');

    expect(acoes).toMatchObject({ iniciar: false, finalizar: false, registrarPerda: false, inspecionar: false });
    expect(acoes.explicacao).toMatch(/concluída/);
  });

  it.each<Perfil>(['Vendedor', 'Cliente', 'Administrador', 'GerenteProducao', 'Almoxarife'])(
    '%s não aponta etapas',
    (perfil) => {
      expect(acoesDaEtapa(naTela(), perfil)).toMatchObject({ iniciar: false, finalizar: false, inspecionar: false });
    },
  );
});

describe('descreverEtapa', () => {
  it.each([
    [{ status: 'Pendente', liberada: true, pendenteEnvio: false }, '⏳ Pronta para iniciar'],
    [{ status: 'Pendente', liberada: false, pendenteEnvio: false }, '🔒 Aguardando etapa anterior'],
    [{ status: 'EmAndamento', liberada: true, pendenteEnvio: false }, '▶ Em andamento'],
    [{ status: 'EmAndamento', liberada: true, pendenteEnvio: true }, '▶ Em andamento · ⬆ a enviar'],
    [{ status: 'Concluida', liberada: true, pendenteEnvio: false }, '✔ Concluída'],
  ] as const)('estado %j vira "%s"', (estado, texto) => {
    expect(descreverEtapa(estado)).toBe(texto);
  });
});

describe('extrairCodigoDaOrdem', () => {
  it('aceita o GUID da etiqueta, com espaços e maiúsculas', () => {
    expect(extrairCodigoDaOrdem(' B6D1C0F2-1D1E-4A7E-9D3B-3F7E5C2A9A10 ')).toBe('b6d1c0f2-1d1e-4a7e-9d3b-3f7e5c2a9a10');
  });

  it.each(['', 'https://exemplo.com/produto/123', '7891234567890', 'b6d1c0f2-1d1e-4a7e-9d3b', 'OP-2026-00058'])(
    'recusa qualquer outro conteúdo: "%s"',
    (conteudo) => {
      expect(extrairCodigoDaOrdem(conteudo)).toBeNull();
    },
  );
});

describe('validarPerda', () => {
  it.each([
    ['', 'quebrou', /quantidade/],
    ['0', 'quebrou', /quantidade/],
    ['-3', 'quebrou', /quantidade/],
    ['2,5', 'quebrou', /quantidade/],
    ['abc', 'quebrou', /quantidade/],
    ['100001', 'quebrou', /quantidade/],
    ['3', '   ', /motivo/],
    ['3', 'x'.repeat(201), /200/],
  ])('recusa quantidade "%s" com motivo "%s"', (quantidade, motivo, esperado) => {
    expect(validarPerda(quantidade, motivo)).toMatch(esperado);
  });

  it('aceita quantidade inteira dentro do limite e motivo preenchido', () => {
    expect(validarPerda('3', 'Peça rachada no corte')).toBeNull();
    expect(validarPerda('100000', 'x'.repeat(200))).toBeNull();
  });
});

describe('mensagens', () => {
  it('apontamento confirmado, guardado e recusado dizem coisas diferentes', () => {
    expect(mensagemDoApontamento('Inicio', 'confirmado')).toMatchObject({ tipo: 'sucesso' });
    expect(mensagemDoApontamento('Fim', 'guardado')).toMatchObject({
      tipo: 'aviso',
      texto: 'Fim salvo no aparelho. Será enviado quando houver conexão.',
    });
    expect(mensagemDoApontamento('Inicio', 'rejeitado', 'Saldo insuficiente.')).toEqual({
      tipo: 'erro',
      texto: 'Início recusado pelo servidor: Saldo insuficiente.',
    });
  });

  it('o selo descreve conexão e pendências para o leitor de tela', () => {
    expect(descreverSincronizacao(true, 0, 0)).toBe('Conectado');
    expect(descreverSincronizacao(false, 1, 0)).toBe('Sem conexão · 1 apontamento a enviar');
    expect(descreverSincronizacao(true, 3, 2)).toBe('Conectado · 3 apontamentos a enviar · 2 rejeitados');
  });
});
