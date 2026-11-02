import { useServicos } from '@/core/ServicosProvider';
import { useAuth } from '@/features/auth/AuthProvider';
import { useSync } from '@/features/sync/SyncProvider';

/**
 * Sair apaga o banco local (docs/08 §4), e isso levaria junto apontamentos que o servidor ainda não recebeu.
 * Por isso tenta enviar antes e, se sobrar algo, só sai depois de o usuário confirmar a perda.
 * Devolve verdadeiro se saiu.
 */
export function useSairComSeguranca() {
  const { sair } = useAuth();
  const { fila } = useServicos();
  const { online, sincronizarAgora } = useSync();

  return async function sairComSeguranca(confirmarPerda: (naoEnviados: number) => Promise<boolean>): Promise<boolean> {
    if (online) await sincronizarAgora().catch(() => undefined);
    const contagem = await fila.contar();
    const naoEnviados = contagem.pendentes + contagem.enviando + contagem.rejeitados;
    if (naoEnviados > 0 && !(await confirmarPerda(naoEnviados))) return false;
    await sair();
    return true;
  };
}
