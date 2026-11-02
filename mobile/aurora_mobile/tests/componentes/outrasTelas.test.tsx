import { render, screen, userEvent, waitFor } from '@testing-library/react-native';
import { router } from 'expo-router';
import { Alert, type AlertButton } from 'react-native';

import { TelaDeEstacoes } from '@/features/estacoes/TelaDeEstacoes';
import { TelaDeEstoque } from '@/features/estoque/TelaDeEstoque';
import { TelaDePedidos } from '@/features/pedidos/TelaDePedidos';
import { TelaDoPedido } from '@/features/pedidos/TelaDoPedido';
import { TelaDePerda } from '@/features/perdas/TelaDePerda';
import { TelaDeConfiguracoes } from '@/features/preferencias/TelaDeConfiguracoes';
import { TelaDeInspecao } from '@/features/qualidade/TelaDeInspecao';
import { montarApp } from '../helpers/montarApp';

const pedido = {
  id: 31,
  numero: 'PED-2026-00031',
  clienteNome: 'Móveis Lima',
  dataPedido: '2026-10-30T12:00:00',
  prazoEntrega: '2026-12-15',
  status: 'EmProducao',
  valorTotal: 51600,
  quantidadeItens: 2,
  atrasado: false,
};

describe('pedidos', () => {
  it('o cliente consulta /meus-pedidos e vê o estado em texto e ícone', async () => {
    const app = await montarApp('Cliente');
    app.http.get.mockResolvedValue({ data: { items: [pedido] } });

    await render(<TelaDePedidos />, { wrapper: app.Envolver });

    expect(await screen.findByText('PED-2026-00031')).toBeOnTheScreen();
    expect(screen.getByText('🏭 Em produção')).toBeOnTheScreen();
    expect(app.http.get).toHaveBeenCalledWith('/meus-pedidos', expect.anything());
    expect(screen.queryByText('Móveis Lima')).toBeNull(); // o cliente não precisa ver o próprio nome em cada cartão
  });

  it('o vendedor consulta /pedidos e vê o cliente de cada pedido', async () => {
    const app = await montarApp('Vendedor');
    app.http.get.mockResolvedValue({ data: { items: [pedido] } });

    await render(<TelaDePedidos />, { wrapper: app.Envolver });

    expect(await screen.findByText('Móveis Lima')).toBeOnTheScreen();
    expect(app.http.get).toHaveBeenCalledWith('/pedidos', expect.anything());
  });

  it('sem pedidos mostra o estado vazio; sem rede e sem cache explica a falta de conexão', async () => {
    const app = await montarApp('Cliente');
    app.http.get.mockResolvedValueOnce({ data: { items: [] } });
    await render(<TelaDePedidos />, { wrapper: app.Envolver });
    expect(await screen.findByText('Nenhum pedido')).toBeOnTheScreen();

    const semCache = await montarApp('Vendedor');
    await render(<TelaDePedidos />, { wrapper: semCache.Envolver });
    expect(await screen.findByText(/Sem conexão com o servidor/)).toBeOnTheScreen();
  });

  it('abre o detalhe do pedido ao tocar no cartão', async () => {
    const app = await montarApp('Cliente');
    app.http.get.mockResolvedValue({ data: { items: [pedido] } });
    await render(<TelaDePedidos />, { wrapper: app.Envolver });

    await userEvent.press(await screen.findByRole('button', { name: /Pedido PED-2026-00031/ }));

    expect(router.push).toHaveBeenCalledWith({ pathname: '/pedido/[id]', params: { id: '31' } });
  });

  it('o detalhe mostra itens e a linha do tempo da produção na ordem', async () => {
    const app = await montarApp('Cliente');
    app.http.get.mockResolvedValue({
      data: {
        pedido: {
          ...pedido,
          observacao: null,
          itens: [{ id: 1, produtoCodigo: 'CE-100', produtoNome: 'Cadeira Ergonômica Presidente', quantidade: 40, precoUnitario: 1290, subtotal: 51600 }],
        },
        eventos: [
          { dataHora: '2026-10-30T12:00:00', evento: 'Pedido registrado', numeroOp: null },
          { dataHora: '2026-10-31T09:30:00', evento: 'Etapa iniciada: Corte', numeroOp: 'OP-2026-00058' },
        ],
      },
    });

    await render(<TelaDoPedido pedidoId={31} />, { wrapper: app.Envolver });

    expect(await screen.findByText('40 × Cadeira Ergonômica Presidente')).toBeOnTheScreen();
    expect(screen.getByText('● Pedido registrado')).toBeOnTheScreen();
    expect(screen.getByText('● Etapa iniciada: Corte')).toBeOnTheScreen();
    expect(app.http.get).toHaveBeenCalledWith('/pedidos/31/acompanhamento');
  });
});

