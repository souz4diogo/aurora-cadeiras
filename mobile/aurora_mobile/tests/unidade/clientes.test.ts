import type { Db } from '@/core/db/db';
import { apagarDadosLocais } from '@/core/db/migracoes';
import { ApiError } from '@/core/http/problem';
import { ClientesRepository } from '@/features/clientes/clientesRepository';
import { ClientesService, validarCliente, type ClientesApi } from '@/features/clientes/clientesService';
import type { DadosDoCliente } from '@/features/clientes/tipos';
import { FilaRepository } from '@/features/sync/filaRepository';
import { SyncService } from '@/features/sync/syncService';
import { criarBancoDeTeste } from '../helpers/sqliteEmMemoria';
import { ServidorDeSincronizacaoFalso, erroDeRede } from '../helpers/servidorFalso';

const VALIDO: DadosDoCliente = {
  nome: '  Móveis Lima  ',
  cpfCnpj: '12.345.678/0001-95',
  email: ' contato@lima.com.br ',
  telefone: '',
  cidade: 'Campinas',
  uf: 'sp',
};

let db: Db;
let repo: ClientesRepository;
let api: { criar: jest.Mock; buscar: jest.Mock };
let servico: ClientesService;
let contador: number;

beforeEach(async () => {
  db = await criarBancoDeTeste();
  repo = new ClientesRepository(db);
  api = { criar: jest.fn(async () => undefined), buscar: jest.fn(async () => []) };
  contador = 0;
  servico = new ClientesService({
    repo,
    api: api as unknown as ClientesApi,
    novoId: () => `cli-${++contador}`,
    agora: () => new Date(Date.UTC(2026, 10, 3, 12, contador)),
  });
});

describe('validarCliente (mesmas regras da API)', () => {
  it('aceita um cadastro completo e um só com nome e documento', () => {
    expect(validarCliente(VALIDO)).toEqual({});
    expect(validarCliente({ nome: 'Ana', cpfCnpj: '12345678901', email: '', telefone: '', cidade: '', uf: '' })).toEqual({});
  });

  it('recusa nome vazio, documento com tamanho errado, e-mail e UF inválidos e textos longos', () => {
    const erros = validarCliente({
      nome: ' ',
      cpfCnpj: '123',
      email: 'sem-arroba',
      telefone: 'x'.repeat(21),
      cidade: 'y'.repeat(81),
      uf: 'SPP',
    });

    expect(Object.keys(erros).sort()).toEqual(['cidade', 'cpfCnpj', 'email', 'nome', 'telefone', 'uf']);
  });

  it('o documento pode vir com máscara: conta só os dígitos', () => {
    expect(validarCliente({ ...VALIDO, cpfCnpj: '123.456.789-01' }).cpfCnpj).toBeUndefined();
    expect(validarCliente({ ...VALIDO, cpfCnpj: '123.456.789-0' }).cpfCnpj).toBeDefined();
  });
});

describe('cadastrar (grava primeiro no SQLite)', () => {
  it('normaliza os dados e guarda como Pendente, sem chamar a API', async () => {
    const cliente = await servico.cadastrar(VALIDO);

    expect(cliente).toMatchObject({
      id: 'cli-1',
      nome: 'Móveis Lima',
      cpfCnpj: '12345678000195',
      email: 'contato@lima.com.br',
      telefone: null,
      uf: 'SP',
      estado: 'Pendente',
    });
    expect(await repo.listar()).toHaveLength(1);
    expect(api.criar).not.toHaveBeenCalled();
    expect(await servico.contar()).toEqual({ pendentes: 1, enviando: 0, rejeitados: 0 });
  });

  it('dado inválido não é gravado', async () => {
    await expect(servico.cadastrar({ ...VALIDO, nome: '' })).rejects.toMatchObject({ code: 'VALIDACAO' });
    expect(await repo.listar()).toEqual([]);
  });

  it('o mesmo documento duas vezes na fila é recusado', async () => {
    await servico.cadastrar(VALIDO);

    await expect(servico.cadastrar({ ...VALIDO, nome: 'Outro' })).rejects.toMatchObject({ code: 'DOCUMENTO_DUPLICADO' });
    expect(await repo.listar()).toHaveLength(1);
  });
});

