import type { Sessao, TokenStore, UsuarioLogado } from '@/core/auth/tokenStore';
import { interpretarErro } from '@/core/http/problem';
import type { AuthApi } from '@/features/auth/authApi';

/** Onde a sessão fica; quem não marca "manter conectado" fica só em memória (ver TokenStoreEscolhivel). */
export interface ArmazenamentoDaSessao extends TokenStore {
  definirPersistencia(persistir: boolean): void;
}

export interface DependenciasDeAuth {
  api: AuthApi;
  tokenStore: ArmazenamentoDaSessao;
  /** Apaga o banco local (etapas, fila, caches) ao sair (docs/08 §4 e §9). */
  limparDadosLocais: () => Promise<void>;
  /** Depois de um login com sucesso (ex.: detectar troca de usuário no aparelho). */
  aoAutenticar?: (usuario: UsuarioLogado) => Promise<void>;
}

export class AuthService {
  constructor(private readonly deps: DependenciasDeAuth) {}

  /** Ao abrir o app: há sessão guardada? O token vencido é renovado pelo interceptor na primeira chamada. */
  async restaurar(): Promise<UsuarioLogado | null> {
    return (await this.deps.tokenStore.carregar())?.usuario ?? null;
  }

  async entrar(email: string, senha: string, manterConectado: boolean): Promise<UsuarioLogado> {
    try {
      const resposta = await this.deps.api.login(email.trim(), senha);
      const sessao: Sessao = {
        accessToken: resposta.accessToken,
        refreshToken: resposta.refreshToken,
        usuario: resposta.usuario,
      };
      // Antes de gravar a sessão: se a limpeza dos dados do usuário anterior falhar, o login falha inteiro e nenhuma
      // sessão fica guardada (senão, ao reabrir, o app entraria como o novo usuário e enviaria a fila do antigo)
      await this.deps.aoAutenticar?.(sessao.usuario);
      this.deps.tokenStore.definirPersistencia(manterConectado);
      await this.deps.tokenStore.salvar(sessao);
      return sessao.usuario;
    } catch (erro) {
      throw interpretarErro(erro);
    }
  }

  /**
   * Revoga o refresh token na API (se houver rede), apaga a sessão e o banco local. Sem rede o servidor não é
   * avisado, mas o aparelho fica limpo; o refresh token expira sozinho em 7 dias.
   */
  async sair(): Promise<void> {
    const sessao = await this.deps.tokenStore.carregar();
    if (sessao) {
      try {
        await this.deps.api.logout(sessao.refreshToken);
      } catch {
        // Sair não pode depender da rede
      }
    }
    await this.deps.tokenStore.limpar();
    await this.deps.limparDadosLocais();
  }
}