describe('estoque', () => {
  const saldo = (sobrescrever = {}) => ({
    id: 9, codigo: 'MAD-01', nome: 'Madeira maciça', unidade: 'm3', estoqueAtual: 0.2, estoqueMinimo: 0.5, critico: true, ...sobrescrever,
  });

  it('mostra o saldo, o mínimo e o alerta de crítico com ícone e texto', async () => {
    const app = await montarApp();
    app.http.get.mockResolvedValue({ data: { items: [saldo(), saldo({ id: 10, nome: 'Tecido', unidade: 'm', estoqueAtual: 40, critico: false })] } });

    await render(<TelaDeEstoque />, { wrapper: app.Envolver });

    expect(await screen.findByText('Madeira maciça')).toBeOnTheScreen();
    expect(screen.getByText('⚠ Estoque crítico (no mínimo ou abaixo)')).toBeOnTheScreen();
    expect(screen.getByText('✔ Estoque normal')).toBeOnTheScreen();
    expect(screen.getByText(/Atualizado em/)).toBeOnTheScreen();
  });

  it('sem rede mostra o último saldo conhecido avisando a hora dele', async () => {
    const app = await montarApp();
    app.http.get.mockResolvedValueOnce({ data: { items: [saldo()] } });
    const primeira = await render(<TelaDeEstoque />, { wrapper: app.Envolver });
    await screen.findByText('Madeira maciça');
    await primeira.unmount();

    await render(<TelaDeEstoque />, { wrapper: app.Envolver }); // o http falso volta a falhar por falta de rede

    expect(await screen.findByText('Madeira maciça')).toBeOnTheScreen();
    expect(screen.getByText(/Sem conexão\. Último saldo conhecido/)).toBeOnTheScreen();
  });
});

describe('escolher estação', () => {
  it('lista as estações que geram etapa e guarda a escolhida', async () => {
    const app = await montarApp('Operador', null);
    await render(<TelaDeEstacoes />, { wrapper: app.Envolver });

    await userEvent.press(await screen.findByRole('button', { name: 'Montagem da Estrutura' }));

    expect(router.replace).toHaveBeenCalledWith('/etapas');
    expect(await app.servicos.estacoes.estacaoEscolhida()).toBe(2);
  });
});

