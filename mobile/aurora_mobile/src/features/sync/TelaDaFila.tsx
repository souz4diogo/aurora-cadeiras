import { useCallback, useEffect } from 'react';
import { Alert, View } from 'react-native';

import { useServicos } from '@/core/ServicosProvider';
import { useSync } from '@/features/sync/SyncProvider';
import type { ClientePendente } from '@/features/clientes/tipos';
import type { ApontamentoPendente } from '@/features/sync/tipos';
import { Aviso, Botao, Cartao, EstadoVazio, Tela, Texto } from '@/shared/componentes';
import { formatarDataEHora } from '@/shared/formatar';
import { useCarregamento } from '@/shared/useCarregamento';
import { useTema } from '@/shared/tema';

export function descreverEstado(item: Pick<ApontamentoPendente, 'estado'>): string {
  return item.estado === 'Pendente' ? '⏳ Aguardando envio' : item.estado === 'Enviando' ? '⬆ Enviando' : '✖ Rejeitado';
}

/** Fila de sincronização (RF23): o que falta enviar, o que o servidor recusou e por quê, com as ações possíveis. */
export function TelaDaFila() {
  const { fila, clientes: clientesService } = useServicos();
  const {
    online,
    sincronizando,
    contagem,
    clientes: contagemDeClientes,
    ultimaRodada,
    sincronizarAgora,
    atualizarContagem,
  } = useSync();
  const { cores } = useTema();
  const carregar = useCallback(() => fila.listar(), [fila]);
  const lista = useCarregamento(carregar);
  const { definir } = lista;
  const carregarClientes = useCallback(() => clientesService.listar(), [clientesService]);
  const listaDeClientes = useCarregamento(carregarClientes);
  const { definir: definirClientes } = listaDeClientes;

  // Relê a fila quando uma rodada termina ou a contagem muda
  useEffect(() => {
    void fila.listar().then((itens) => definir(() => itens));
  }, [fila, definir, contagem.pendentes, contagem.enviando, contagem.rejeitados, ultimaRodada]);

  useEffect(() => {
    void clientesService.listar().then((itens) => definirClientes(() => itens));
  }, [
    clientesService,
    definirClientes,
    contagemDeClientes.pendentes,
    contagemDeClientes.enviando,
    contagemDeClientes.rejeitados,
    ultimaRodada,
  ]);

  async function reenviarCliente(cliente: ClientePendente): Promise<void> {
    await clientesService.reenviar(cliente.id);
    await atualizarContagem();
    await sincronizarAgora().catch(() => undefined);
  }

  function descartarCliente(cliente: ClientePendente): void {
    Alert.alert('Descartar cliente?', `O cadastro de ${cliente.nome} não será enviado. Isso não pode ser desfeito.`, [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Descartar',
        style: 'destructive',
        onPress: () => {
          void clientesService.descartar(cliente.id).then(atualizarContagem);
        },
      },
    ]);
  }

  async function reenviar(item: ApontamentoPendente): Promise<void> {
    await fila.reenviar(item.clientRequestId);
    await atualizarContagem();
    await sincronizarAgora().catch(() => undefined);
  }

  function descartar(item: ApontamentoPendente): void {
    Alert.alert(
      'Descartar apontamento?',
      `O ${item.tipo === 'Inicio' ? 'início' : 'fim'} da etapa em ${item.estacaoNome} (${item.numeroOp}) não será enviado. Isso não pode ser desfeito.`,
      [
        { text: 'Cancelar', style: 'cancel' },
        {
          text: 'Descartar',
          style: 'destructive',
          onPress: () => {
            void fila.descartar(item.clientRequestId).then(atualizarContagem);
          },
        },
      ],
    );
  }

  const itens = lista.dados ?? [];
  const clientes = listaDeClientes.dados ?? [];
  const aEnviar = contagem.pendentes + contagem.enviando + contagemDeClientes.pendentes + contagemDeClientes.enviando;
  const rejeitados = contagem.rejeitados + contagemDeClientes.rejeitados;

  return (
    <Tela carregando={lista.carregando} aoAtualizar={() => void sincronizarAgora().then(lista.recarregar)} atualizando={lista.atualizando}>
      <Texto secundario>
        {`${aEnviar} a enviar · ${rejeitados} rejeitados · ${online ? 'online' : 'sem conexão'}`}
      </Texto>
      <Botao
        titulo={sincronizando ? 'Enviando…' : 'Enviar agora'}
        dica="Tenta enviar ao servidor os apontamentos e os clientes pendentes"
        carregando={sincronizando}
        desabilitado={!online || contagem.pendentes + contagemDeClientes.pendentes === 0}
        aoPressionar={() => void sincronizarAgora().catch(() => undefined)}
      />
      {!online ? <Aviso tipo="aviso" texto="Sem conexão. Os apontamentos ficam guardados e são enviados quando a internet voltar." /> : null}
      {lista.erro ? <Aviso tipo="erro" texto={lista.erro} /> : null}

      {itens.length === 0 && clientes.length === 0 && !lista.carregando ? (
        <EstadoVazio titulo="Fila vazia" texto="Tudo o que você apontou ou cadastrou já foi enviado ao servidor." />
      ) : null}

      {itens.map((item) => (
        <Cartao
          key={item.clientRequestId}
          rotuloDeAcessibilidade={`${item.tipo === 'Inicio' ? 'Início' : 'Fim'} em ${item.estacaoNome}, ${item.numeroOp}. ${descreverEstado(item)}${item.mensagem ? `. ${item.mensagem}` : ''}`}
        >
          <Texto negrito>{`${item.tipo === 'Inicio' ? 'Início' : 'Fim'} · ${item.estacaoNome}`}</Texto>
          <Texto secundario tamanho="pequeno">{`${item.numeroOp} · ${formatarDataEHora(item.dataHoraCliente)}`}</Texto>
          <Texto negrito cor={item.estado === 'Rejeitado' ? cores.perigo : cores.texto}>{descreverEstado(item)}</Texto>
          {item.mensagem ? <Texto cor={cores.perigo}>{item.mensagem}</Texto> : null}
          {item.tentativas > 0 && item.estado !== 'Rejeitado' ? (
            <Texto secundario tamanho="pequeno">{`${item.tentativas} tentativa(s) sem sucesso`}</Texto>
          ) : null}
          {item.estado === 'Rejeitado' ? (
            <View style={{ flexDirection: 'row', gap: 12, marginTop: 8 }}>
              <Botao titulo="Tentar de novo" estilo={{ flex: 1 }} dica="Envia este apontamento mais uma vez" aoPressionar={() => void reenviar(item)} />
              <Botao titulo="Descartar" variante="perigo" estilo={{ flex: 1 }} aoPressionar={() => descartar(item)} />
            </View>
          ) : null}
        </Cartao>
      ))}

      {clientes.length > 0 ? <Texto negrito>Clientes cadastrados neste aparelho</Texto> : null}
      {clientes.map((cliente) => (
        <Cartao
          key={cliente.id}
          rotuloDeAcessibilidade={`Cliente ${cliente.nome}. ${descreverEstado(cliente)}${cliente.mensagem ? `. ${cliente.mensagem}` : ''}`}
        >
          <Texto negrito>{cliente.nome}</Texto>
          <Texto secundario tamanho="pequeno">{`Cadastro de cliente · ${formatarDataEHora(cliente.criadoEm)}`}</Texto>
          <Texto negrito cor={cliente.estado === 'Rejeitado' ? cores.perigo : cores.texto}>{descreverEstado(cliente)}</Texto>
          {cliente.mensagem ? <Texto cor={cores.perigo}>{cliente.mensagem}</Texto> : null}
          {cliente.estado === 'Rejeitado' ? (
            <View style={{ flexDirection: 'row', gap: 12, marginTop: 8 }}>
              <Botao titulo="Tentar de novo" estilo={{ flex: 1 }} dica="Envia este cliente mais uma vez" aoPressionar={() => void reenviarCliente(cliente)} />
              <Botao titulo="Descartar" variante="perigo" estilo={{ flex: 1 }} aoPressionar={() => descartarCliente(cliente)} />
            </View>
          ) : null}
        </Cartao>
      ))}
    </Tela>
  );
}
