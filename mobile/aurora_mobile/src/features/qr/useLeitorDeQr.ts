import { router } from 'expo-router';
import { useRef, useState } from 'react';

import { useServicos } from '@/core/ServicosProvider';
import { interpretarErro } from '@/core/http/problem';
import { extrairCodigoDaOrdem, MENSAGEM_QR_INVALIDO } from '@/features/qr/leituraDeQr';
import { vibrarAlerta, vibrarSucesso } from '@/shared/vibracao';

/** ViewModel do leitor: recebe o texto do QR (da câmera ou digitado), localiza a OP e abre a etapa da estação. */
export function useLeitorDeQr() {
  const { estacoes, etapas } = useServicos();
  const [processando, setProcessando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  // A câmera dispara várias leituras do mesmo QR por segundo: o ref barra as repetidas antes do estado atualizar
  const ocupado = useRef(false);

  async function ler(conteudo: string): Promise<void> {
    if (ocupado.current) return;
    ocupado.current = true;
    setProcessando(true);
    setErro(null);
    try {
      const codigo = extrairCodigoDaOrdem(conteudo);
      if (!codigo) {
        setErro(MENSAGEM_QR_INVALIDO);
        vibrarAlerta();
        return;
      }
      const estacaoId = await estacoes.estacaoEscolhida();
      if (estacaoId === null) {
        router.replace('/estacao');
        return;
      }
      const etapaId = await etapas.abrirPorQr(codigo, estacaoId);
      vibrarSucesso();
      router.replace({ pathname: '/etapa/[id]', params: { id: String(etapaId) } });
    } catch (falha) {
      setErro(interpretarErro(falha).message);
      vibrarAlerta();
    } finally {
      ocupado.current = false;
      setProcessando(false);
    }
  }

  return { processando, erro, ler, limparErro: () => setErro(null) };
}