describe('registrar perda', () => {
  it('campos em branco mostram o que falta e não chamam a API', async () => {
    const app = await montarApp();
    await render(<TelaDePerda etapaId={341} />, { wrapper: app.Envolver });

    await userEvent.press(screen.getByRole('button', { name: 'Registrar perda' }));

    expect(screen.getByText(/Informe a quantidade/)).toBeOnTheScreen();
    expect(app.api.registrarPerda).not.toHaveBeenCalled();
  });

  it('registra refugo com quantidade e motivo e confirma', async () => {
    const app = await montarApp();
    await render(<TelaDePerda etapaId={341} />, { wrapper: app.Envolver });

    await userEvent.type(screen.getByLabelText('Quantidade'), '3');
    await userEvent.type(screen.getByLabelText('Motivo'), 'Peça rachada no corte');
    await userEvent.press(screen.getByRole('button', { name: 'Registrar perda' }));

    await waitFor(() =>
      expect(app.api.registrarPerda).toHaveBeenCalledWith(341, { tipo: 'Refugo', quantidade: 3, motivo: 'Peça rachada no corte' }),
    );
    expect(await screen.findByText(/Refugo de 3 un\. registrado/)).toBeOnTheScreen();
  });

  it('retrabalho vai com o tipo escolhido', async () => {
    const app = await montarApp();
    await render(<TelaDePerda etapaId={341} />, { wrapper: app.Envolver });

    await userEvent.press(screen.getByRole('button', { name: 'Retrabalho' }));
    await userEvent.type(screen.getByLabelText('Quantidade'), '1');
    await userEvent.type(screen.getByLabelText('Motivo'), 'Costura torta');
    await userEvent.press(screen.getByRole('button', { name: 'Registrar perda' }));

    await waitFor(() => expect(app.api.registrarPerda).toHaveBeenCalledWith(341, expect.objectContaining({ tipo: 'Retrabalho' })));
  });

  it('sem conexão o botão fica desabilitado e a tela avisa', async () => {
    const app = await montarApp();
    await app.definirOnline(false);
    await render(<TelaDePerda etapaId={341} />, { wrapper: app.Envolver });

    expect(screen.getByRole('button', { name: 'Registrar perda' })).toBeDisabled();
    expect(screen.getByText(/exige internet/)).toBeOnTheScreen();
  });
});

describe('inspeção de qualidade', () => {
  const ordem = {
    ordem: { id: 58, numero: 'OP-2026-00058' },
    etapas: [
      { id: 1, estacaoId: 1, estacaoNome: 'Corte', sequencia: 1, status: 'Concluida' },
      { id: 2, estacaoId: 2, estacaoNome: 'Montagem da Estrutura', sequencia: 2, status: 'Concluida' },
      { id: 3, estacaoId: 5, estacaoNome: 'Qualidade', sequencia: 3, status: 'EmAndamento' },
    ],
  };

  async function abrir() {
    const app = await montarApp('Inspetor', 5);
    app.doServidor.ordem = ordem as never;
    await render(<TelaDeInspecao ordemId={58} />, { wrapper: app.Envolver });
    return app;
  }

  it('confirmar fica desabilitado até escolher o resultado', async () => {
    await abrir();

    expect(await screen.findByRole('button', { name: 'Confirmar inspeção' })).toBeDisabled();
  });

  it('reprovar exige a estação de retrabalho e só oferece as anteriores à Qualidade', async () => {
    await abrir();

    await userEvent.press(await screen.findByRole('button', { name: 'Reprovar' }));

    expect(await screen.findByRole('button', { name: 'Corte' })).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Montagem da Estrutura' })).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Qualidade' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Confirmar inspeção' })).toBeDisabled();
  });

  it('reprovar com estação e observação envia a inspeção e conta o que acontece com a produção', async () => {
    const app = await abrir();
    app.http.post.mockResolvedValue({ data: { id: 1, numeroOp: 'OP-2026-00058', resultado: 'Reprovada', statusOrdem: 'EmAndamento', statusPedido: 'EmProducao' } });

    await userEvent.press(await screen.findByRole('button', { name: 'Reprovar' }));
    await userEvent.press(await screen.findByRole('button', { name: 'Montagem da Estrutura' }));
    await userEvent.type(screen.getByLabelText('Observação (opcional)'), 'Estrutura torta');
    await userEvent.press(screen.getByRole('button', { name: 'Confirmar inspeção' }));

    await waitFor(() =>
      expect(app.http.post).toHaveBeenCalledWith('/ordens-producao/58/inspecoes', {
        resultado: 'Reprovada',
        estacaoRetrabalhoId: 2,
        observacao: 'Estrutura torta',
      }),
    );
    expect(await screen.findByText(/reprovada\. A produção volta para a estação de retrabalho/)).toBeOnTheScreen();
  });

  it('aprovar avisa quando o pedido ficou pronto para expedição', async () => {
    const app = await abrir();
    app.http.post.mockResolvedValue({ data: { id: 1, numeroOp: 'OP-2026-00058', resultado: 'Aprovada', statusOrdem: 'Concluida', statusPedido: 'Pronto' } });

    await userEvent.press(await screen.findByRole('button', { name: 'Aprovar' }));
    await userEvent.press(screen.getByRole('button', { name: 'Confirmar inspeção' }));

    expect(await screen.findByText(/aprovada e concluída\. O pedido está pronto para expedição/)).toBeOnTheScreen();
  });
});

