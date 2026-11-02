import { isAxiosError } from 'axios';

/** Corpo de erro da API: ProblemDetails (RFC 7807) com o campo extra `code` (docs/06 §1). */
export interface ProblemDetails {
  title?: string;
  status?: number;
  code?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Erro já interpretado: tem o código estável da API e uma mensagem em português para o usuário. */
export class ApiError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export const SEM_CONEXAO = 'SEM_CONEXAO';

/** Mensagens em português para os códigos que a API devolve (docs/06 §1, docs/DECISOES.md). */
const MENSAGENS: Record<string, string> = {
  RN01: 'O pedido precisa estar confirmado para gerar ordens de produção.',
  RN04: 'Não há saldo suficiente de matéria-prima para iniciar esta etapa.',
  RN05: 'A etapa anterior ainda não foi concluída.',
  RN06: 'Informe uma estação de retrabalho válida, anterior à Qualidade.',
  RN07: 'O pedido só pode ser expedido quando estiver pronto.',
  STATUS_INVALIDO: 'A situação atual não permite esta ação.',
  ETAPA_NAO_ENCONTRADA: 'Etapa não encontrada.',
  ORDEM_PRODUCAO_NAO_ENCONTRADA: 'Ordem de produção não encontrada.',
  PEDIDO_NAO_ENCONTRADO: 'Pedido não encontrado.',
  NAO_ENCONTRADO: 'Registro não encontrado.',
  PERFIL_SEM_PERMISSAO: 'Seu perfil não pode fazer esta ação.',
  SEM_PERMISSAO: 'Seu perfil não tem permissão para isso.',
  NAO_AUTENTICADO: 'Sua sessão expirou. Entre novamente.',
  CREDENCIAIS_INVALIDAS: 'E-mail ou senha inválidos.',
  CONTA_BLOQUEADA: 'Conta bloqueada por tentativas incorretas. Tente de novo em alguns minutos.',
  REFRESH_INVALIDO: 'Sua sessão expirou. Entre novamente.',
  LIMITE_EXCEDIDO: 'Muitas tentativas. Aguarde um minuto e tente de novo.',
  PERDA_ACIMA_DA_QUANTIDADE: 'A perda não pode passar da quantidade da ordem de produção.',
  ESTACAO_INEXISTENTE: 'Estação inexistente.',
  VALIDACAO: 'Dados inválidos.',
  [SEM_CONEXAO]: 'Sem conexão com o servidor. Verifique a internet e tente de novo.',
  ERRO: 'O servidor não conseguiu concluir a operação. Tente novamente.',
};

/** Mensagem em português para um código; sem tradução conhecida, usa o texto que o servidor mandou. */
export function mensagemDoCodigo(code: string, detalhe?: string): string {
  return MENSAGENS[code] ?? detalhe ?? MENSAGENS.ERRO;
}

/** Falhou por rede (sem resposta do servidor) em vez de por regra: a ação pode ser repetida depois. */
export function ehErroDeRede(erro: unknown): boolean {
  if (erro instanceof ApiError) return erro.code === SEM_CONEXAO;
  return isAxiosError(erro) && erro.response === undefined;
}

/** Converte qualquer erro numa ApiError com mensagem em português (nunca expõe texto técnico). */
export function interpretarErro(erro: unknown): ApiError {
  if (erro instanceof ApiError) return erro;
  if (isAxiosError(erro)) {
    if (erro.response === undefined) {
      return new ApiError(SEM_CONEXAO, MENSAGENS[SEM_CONEXAO]);
    }
    const status = erro.response.status;
    const problema = (erro.response.data ?? {}) as ProblemDetails;
    const code = problema.code ?? (status === 400 ? 'VALIDACAO' : 'ERRO');
    const detalhe = primeiroErroDeValidacao(problema) ?? (status < 500 ? problema.detail : undefined);
    // Na validação a mensagem do campo ajuda mais do que "Dados inválidos."
    const mensagem = code === 'VALIDACAO' && detalhe ? detalhe : mensagemDoCodigo(code, detalhe);
    return new ApiError(code, mensagem, status);
  }
  return new ApiError('ERRO', MENSAGENS.ERRO);
}

function primeiroErroDeValidacao(problema: ProblemDetails): string | undefined {
  const lista = problema.errors ? Object.values(problema.errors).flat() : [];
  return lista[0];
}
