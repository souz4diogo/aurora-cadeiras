import { render, screen } from '@testing-library/react-native';

import { TelaDeEtapas } from '@/features/etapas/TelaDeEtapas';
import { etapa } from '../helpers/dados';
import { montarApp } from '../helpers/montarApp';

describe('lista de etapas da estação', () => {
  it('mostra a OP, o produto, a quantidade e o estado de cada etapa', async () => {
    const app = await montarApp();
    app.doServidor.etapas = [
      etapa({ etapaId: 1, numeroOp: 'OP-2026-00001', status: 'EmAndamento' }),
      etapa({ etapaId: 2, numeroOp: 'OP-2026-00002', sequencia: 2, liberada: false }),
    ];

    await render(<TelaDeEtapas />, { wrapper: app.Envolver });

    expect(await screen.findByText('OP-2026-00001')).toBeOnTheScreen();
    expect(screen.getByText('OP-2026-00002')).toBeOnTheScreen();
    expect(screen.getAllByText(/Cadeira Ergonômica Presidente · 40 un\./)).toHaveLength(2);
    expect(screen.getByText(/▶ Em andamento/)).toBeOnTheScreen();
    expect(screen.getByText(/🔒 Aguardando etapa anterior/)).toBeOnTheScreen();
  });

  it('cada cartão descreve a etapa por inteiro para o leitor de tela', async () => {
    const app = await montarApp();
    app.doServidor.etapas = [etapa({ numeroOp: 'OP-2026-00058' })];

    await render(<TelaDeEtapas />, { wrapper: app.Envolver });

    const cartao = await screen.findByRole('button', {
      name: /OP-2026-00058, Cadeira Ergonômica Presidente, 40 unidades\. ⏳ Pronta para iniciar/,
    });
    expect(cartao.props.accessibilityHint).toBe('Abre os detalhes da etapa');
  });

  it('sem etapas mostra o estado vazio com a próxima ação', async () => {
    const app = await montarApp();

    await render(<TelaDeEtapas />, { wrapper: app.Envolver });

    expect(await screen.findByText('Nenhuma etapa na fila')).toBeOnTheScreen();
    expect(screen.getByText(/Puxe a tela para atualizar/)).toBeOnTheScreen();
  });

  it('sem conexão mostra o que estava guardado e avisa que são dados do aparelho', async () => {
    const app = await montarApp();
    app.doServidor.etapas = [etapa({ numeroOp: 'OP-2026-00058' })];
    await app.servicos.etapas.carregarFila(1); // o aparelho aprende a etapa com rede
    await app.definirOnline(false);

    await render(<TelaDeEtapas />, { wrapper: app.Envolver });

    expect(await screen.findByText('OP-2026-00058')).toBeOnTheScreen();
    expect(screen.getByText(/Sem conexão\. Mostrando os dados do aparelho/)).toBeOnTheScreen();
  });

  it('oferece ler o QR code, estoque, trocar estação e configurações', async () => {
    const app = await montarApp();

    await render(<TelaDeEtapas />, { wrapper: app.Envolver });

    await screen.findByText('Nenhuma etapa na fila');
    for (const nome of ['Ler QR code', 'Estoque', 'Trocar estação', 'Configurações']) {
      expect(screen.getByRole('button', { name: nome })).toBeOnTheScreen();
    }
  });

  it('o inspetor não vê o botão Estoque, que a API nega a ele', async () => {
    const app = await montarApp('Inspetor', 5);

    await render(<TelaDeEtapas />, { wrapper: app.Envolver });

    await screen.findByText('Nenhuma etapa na fila');
    expect(screen.queryByRole('button', { name: 'Estoque' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Ler QR code' })).toBeOnTheScreen();
  });

  it('sem estação escolhida manda o operador escolher uma', async () => {
    const app = await montarApp('Operador', null);

    await render(<TelaDeEtapas />, { wrapper: app.Envolver });

    expect((await screen.findByTestId('redirect')).props.children).toBe('/estacao');
  });
});
