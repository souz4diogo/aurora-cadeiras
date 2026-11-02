import { useCallback, useState } from 'react';

import { useServicos } from '@/core/ServicosProvider';
import { acoesDaEtapa } from '@/features/etapas/acoes';
import type { EtapaNaTela, MaterialDaEtapa } from '@/features/etapas/tipos';
import { useSync } from '@/features/sync/SyncProvider';
import type { TipoDeApontamento } from '@/features/sync/tipos';
import type { Perfil } from '@/core/auth/tokenStore';
import { vibrarAlerta, vibrarSucesso } from '@/shared/vibracao';
import { useCarregamento } from '@/shared/useCarregamento';

export type TipoDeMensagem = 'sucesso' | 'aviso' | 'erro';

export interface MensagemDoApontamento {
  tipo: TipoDeMensagem;
  texto: string;
}

interface DadosDaEtapa {
  etapa: EtapaNaTela | null;
  materiais: MaterialDaEtapa[] | null;
}

/** Texto da confirmação depois de apontar: servidor confirmou, ficou no aparelho ou foi recusado. */
export function mensagemDoApontamento(
  tipo: TipoDeApontamento,
  situacao: 'confirmado' | 'guardado' | 'rejeitado',
  motivo?: string | null,
): MensagemDoApontamento {
  const acao = tipo === 'Inicio' ? 'Início' : 'Fim';
  if (situacao === 'confirmado') {
    return { tipo: 'sucesso', texto: `${acao} da etapa registrado e confirmado pelo servidor.` };
  }
  if (situacao === 'guardado') {
    return { tipo: 'aviso', texto: `${acao} salvo no aparelho. Será enviado quando houver conexão.` };
  }
  return { tipo: 'erro', texto: `${acao} recusado pelo servidor: ${motivo ?? 'motivo não informado'}` };
}

/**
 * ViewModel do detalhe da etapa. Apontar grava na fila local, tenta enviar na hora e conta ao operador o que
 * aconteceu (docs/08 §3 e §5): o toque nunca depende da rede para ser registrado.
 */
export function useDetalheDaEtapa(etapaId: number, perfil: Perfil) {
  const { etapas, fila, materiais } = useServicos();
  const { online, sincronizarAgora, atualizarContagem } = useSync();
  const [mensagem, setMensagem] = useState<MensagemDoApontamento | null>(null);
  const [apontando, setApontando] = useState(false);

  const carregar = useCallback(async (): Promise<DadosDaEtapa> => {
    const etapa = await etapas.obterEtapa(etapaId);
    if (!etapa) return { etapa: null, materiais: null };
    // Materiais são um complemento: sem rede e sem cache a tela segue sem eles
    const lista = await materiais.daEtapa(etapa.produtoId, etapa.estacaoId).then(
      (r) => r.dados,
      () => null,
    );
    return { etapa, materiais: lista };
  }, [etapas, materiais, etapaId]);
  const estado = useCarregamento(carregar);
  const etapaAtual = estado.dados?.etapa ?? null;

  /** Tenta enviar o item recém-gravado. Uma rodada pode já estar em curso e ter começado antes do toque: se ele ficou para trás, roda de novo. */
  async function enviar(id: string): Promise<void> {
    for (let tentativa = 0; tentativa < 2; tentativa++) {
      await sincronizarAgora().catch(() => undefined);
      const item = await buscarNaFila(id);
      if (!item || item.estado !== 'Pendente' || !online) return;
    }
  }

  async function buscarNaFila(id: string) {
    return (await fila.listar()).find((i) => i.clientRequestId === id);
  }

  async function apontar(tipo: TipoDeApontamento): Promise<void> {
    if (!etapaAtual || apontando) return;
    setApontando(true);
    setMensagem(null);
    try {
      const id = await etapas.apontar(tipo, etapaAtual);
      await atualizarContagem();
      await enviar(id);

      // Fora da fila = o servidor aceitou; rejeitado = ficou com o motivo; pendente = esperando rede
      const item = await buscarNaFila(id);
      const situacao = !item ? 'confirmado' : item.estado === 'Rejeitado' ? 'rejeitado' : 'guardado';
      (situacao === 'rejeitado' ? vibrarAlerta : vibrarSucesso)();
      setMensagem(mensagemDoApontamento(tipo, situacao, item?.mensagem));

      const resultado = await etapaAposApontar(tipo, situacao, etapaAtual);
      estado.definir((anterior) => (anterior ? { ...anterior, etapa: resultado } : anterior));
    } finally {
      setApontando(false);
    }
  }

  /** A etapa concluída some do cache do servidor; sem ela, a tela segue com o que o toque produziu. */
  async function etapaAposApontar(
    tipo: TipoDeApontamento,
    situacao: 'confirmado' | 'guardado' | 'rejeitado',
    anterior: EtapaNaTela,
  ): Promise<EtapaNaTela> {
    const atualizada = await etapas.obterEtapa(etapaId);
    if (atualizada) return atualizada;
    if (situacao === 'rejeitado') return anterior;
    return { ...anterior, status: tipo === 'Inicio' ? 'EmAndamento' : 'Concluida', pendenteEnvio: false };
  }

  return {
    ...estado,
    etapa: etapaAtual,
    materiais: estado.dados?.materiais ?? null,
    acoes: etapaAtual ? acoesDaEtapa(etapaAtual, perfil) : null,
    online,
    mensagem,
    apontando,
    iniciar: () => apontar('Inicio'),
    finalizar: () => apontar('Fim'),
  };
}
