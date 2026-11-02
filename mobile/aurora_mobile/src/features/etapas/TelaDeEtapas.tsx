import { Redirect, router } from 'expo-router';
import { View } from 'react-native';

import { descreverEtapa, StatusDaEtapa } from '@/features/etapas/StatusDaEtapa';
import type { EtapaNaTela } from '@/features/etapas/tipos';
import { useFilaDaEstacao } from '@/features/etapas/useFilaDaEstacao';
import { Aviso, Botao, Cartao, EstadoVazio, Tela, Texto } from '@/shared/componentes';
import { formatarData, formatarHora } from '@/shared/formatar';

/** Minhas etapas (RF11): fila da estação com o estado de cada etapa, atalhos e o horário da última atualização. */
export function TelaDeEtapas() {
  const fila = useFilaDaEstacao();
  const dados = fila.dados;

  if (dados && dados.estacaoId === null) return <Redirect href="/estacao" />;

  return (
    <Tela carregando={fila.carregando} aoAtualizar={() => void fila.recarregar()} atualizando={fila.atualizando}>
      {fila.erro ? <Aviso tipo="erro" texto={fila.erro} /> : null}
      {dados?.doCache ? (
        <Aviso
          tipo="aviso"
          texto={`Sem conexão. Mostrando os dados do aparelho${dados.atualizadoEm ? `, de ${formatarHora(dados.atualizadoEm)}` : ''}.`}
        />
      ) : null}

      <View style={{ gap: 12 }}>
        <Botao
          titulo="Ler QR code"
          dica="Abre a câmera para ler a etiqueta da ordem de produção"
          aoPressionar={() => router.push('/qr')}
        />
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <Botao titulo="Estoque" variante="secundario" estilo={{ flex: 1 }} aoPressionar={() => router.push('/estoque')} />
          <Botao titulo="Trocar estação" variante="secundario" estilo={{ flex: 1 }} aoPressionar={() => router.push('/estacao')} />
        </View>
      </View>

      {dados && dados.etapas.length === 0 && !fila.erro ? (
        <EstadoVazio
          titulo="Nenhuma etapa na fila"
          texto="Quando uma ordem de produção chegar nesta estação, ela aparece aqui. Puxe a tela para atualizar."
        />
      ) : null}

      {dados?.etapas.map((etapa) => <CartaoDeEtapa key={etapa.etapaId} etapa={etapa} />)}

      <Botao titulo="Configurações" variante="secundario" aoPressionar={() => router.push('/configuracoes')} />
    </Tela>
  );
}

function CartaoDeEtapa({ etapa }: { etapa: EtapaNaTela }) {
  return (
    <Cartao
      aoPressionar={() => router.push({ pathname: '/etapa/[id]', params: { id: String(etapa.etapaId) } })}
      rotuloDeAcessibilidade={`${etapa.numeroOp}, ${etapa.produtoNome}, ${etapa.quantidade} unidades. ${descreverEtapa(etapa)}`}
      dica="Abre os detalhes da etapa"
    >
      <Texto negrito tamanho="grande">{etapa.numeroOp}</Texto>
      <Texto>{`${etapa.produtoNome} · ${etapa.quantidade} un.`}</Texto>
      <Texto secundario tamanho="pequeno">
        {`${etapa.estacaoNome}${etapa.previsaoConclusao ? ` · previsão ${formatarData(etapa.previsaoConclusao)}` : ''}`}
      </Texto>
      <StatusDaEtapa etapa={etapa} />
    </Cartao>
  );
}
