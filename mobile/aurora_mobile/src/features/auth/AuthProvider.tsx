import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import type { UsuarioLogado } from '@/core/auth/tokenStore';
import { useServicos } from '@/core/ServicosProvider';

type Estado = { status: 'carregando' } | { status: 'deslogado' } | { status: 'logado'; usuario: UsuarioLogado };

interface ContextoDeAuth {
  estado: Estado;
  entrar(email: string, senha: string, manterConectado: boolean): Promise<void>;
  sair(): Promise<void>;
}

const Contexto = createContext<ContextoDeAuth | null>(null);

/** Quem avisa o app que a renovação do token falhou (o cliente HTTP chama; o provider escuta). */
const ouvintes = new Set<() => void>();
export const eventosDeSessao = {
  expirou(): void {
    ouvintes.forEach((ouvinte) => ouvinte());
  },
};

export function AuthProvider({ children }: { children: ReactNode }) {
  const { auth } = useServicos();
  const [estado, setEstado] = useState<Estado>({ status: 'carregando' });

  useEffect(() => {
    let ativo = true;
    void auth.restaurar().then((usuario) => {
      if (ativo) setEstado(usuario ? { status: 'logado', usuario } : { status: 'deslogado' });
    });
    return () => {
      ativo = false;
    };
  }, [auth]);

  useEffect(() => {
    const aoExpirar = () => setEstado({ status: 'deslogado' });
    ouvintes.add(aoExpirar);
    return () => {
      ouvintes.delete(aoExpirar);
    };
  }, []);

  const entrar = useCallback(
    async (email: string, senha: string, manterConectado: boolean) => {
      const usuario = await auth.entrar(email, senha, manterConectado);
      setEstado({ status: 'logado', usuario });
    },
    [auth],
  );

  const sair = useCallback(async () => {
    await auth.sair();
    setEstado({ status: 'deslogado' });
  }, [auth]);

  const valor = useMemo(() => ({ estado, entrar, sair }), [estado, entrar, sair]);
  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useAuth(): ContextoDeAuth {
  const contexto = useContext(Contexto);
  if (!contexto) throw new Error('useAuth precisa estar dentro de AuthProvider.');
  return contexto;
}

/** Usuário logado; só chame em telas que ficam atrás do login. */
export function useUsuario(): UsuarioLogado {
  const { estado } = useAuth();
  if (estado.status !== 'logado') throw new Error('Tela protegida aberta sem usuário logado.');
  return estado.usuario;
}
