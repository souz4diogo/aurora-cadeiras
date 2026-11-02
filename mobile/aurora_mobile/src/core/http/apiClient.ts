import { create, isAxiosError, type AxiosAdapter, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios';

import type { Sessao, TokenStore, UsuarioLogado } from '@/core/auth/tokenStore';
import { API_BASE_URL, TIMEOUT_DA_API_MS } from '@/core/config';

export interface TokenResponse {
  accessToken: string;
  expiresInSeconds: number;
  refreshToken: string;
  usuario: UsuarioLogado;
}

export interface OpcoesDoCliente {
  baseURL?: string;
  tokenStore: TokenStore;
  /** Chamado quando a renovação falha: a sessão acabou e o app volta ao Login (docs/08 §4). */
  aoExpirarSessao: () => void;
  /** Só os testes trocam o adaptador de rede. */
  adapter?: AxiosAdapter;
}

interface ConfigComTentativa extends InternalAxiosRequestConfig {
  _jaRenovou?: boolean;
}

/** Não há sessão guardada para renovar. */
class SemSessaoError extends Error {}

/**
 * O servidor respondeu e RECUSOU a renovação (refresh inválido, expirado, revogado ou reutilizado): a sessão acabou.
 * Queda de rede, tempo esgotado ou erro do servidor (5xx) não provam nada sobre a sessão e não podem deslogar quem
 * está trabalhando offline na fábrica.
 */
export function renovacaoFoiRecusada(falha: unknown): boolean {
  if (falha instanceof SemSessaoError) return true;
  const status = isAxiosError(falha) ? falha.response?.status : undefined;
  return status === 400 || status === 401 || status === 403;
}

/** Rotas de autenticação: um 401 aqui é senha ou refresh inválido, nunca "token vencido". */
const ROTAS_DE_AUTENTICACAO = ['/auth/login', '/auth/refresh', '/auth/logout'];

function ehRotaDeAutenticacao(url: string | undefined): boolean {
  return ROTAS_DE_AUTENTICACAO.some((rota) => url?.includes(rota));
}

/**
 * Cliente HTTP da API. Anexa o Bearer e, ao receber 401, renova o token UMA vez e repete a chamada;
 * chamadas simultâneas esperam a mesma renovação (docs/08 §4). Se a renovação falha, encerra a sessão.
 */
export function criarApiClient(opcoes: OpcoesDoCliente): AxiosInstance {
  const { tokenStore, aoExpirarSessao } = opcoes;
  const http = create({
    baseURL: opcoes.baseURL ?? API_BASE_URL,
    timeout: TIMEOUT_DA_API_MS,
    headers: { Accept: 'application/json' },
    adapter: opcoes.adapter,
  });
  // Cliente sem interceptors: a própria chamada de renovação não pode cair no tratamento de 401
  const semInterceptors = create({
    baseURL: opcoes.baseURL ?? API_BASE_URL,
    timeout: TIMEOUT_DA_API_MS,
    adapter: opcoes.adapter,
  });

  let renovacaoEmAndamento: Promise<Sessao> | null = null;

  const renovar = (): Promise<Sessao> => {
    renovacaoEmAndamento ??= (async () => {
      try {
        const atual = await tokenStore.carregar();
        if (!atual) throw new SemSessaoError();
        const resposta = await semInterceptors.post<TokenResponse>('/auth/refresh', {
          refreshToken: atual.refreshToken,
        });
        const nova: Sessao = {
          accessToken: resposta.data.accessToken,
          refreshToken: resposta.data.refreshToken,
          usuario: resposta.data.usuario,
        };
        await tokenStore.salvar(nova);
        return nova;
      } finally {
        renovacaoEmAndamento = null;
      }
    })();
    return renovacaoEmAndamento;
  };

  http.interceptors.request.use(async (config) => {
    if (!ehRotaDeAutenticacao(config.url)) {
      const sessao = await tokenStore.carregar();
      if (sessao) config.headers.Authorization = `Bearer ${sessao.accessToken}`;
    }
    return config;
  });

  http.interceptors.response.use(
    (resposta) => resposta,
    async (erro: unknown) => {
      if (!isAxiosError(erro) || erro.response?.status !== 401 || !erro.config) throw erro;
      const original = erro.config as ConfigComTentativa;
      if (original._jaRenovou || ehRotaDeAutenticacao(original.url)) throw erro;

      let nova: Sessao;
      try {
        nova = await renovar();
      } catch (falha) {
        // Sessão ainda pode valer: devolve o erro real (rede, 5xx) e deixa tokens e fila intactos
        if (!renovacaoFoiRecusada(falha)) throw falha;
        await tokenStore.limpar();
        aoExpirarSessao();
        throw erro;
      }
      original._jaRenovou = true;
      original.headers.Authorization = `Bearer ${nova.accessToken}`;
      return http.request(original);
    },
  );

  return http;
}