describe('sair com segurança', () => {
  const pendente = {
    clientRequestId: 'p',
    tipo: 'Inicio' as const,
    etapaId: 1,
    numeroOp: 'OP-1',
    estacaoNome: 'Corte',
    dataHoraCliente: '2026-11-02T12:00:00.000Z',
  };

  function botoesDoAlerta(alerta: jest.SpyInstance): AlertButton[] {
    return alerta.mock.calls[0][2] as AlertButton[];
  }

  it('sem nada pendente sai direto, sem perguntar', async () => {
    const app = await montarApp();
    const alerta = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
    await render(<TelaDeConfiguracoes />, { wrapper: app.Envolver });

    await userEvent.press(await screen.findByRole('button', { name: 'Sair' }));

    await waitFor(() => expect(app.auth.sair).toHaveBeenCalledTimes(1));
    expect(alerta).not.toHaveBeenCalled();
    alerta.mockRestore();
  });

  it('com apontamento não enviado e sem rede avisa que será perdido; "Ficar" mantém tudo', async () => {
    const app = await montarApp();
    await app.fila.enfileirar(pendente);
    await app.definirOnline(false);
    const alerta = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
    await render(<TelaDeConfiguracoes />, { wrapper: app.Envolver });

    await userEvent.press(await screen.findByRole('button', { name: 'Sair' }));

    await waitFor(() => expect(alerta).toHaveBeenCalledTimes(1));
    expect(alerta.mock.calls[0][1]).toMatch(/1 apontamento\(s\) ainda não chegaram ao servidor/);
    botoesDoAlerta(alerta).find((b) => b.text === 'Ficar')?.onPress?.();
    await Promise.resolve();
    expect(app.auth.sair).not.toHaveBeenCalled();
    expect(await app.fila.contar()).toMatchObject({ pendentes: 1 });
    alerta.mockRestore();
  });

  it('"Sair mesmo assim" sai depois da confirmação', async () => {
    const app = await montarApp();
    await app.fila.enfileirar(pendente);
    await app.definirOnline(false);
    const alerta = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
    await render(<TelaDeConfiguracoes />, { wrapper: app.Envolver });

    await userEvent.press(await screen.findByRole('button', { name: 'Sair' }));
    await waitFor(() => expect(alerta).toHaveBeenCalled());
    botoesDoAlerta(alerta).find((b) => b.text === 'Sair mesmo assim')?.onPress?.();

    await waitFor(() => expect(app.auth.sair).toHaveBeenCalledTimes(1));
    alerta.mockRestore();
  });

  it('com rede, tenta enviar antes de perguntar: o que chegou ao servidor não é perdido', async () => {
    const app = await montarApp();
    await app.fila.enfileirar(pendente);
    const alerta = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
    await render(<TelaDeConfiguracoes />, { wrapper: app.Envolver });

    await userEvent.press(await screen.findByRole('button', { name: 'Sair' }));

    await waitFor(() => expect(app.auth.sair).toHaveBeenCalledTimes(1));
    expect(app.servidor.efeitos.get('p')).toBe(1);
    expect(alerta).not.toHaveBeenCalled();
    alerta.mockRestore();
  });

  it('o tamanho do texto escolhido fica salvo', async () => {
    const app = await montarApp();
    await render(<TelaDeConfiguracoes />, { wrapper: app.Envolver });

    await userEvent.press(await screen.findByRole('button', { name: 'Muito grande' }));

    await waitFor(async () => expect(await app.servicos.preferencias.obter('escala_fonte')).toBe('1.3'));
  });
});
