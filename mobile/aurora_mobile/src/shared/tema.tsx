import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useColorScheme } from 'react-native';

import {
  CHAVES,
  ESCALAS_DE_FONTE,
  type EscalaDeFonte,
  type PreferenciasRepository,
} from '@/features/preferencias/preferenciasRepository';

/** Paleta com contraste mínimo de 4,5:1 entre texto e fundo (docs/08 §8). */
export interface Cores {
  fundo: string;
  superficie: string;
  texto: string;
  textoSecundario: string;
  borda: string;
  primaria: string;
  textoSobrePrimaria: string;
  perigo: string;
  textoSobrePerigo: string;
  sucesso: string;
  fundoSucesso: string;
  aviso: string;
  fundoAviso: string;
  fundoErro: string;
}

const CLARO: Cores = {
  fundo: '#F3F5F8',
  superficie: '#FFFFFF',
  texto: '#14212E',
  textoSecundario: '#46576A',
  borda: '#C7D0DA',
  primaria: '#1F3A5F',
  textoSobrePrimaria: '#FFFFFF',
  perigo: '#9C1C14',
  textoSobrePerigo: '#FFFFFF',
  sucesso: '#16603A',
  fundoSucesso: '#E3F4EA',
  aviso: '#6B4300',
  fundoAviso: '#FFF1CC',
  fundoErro: '#FBE4E2',
};

const ESCURO: Cores = {
  fundo: '#0F1722',
  superficie: '#182433',
  texto: '#F1F5F9',
  textoSecundario: '#B7C3D0',
  borda: '#34475C',
  primaria: '#8DB6F0',
  textoSobrePrimaria: '#0B1624',
  perigo: '#FF9C94',
  textoSobrePerigo: '#2B0805',
  sucesso: '#7FD6A3',
  fundoSucesso: '#143323',
  aviso: '#FFD27A',
  fundoAviso: '#3A2B08',
  fundoErro: '#3E1713',
};

const ALTO_CONTRASTE: Cores = {
  fundo: '#000000',
  superficie: '#000000',
  texto: '#FFFFFF',
  textoSecundario: '#FFFFFF',
  borda: '#FFFFFF',
  primaria: '#FFE600',
  textoSobrePrimaria: '#000000',
  perigo: '#FF6B5E',
  textoSobrePerigo: '#000000',
  sucesso: '#6BFF9E',
  fundoSucesso: '#000000',
  aviso: '#FFE600',
  fundoAviso: '#000000',
  fundoErro: '#000000',
};

interface ContextoDoTema {
  cores: Cores;
  escala: EscalaDeFonte;
  altoContraste: boolean;
  /** Cor dos ícones da barra de status: escuros quando o cabeçalho é claro (tema escuro e alto contraste). */
  estiloDaBarra: 'light' | 'dark';
  definirEscala(escala: EscalaDeFonte): void;
  definirAltoContraste(ativo: boolean): void;
}

const Contexto = createContext<ContextoDoTema>({
  cores: CLARO,
  escala: 1,
  altoContraste: false,
  estiloDaBarra: 'light',
  definirEscala: () => {},
  definirAltoContraste: () => {},
});

export function useTema(): ContextoDoTema {
  return useContext(Contexto);
}

interface Props {
  preferencias: PreferenciasRepository;
  children: ReactNode;
}

/** Tema do app: claro/escuro do sistema, alto contraste e escala de fonte escolhidos pelo usuário (docs/08 §8). */
export function ProvedorDeTema({ preferencias, children }: Props) {
  const esquema = useColorScheme();
  const [escala, setEscala] = useState<EscalaDeFonte>(1);
  const [altoContraste, setAltoContraste] = useState(false);

  useEffect(() => {
    let ativo = true;
    void (async () => {
      const salva = Number(await preferencias.obter(CHAVES.escalaDaFonte));
      const contraste = (await preferencias.obter(CHAVES.altoContraste)) === '1';
      if (!ativo) return;
      if ((ESCALAS_DE_FONTE as readonly number[]).includes(salva)) setEscala(salva as EscalaDeFonte);
      setAltoContraste(contraste);
    })();
    return () => {
      ativo = false;
    };
  }, [preferencias]);

  const definirEscala = useCallback(
    (nova: EscalaDeFonte) => {
      setEscala(nova);
      void preferencias.definir(CHAVES.escalaDaFonte, String(nova));
    },
    [preferencias],
  );
  const definirAltoContraste = useCallback(
    (ativo: boolean) => {
      setAltoContraste(ativo);
      void preferencias.definir(CHAVES.altoContraste, ativo ? '1' : '0');
    },
    [preferencias],
  );

  const valor = useMemo<ContextoDoTema>(
    () => ({
      cores: altoContraste ? ALTO_CONTRASTE : esquema === 'dark' ? ESCURO : CLARO,
      escala,
      altoContraste,
      estiloDaBarra: altoContraste || esquema === 'dark' ? 'dark' : 'light',
      definirEscala,
      definirAltoContraste,
    }),
    [altoContraste, esquema, escala, definirEscala, definirAltoContraste],
  );

  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}