describe('enviarPendentes', () => {
  it('envia em ordem de cadastro, com o corpo da API, e remove os confirmados', async () => {
    await servico.cadastrar(VALIDO);
    await servico.cadastrar({ ...VALIDO, nome: 'Ana', cpfCnpj: '12345678901' });

    const resumo = await servico.enviarPendentes();

    expect(resumo).toEqual({ confirmados: 2, rejeitados: 0, interrompidaPorRede: false });
    expect(api.criar.mock.calls.map(([c]) => c.nome)).toEqual(['Móveis Lima', 'Ana']);
    expect(api.criar.mock.calls[0][0]).toEqual({
      nome: 'Móveis Lima',
      cpfCnpj: '12345678000195',
      email: 'contato@lima.com.br',
      telefone: null,
      cidade: 'Campinas',
      uf: 'SP',
    });
    expect(await repo.listar()).toEqual([]);
  });

  it('sem rede o cliente continua guardado, volta a Pendente e a rodada para', async () => {
    await servico.cadastrar(VALIDO);
    await servico.cadastrar({ ...VALIDO, nome: 'Ana', cpfCnpj: '12345678901' });
    api.criar.mockRejectedValue(erroDeRede());

    const resumo = await servico.enviarPendentes();

    expect(resumo).toEqual({ confirmados: 0, rejeitados: 0, interrompidaPorRede: true });
    expect(api.criar).toHaveBeenCalledTimes(1);
    const guardados = await repo.listar();
    expect(guardados).toHaveLength(2);
    expect(guardados.every((c) => c.estado === 'Pendente')).toBe(true);
    expect(guardados.find((c) => c.id === 'cli-1')?.tentativas).toBe(1);
  });

  it('quando a rede volta, o que ficou guardado é enviado', async () => {
    await servico.cadastrar(VALIDO);
    api.criar.mockRejectedValueOnce(erroDeRede());
    await servico.enviarPendentes();

    const resumo = await servico.enviarPendentes();

    expect(resumo.confirmados).toBe(1);
    expect(await repo.listar()).toEqual([]);
  });

  it('recusa do servidor (4xx) fica como Rejeitado, com o motivo, e não impede os demais', async () => {
    await servico.cadastrar(VALIDO);
    await servico.cadastrar({ ...VALIDO, nome: 'Ana', cpfCnpj: '12345678901' });
    api.criar.mockRejectedValueOnce(new ApiError('VALIDACAO', 'Informe um e-mail válido.', 400));

    const resumo = await servico.enviarPendentes();

    expect(resumo).toEqual({ confirmados: 1, rejeitados: 1, interrompidaPorRede: false });
    expect(await repo.listar()).toMatchObject([
      { id: 'cli-1', estado: 'Rejeitado', codigo: 'VALIDACAO', mensagem: 'Informe um e-mail válido.' },
    ]);
  });

  it('erro do servidor (5xx) devolve à fila e encerra a rodada, sem rejeitar', async () => {
    await servico.cadastrar(VALIDO);
    api.criar.mockRejectedValue(new ApiError('ERRO', 'Falha.', 503));

    const resumo = await servico.enviarPendentes();

    expect(resumo).toEqual({ confirmados: 0, rejeitados: 0, interrompidaPorRede: false });
    expect((await repo.listar())[0]).toMatchObject({ estado: 'Pendente', tentativas: 1 });
  });

  it('resposta perdida: o reenvio dá 409, mas o servidor já tem este cliente, e ele sai da fila', async () => {
    await servico.cadastrar(VALIDO);
    api.criar.mockRejectedValue(new ApiError('DOCUMENTO_DUPLICADO', 'Já existe.', 409));
    api.buscar.mockResolvedValue([{ nome: 'MÓVEIS LIMA', cpfCnpj: '12.345.678/0001-95' }]);

    const resumo = await servico.enviarPendentes();

    expect(resumo).toMatchObject({ confirmados: 1, rejeitados: 0 });
    expect(api.buscar).toHaveBeenCalledWith('12345678000195');
    expect(await repo.listar()).toEqual([]);
  });

  it('409 de um cliente diferente com o mesmo documento é rejeitado e mostrado ao Vendedor', async () => {
    await servico.cadastrar(VALIDO);
    api.criar.mockRejectedValue(new ApiError('DOCUMENTO_DUPLICADO', 'Já existe um cliente com este CPF/CNPJ.', 409));
    api.buscar.mockResolvedValue([{ nome: 'Outra Empresa', cpfCnpj: '12345678000195' }]);

    const resumo = await servico.enviarPendentes();

    expect(resumo).toMatchObject({ confirmados: 0, rejeitados: 1 });
    expect((await repo.listar())[0]).toMatchObject({ estado: 'Rejeitado', codigo: 'DOCUMENTO_DUPLICADO' });
  });

  it('se a conferência do 409 falhar, o cliente é rejeitado em vez de se perder', async () => {
    await servico.cadastrar(VALIDO);
    api.criar.mockRejectedValue(new ApiError('DOCUMENTO_DUPLICADO', 'Já existe.', 409));
    api.buscar.mockRejectedValue(erroDeRede());

    await servico.enviarPendentes();

    expect((await repo.listar())[0].estado).toBe('Rejeitado');
  });

  it('Tentar de novo volta o rejeitado a Pendente; Descartar só vale para rejeitado', async () => {
    await servico.cadastrar(VALIDO);
    api.criar.mockRejectedValueOnce(new ApiError('VALIDACAO', 'Recusado.', 400));
    await servico.enviarPendentes();

    await servico.reenviar('cli-1');
    expect((await repo.listar())[0]).toMatchObject({ estado: 'Pendente', codigo: null, mensagem: null });

    await servico.descartar('cli-1'); // pendente: não descarta
    expect(await repo.listar()).toHaveLength(1);

    await servico.enviarPendentes();
    expect(await repo.listar()).toEqual([]);
  });

  it('o que ficou Enviando por um fechamento do app volta a Pendente', async () => {
    await servico.cadastrar(VALIDO);
    await repo.marcarEnviando('cli-1');

    await servico.recuperarEnvioInterrompido();

    expect((await repo.listar())[0].estado).toBe('Pendente');
  });
});

