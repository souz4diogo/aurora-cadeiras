import NetInfo from '@react-native-community/netinfo';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { AppState } from 'react-native';

import { INTERVALO_DE_SINCRONIZACAO_MS } from '@/core/config';
import { useServicos } from '@/core/ServicosProvider';
import type { ContagemDaFila, ResumoDaSincronizacao } from '@/features/sync/tipos';

interface ContextoDeSync {
  online: boolean;
  sincronizando: boolean;
  contagem: ContagemDaFila;
  /** Resultado da última rodada, para a tela avisar "N rejeitados". */
  ultimaRodada: ResumoDaSincronizacao | null;
  /** Envia a fila agora (botão e puxar para atualizar). */
  sincronizarAgora(): Promise<ResumoDaSincronizacao>;
  /** Recontagem depois de mexer na fila (ao apontar, descartar ou reenviar). */
  atualizarContagem(): Promise<void>;
}

const VAZIA: ContagemDaFila = { pendentes: 0, enviando: 0, rejeitados: 0 };

const Contexto = createContext<ContextoDeSync | null>(null);

/**
 * Conectividade e sincronização (docs/08 §5, passo 3): envia a fila ao abrir o app, quando a conexão volta, a cada
 * 5 minutos com o app em primeiro plano e quando ele volta ao primeiro plano. Fica atrás do login.
 */
export function SyncProvider({ ativo, children }: { ativo: boolean; children: ReactNode }) {
  const { sync, fila } = useServicos();
  const [online, setOnline] = useState(true);
  const [sincronizando, setSincronizando] = useState(false);
  const [contagem, setContagem] = useState<ContagemDaFila>(VAZIA);
  const [ultimaRodada, setUltimaRodada] = useState<ResumoDaSincronizacao | null>(null);
  const estavaOnline = useRef(true);

  const atualizarContagem = useCallback(async () => {
    setContagem(await fila.contar());
  }, [fila]);

  const sincronizarAgora = useCallback(async () => {
    setSincronizando(true);
    try {
      const resumo = await sync.sincronizar();
      setUltimaRodada(resumo);
      return resumo;
    } finally {
      setSincronizando(false);
      await atualizarContagem();
    }
  }, [sync, atualizarContagem]);

  // Disparos silenciosos: falha aqui não incomoda o usuário, a fila continua intacta
  const tentarSincronizar = useCallback(() => {
    void sincronizarAgora().catch(() => {});
  }, [sincronizarAgora]);

  useEffect(() => {
    if (!ativo) return;
    void fila.contar().then(setContagem);
    // Ao abrir o app. Adiado para depois da montagem, para não atualizar estado dentro do próprio efeito
    const adiado = setTimeout(tentarSincronizar, 0);
    return () => clearTimeout(adiado);
  }, [ativo, fila, tentarSincronizar]);

  useEffect(() => {
    if (!ativo) return;
    return NetInfo.addEventListener((estado) => {
      const conectado = !!estado.isConnected && estado.isInternetReachable !== false;
      setOnline(conectado);
      if (conectado && !estavaOnline.current) tentarSincronizar(); // a conexão voltou
      estavaOnline.current = conectado;
    });
  }, [ativo, tentarSincronizar]);

  useEffect(() => {
    if (!ativo) return;
    const temporizador = setInterval(() => {
      if (AppState.currentState === 'active') tentarSincronizar();
    }, INTERVALO_DE_SINCRONIZACAO_MS);
    const assinatura = AppState.addEventListener('change', (estado) => {
      if (estado === 'active') tentarSincronizar();
    });
    return () => {
      clearInterval(temporizador);
      assinatura.remove();
    };
  }, [ativo, tentarSincronizar]);

  const valor = useMemo(
    () => ({ online, sincronizando, contagem, ultimaRodada, sincronizarAgora, atualizarContagem }),
    [online, sincronizando, contagem, ultimaRodada, sincronizarAgora, atualizarContagem],
  );
  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useSync(): ContextoDeSync {
  const contexto = useContext(Contexto);
  if (!contexto) throw new Error('useSync precisa estar dentro de SyncProvider.');
  return contexto;
}
