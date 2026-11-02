import { AxiosError } from 'axios';

import { ApiError, ehErroDeRede, interpretarErro, mensagemDoCodigo, SEM_CONEXAO } from '@/core/http/problem';

function erroHttp(status: number, data: unknown): AxiosError {
  const config = {} as never;
  return new AxiosError('falhou', 'ERR_BAD_REQUEST', config, null, {
    status,
    statusText: String(status),
    headers: {},
    config,
    data,
  });
}

describe('interpretarErro', () => {
  it.each([
    ['RN04', 409, 'Não há saldo suficiente de matéria-prima para iniciar esta etapa.'],
    ['RN05', 409, 'A etapa anterior ainda não foi concluída.'],
    ['RN06', 422, 'Informe uma estação de retrabalho válida, anterior à Qualidade.'],
    ['RN07', 409, 'O pedido só pode ser expedido quando estiver pronto.'],
    ['CREDENCIAIS_INVALIDAS', 401, 'E-mail ou senha inválidos.'],
  ])('traduz %s para português', (code, status, mensagem) => {
    const erro = interpretarErro(erroHttp(status, { code, detail: 'texto técnico em inglês' }));

    expect(erro).toBeInstanceOf(ApiError);
    expect(erro.code).toBe(code);
    expect(erro.message).toBe(mensagem);
    expect(erro.status).toBe(status);
  });

  it('sem resposta do servidor é falta de conexão', () => {
    const erro = interpretarErro(new AxiosError('Network Error', 'ERR_NETWORK'));

    expect(erro.code).toBe(SEM_CONEXAO);
    expect(ehErroDeRede(erro)).toBe(true);
  });

  it('código desconhecido usa o texto do servidor quando é erro de uso (4xx)', () => {
    const erro = interpretarErro(erroHttp(422, { code: 'NOVO_CODIGO', detail: 'A quantidade deve ser maior que zero.' }));

    expect(erro.message).toBe('A quantidade deve ser maior que zero.');
  });

  it('erro 500 nunca mostra o texto técnico do servidor', () => {
    const erro = interpretarErro(erroHttp(500, { code: 'ERRO', detail: 'NullReferenceException em Foo.Bar()' }));

    expect(erro.message).toBe(mensagemDoCodigo('ERRO'));
    expect(erro.message).not.toContain('NullReference');
  });

  it('400 de validação mostra a primeira mensagem do campo', () => {
    const erro = interpretarErro(erroHttp(400, { errors: { Motivo: ['O motivo da perda é obrigatório.'] } }));

    expect(erro.code).toBe('VALIDACAO');
    expect(erro.message).toBe('O motivo da perda é obrigatório.');
  });

  it('erro com resposta não é erro de rede', () => {
    expect(ehErroDeRede(erroHttp(409, { code: 'RN04' }))).toBe(false);
    expect(ehErroDeRede(new Error('qualquer'))).toBe(false);
  });

  it('qualquer outra coisa vira erro genérico em português', () => {
    expect(interpretarErro(new Error('boom')).message).toBe(mensagemDoCodigo('ERRO'));
  });
});