describe('integração com a sincronização', () => {
  it('a mesma rodada envia os apontamentos e os clientes e soma os resultados', async () => {
    const fila = new FilaRepository(db);
    await fila.enfileirar({
      clientRequestId: 'a',
      tipo: 'Inicio',
      etapaId: 1,
      numeroOp: 'OP-1',
      estacaoNome: 'Corte',
      dataHoraCliente: '2026-11-03T12:00:00.000Z',
    });
    await servico.cadastrar(VALIDO);

    const resumo = await new SyncService({ fila, api: new ServidorDeSincronizacaoFalso(), clientes: servico }).sincronizar();

    expect(resumo).toMatchObject({ confirmados: 2, rejeitados: 0, interrompidaPorRede: false });
    expect(api.criar).toHaveBeenCalledTimes(1);
  });

  it('com a rede fora, os apontamentos param a rodada e os clientes nem são tentados; nada se perde', async () => {
    const fila = new FilaRepository(db);
    const servidor = new ServidorDeSincronizacaoFalso();
    servidor.online = false;
    await fila.enfileirar({
      clientRequestId: 'a',
      tipo: 'Inicio',
      etapaId: 1,
      numeroOp: 'OP-1',
      estacaoNome: 'Corte',
      dataHoraCliente: '2026-11-03T12:00:00.000Z',
    });
    await servico.cadastrar(VALIDO);

    const resumo = await new SyncService({ fila, api: servidor, clientes: servico }).sincronizar();

    expect(resumo.interrompidaPorRede).toBe(true);
    expect(api.criar).not.toHaveBeenCalled();
    expect(await repo.listar()).toHaveLength(1);
  });

  it('sair do app apaga os clientes que não foram enviados', async () => {
    await servico.cadastrar(VALIDO);

    await apagarDadosLocais(db);

    expect(await repo.listar()).toEqual([]);
  });

  it('a tabela recusa estado inválido e documento repetido (restrições do banco)', async () => {
    await servico.cadastrar(VALIDO);

    await expect(db.executar(`UPDATE cliente_pendente SET estado = 'Outro' WHERE id = 'cli-1'`)).rejects.toThrow();
    await expect(
      repo.gravar({ id: 'x', nome: 'N', cpfCnpj: '12345678000195', email: null, telefone: null, cidade: null, uf: null, criadoEm: 'a' }),
    ).rejects.toThrow();
  });
});
