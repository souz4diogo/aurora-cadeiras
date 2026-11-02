import { router } from 'expo-router';
import { View } from 'react-native';

import { useUsuario } from '@/features/auth/AuthProvider';
import { StatusDaEtapa } from '@/features/etapas/StatusDaEtapa';
import { useDetalheDaEtapa } from '@/features/etapas/useDetalheDaEtapa';
import { Aviso, Botao, Cartao, EstadoVazio, Tela, Texto } from '@/shared/componentes';
import { formatarData, formatarHora, formatarQuantidade } from '@/shared/formatar';

/** Detalhe da etapa (RF11, RF13): produto, quantidade, materiais e os botões grandes de Iniciar e Finalizar. */
export function TelaDeDetalheDaEtapa({ etapaId }: { etapaId: number }) {
  const usuario = useUsuario();
  const vm = useDetalheDaEtapa(etapaId, usuario.perfil);
  const { etapa, acoes } = vm;

  if (vm.carregando) return <Tela carregando>{null}</Tela>;
  if (!etapa || !acoes) {
    return (
      <Tela>
        <EstadoVazio
          titulo="Etapa não encontrada"
          texto="Ela pode ter sido concluída ou não estar mais nesta estação. Volte e atualize a lista."
          acao={<Botao titulo="Voltar" variante="secundario" aoPressionar={() => router.back()} />}
        />
      </Tela>
    );
  }

  const bloqueadoPorRede = acoes.precisaEnviarAntes ? 'Envie o início antes: ele ainda está só no aparelho.' : null;

  return (
    <Tela>
      {vm.mensagem ? (
        <Aviso tipo={vm.mensagem.tipo === 'erro' ? 'erro' : vm.mensagem.tipo === 'aviso' ? 'aviso' : 'sucesso'} texto={vm.mensagem.texto} />
      ) : null}

      <Cartao>
        <Texto tamanho="titulo" negrito accessibilityRole="header">{etapa.numeroOp}</Texto>
        <Texto tamanho="grande">{etapa.produtoNome}</Texto>
        <Texto secundario>{`${etapa.produtoCodigo} · ${etapa.quantidade} unidades`}</Texto>
        <Texto secundario>{`Pedido ${etapa.pedidoNumero}`}</Texto>
        {etapa.previsaoConclusao ? <Texto secundario>{`Previsão: ${formatarData(etapa.previsaoConclusao)}`}</Texto> : null}
        <Texto negrito>{`Estação: ${etapa.estacaoNome}`}</Texto>
        <StatusDaEtapa etapa={etapa} />
        {etapa.iniciadaEm ? <Texto secundario>{`Iniciada às ${formatarHora(etapa.iniciadaEm)}`}</Texto> : null}
      </Cartao>

      {vm.materiais && vm.materiais.length > 0 ? (
        <Cartao>
          <Texto negrito accessibilityRole="header">Materiais que esta etapa consome</Texto>
          {vm.materiais.map((m) => (
            <Texto key={m.materiaPrimaId}>
              {`${m.materiaPrimaNome}: ${formatarQuantidade(m.quantidade * etapa.quantidade)} ${m.unidade}`}
              <Texto secundario tamanho="pequeno">{`  (${formatarQuantidade(m.quantidade)} ${m.unidade} por unidade)`}</Texto>
            </Texto>
          ))}
          <Texto secundario tamanho="pequeno">A baixa do estoque acontece ao iniciar a etapa.</Texto>
        </Cartao>
      ) : null}

      {acoes.explicacao ? <Aviso tipo="info" texto={acoes.explicacao} /> : null}

      <View style={{ gap: 12 }}>
        {acoes.iniciar ? (
          <Botao
            titulo="Iniciar etapa"
            dica="Registra o início e consome o material da etapa"
            carregando={vm.apontando}
            aoPressionar={() => void vm.iniciar()}
          />
        ) : null}
        {acoes.finalizar ? (
          <Botao
            titulo="Finalizar etapa"
            dica="Registra o fim da etapa"
            carregando={vm.apontando}
            aoPressionar={() => void vm.finalizar()}
          />
        ) : null}
        {acoes.inspecionar ? (
          <Botao
            titulo="Registrar inspeção"
            dica="Aprova ou reprova a ordem de produção"
            desabilitado={acoes.precisaEnviarAntes || !vm.online}
            aoPressionar={() =>
              router.push({ pathname: '/inspecao/[ordemId]', params: { ordemId: String(etapa.ordemProducaoId) } })
            }
          />
        ) : null}
        {acoes.registrarPerda ? (
          <Botao
            titulo="Registrar perda"
            variante="secundario"
            dica="Anota refugo ou retrabalho. Exige conexão"
            desabilitado={acoes.precisaEnviarAntes || !vm.online}
            aoPressionar={() => router.push({ pathname: '/perda/[etapaId]', params: { etapaId: String(etapa.etapaId) } })}
          />
        ) : null}
      </View>

      {(acoes.inspecionar || acoes.registrarPerda) && !vm.online ? (
        <Aviso tipo="aviso" texto="Sem conexão. Registrar perda e inspeção exige internet." />
      ) : null}
      {bloqueadoPorRede ? <Aviso tipo="aviso" texto={bloqueadoPorRede} /> : null}
    </Tela>
  );
}
