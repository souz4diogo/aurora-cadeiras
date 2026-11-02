import * as SecureStore from 'expo-secure-store';

export type Perfil =
  | 'Administrador'
  | 'GerenteProducao'
  | 'Operador'
  | 'Vendedor'
  | 'Almoxarife'
  | 'Inspetor'
  | 'Cliente';

export interface UsuarioLogado {
  id: number;
  nome: string;
  perfil: Perfil;
}

export interface Sessao {
  accessToken: string;
  refreshToken: string;
  usuario: UsuarioLogado;
}

/** Onde a sessão fica guardada. A senha nunca é guardada (docs/08 §4). */
export interface TokenStore {
  carregar(): Promise<Sessao | null>;
  salvar(sessao: Sessao): Promise<void>;
  limpar(): Promise<void>;
}

const CHAVE = 'aurora.sessao';

/** Armazenamento seguro do sistema: Keystore no Android, Keychain no iOS (docs/08 §4). */
export class SecureTokenStore implements TokenStore {
  async carregar(): Promise<Sessao | null> {
    const texto = await SecureStore.getItemAsync(CHAVE);
    if (!texto) return null;
    try {
      return JSON.parse(texto) as Sessao;
    } catch {
      await SecureStore.deleteItemAsync(CHAVE);
      return null;
    }
  }

  async salvar(sessao: Sessao): Promise<void> {
    await SecureStore.setItemAsync(CHAVE, JSON.stringify(sessao), {
      // Só com o aparelho desbloqueado e sem sair dele (iOS não leva o token em backup nem para outro aparelho)
      keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
    });
  }

  async limpar(): Promise<void> {
    await SecureStore.deleteItemAsync(CHAVE);
  }
}

/** Sessão só em memória: quem não marca "manter conectado" precisa entrar de novo ao reabrir o app. */
export class MemoryTokenStore implements TokenStore {
  private sessao: Sessao | null = null;

  async carregar(): Promise<Sessao | null> {
    return this.sessao;
  }

  async salvar(sessao: Sessao): Promise<void> {
    this.sessao = sessao;
  }

  async limpar(): Promise<void> {
    this.sessao = null;
  }
}

/**
 * Guarda a sessão no armazenamento seguro quando o usuário escolheu "manter conectado" e só em memória
 * quando não escolheu. O token renovado vai para o mesmo lugar em que a sessão já estava.
 */
export class TokenStoreEscolhivel implements TokenStore {
  private persistir = false;

  constructor(
    private readonly seguro: TokenStore = new SecureTokenStore(),
    private readonly memoria: TokenStore = new MemoryTokenStore(),
  ) {}

  definirPersistencia(persistir: boolean): void {
    this.persistir = persistir;
  }

  async carregar(): Promise<Sessao | null> {
    const guardada = await this.seguro.carregar();
    if (guardada) {
      this.persistir = true;
      return guardada;
    }
    return this.memoria.carregar();
  }

  async salvar(sessao: Sessao): Promise<void> {
    if (this.persistir) {
      await this.seguro.salvar(sessao);
      await this.memoria.limpar();
    } else {
      await this.memoria.salvar(sessao);
      await this.seguro.limpar();
    }
  }

  async limpar(): Promise<void> {
    this.persistir = false;
    await this.seguro.limpar();
    await this.memoria.limpar();
  }
}
