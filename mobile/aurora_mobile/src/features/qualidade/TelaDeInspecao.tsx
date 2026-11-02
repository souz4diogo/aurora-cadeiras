import { router } from 'expo-router';
import { useCallback, useState } from 'react';
import { View } from 'react-native';

import { useServicos } from '@/core/ServicosProvider';
import { interpretarErro } from '@/core/http/problem';
import type { InspecaoRegistrada, ResultadoDaInspecao } from '@/features/qualidade/inspecaoService';
import { useSync } from '@/features/sync/SyncProvider';
import { Aviso, Botao, CampoDeTexto, Tela, Texto } from '@/shared/componentes';
import { useCarregamento } from '@/shared/useCarregamento';
import { vibrarAlerta, vibrarSucesso } from '@/shared/vibracao';

/**
 * Inspeção de qualidade (RF18, RN06). Aprovar conclui a OP; reprovar exige a estação de retrabalho, de onde a
 * produção recomeça. Exige conexão: quem decide é o servidor.
 */
export function TelaDeInspecao({ ordemId }: { ordemId: number }) {
  const { inspecao, etapas, estacoes } = useServicos();
  const { online, atualizarContagem } = useSync();
  const carregarOpcoes = useCallback(() => inspecao.opcoesDeRetrabalho(ordemId), [inspecao, ordemId]);
  const opcoes = useCarregamento(carregarOpcoes);

  const [resultado, setResultado] = useState<ResultadoDaInspecao | null>(null);
  const [estacaoId, setEstacaoId] = useState<number | null>(null);
  const [observacao, setObservacao] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [registrada, setRegistrada] = useState<InspecaoRegistrada | null>(null);

  async function registrar(): Promise<void> {
    if (!resultado) return;
    setErro(null);
    setEnviando(true);
    try {
      const feita = await inspecao.registrar(ordemId, {
        resultado,
        estacaoRetrabalhoId: estacaoId ?? undefined,
        observacao,
      });
      vibrarSucesso();
      setRegistrada(feita);
      // A OP mudou de etapa no servidor: atualiza a fila da estação para a tela anterior já refletir
      const estacao = await estacoes.estacaoEscolhida();
      if (estacao !== null) await etapas.atualizarCache(estacao).catch(() => undefined);
      await atualizarContagem();
    } catch (falha) {
      vibrarAlerta();
      setErro(interpretarErro(falha).message);
    } finally {
      setEnviando(false);
    }
  }

  if (registrada) {
    return (
      <Tela>
        <Aviso
          tipo="sucesso"
          texto={
            registrada.resultado === 'Aprovada'
              ? `OP ${registrada.numeroOp} aprovada e concluída.${registrada.statusPedido === 'Pronto' ? ' O pedido está pronto para expedição.' : ''}`
              : `OP ${registrada.numeroOp} reprovada. A produção volta para a estação de retrabalho.`
          }
        />
        <Botao titulo="Voltar às etapas" aoPressionar={() => router.dismissTo('/etapas')} />
      </Tela>
    );
  }

  const precisaDaEstacao = resultado === 'Reprovada' && estacaoId === null;

  return (
    <Tela carregando={opcoes.carregando}>
      {!online ? <Aviso tipo="aviso" texto="Sem conexão. A inspeção exige internet." /> : null}
      {erro ? <Aviso tipo="erro" texto={erro} /> : null}
      {opcoes.erro ? <Aviso tipo="erro" texto={opcoes.erro} /> : null}

      <Texto negrito tamanho="grande" accessibilityRole="header">Resultado da inspeção</Texto>
      <View style={{ flexDirection: 'row', gap: 12 }}>
        <Botao
          titulo={resultado === 'Aprovada' ? '✔ Aprovar' : 'Aprovar'}
          variante={resultado === 'Aprovada' ? 'primario' : 'secundario'}
          estilo={{ flex: 1 }}
          aoPressionar={() => setResultado('Aprovada')}
        />
        <Botao
          titulo={resultado === 'Reprovada' ? '✔ Reprovar' : 'Reprovar'}
          variante={resultado === 'Reprovada' ? 'perigo' : 'secundario'}
          estilo={{ flex: 1 }}
          aoPressionar={() => setResultado('Reprovada')}
        />
      </View>

      {resultado === 'Reprovada' ? (
        <View style={{ gap: 10 }}>
          <Texto negrito>Voltar a produção para qual estação?</Texto>
          {opcoes.dados?.map((opcao) => (
            <Botao
              key={opcao.estacaoId}
              titulo={estacaoId === opcao.estacaoId ? `✔ ${opcao.nome}` : opcao.nome}
              variante={estacaoId === opcao.estacaoId ? 'primario' : 'secundario'}
              aoPressionar={() => setEstacaoId(opcao.estacaoId)}
            />
          ))}
          {precisaDaEstacao ? <Aviso tipo="aviso" texto="Escolha a estação de retrabalho para reprovar." /> : null}
        </View>
      ) : null}

      {resultado ? (
        <CampoDeTexto
          rotulo="Observação (opcional)"
          value={observacao}
          onChangeText={setObservacao}
          multiline
          maxLength={300}
          placeholder="Ex.: costura do encosto torta"
        />
      ) : null}

      <Botao
        titulo="Confirmar inspeção"
        variante={resultado === 'Reprovada' ? 'perigo' : 'primario'}
        desabilitado={!resultado || precisaDaEstacao || !online}
        carregando={enviando}
        aoPressionar={() => void registrar()}
      />
    </Tela>
  );
}
