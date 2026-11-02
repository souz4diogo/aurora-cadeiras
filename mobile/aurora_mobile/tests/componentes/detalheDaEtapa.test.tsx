import { render, screen, userEvent, waitFor } from '@testing-library/react-native';

import type { Perfil } from '@/core/auth/tokenStore';
import { TelaDeDetalheDaEtapa } from '@/features/etapas/TelaDeDetalheDaEtapa';
import type { EtapaDaEstacao } from '@/features/etapas/tipos';
import { etapa } from '../helpers/dados';
import { montarApp, type AppDeTeste } from '../helpers/montarApp';

async function abrir(perfil: Perfil, etapas: EtapaDaEstacao[], antes?: (app: AppDeTeste) => Promise<void> | void) {
  const app = await montarApp(perfil);
  app.doServidor.etapas = etapas;
  await app.servicos.etapas.carregarFila(etapas[0].estacaoId); // o aparelho já conhece a etapa
  await antes?.(app);
  await render(<TelaDeDetalheDaEtapa etapaId={etapas[0].etapaId} />, { wrapper: app.Envolver });
  return app;
}

describe('detalhe da etapa do operador', () => {
  it('mostra produto, quantidade, pedido e os materiais que a etapa consome', async () => {
    await abrir('Operador', [etapa({ quantidade: 40 })], (app) => {
      app.doServidor.materiais = [
        { materiaPrimaId: 9, materiaPrimaCodigo: 'MAD-01', materiaPrimaNome: 'Madeira maciça', unidade: 'm3', estacaoId: 1, quantidade: 0.45 },
        { materiaPrimaId: 8, materiaPrimaCodigo: 'TEC-01', materiaPrimaNome: 'Tecido', unidade: 'm', estacaoId: 3, quantidade: 2 },
      ];
    });

    expect(await screen.findByText('OP-2026-00058')).toBeOnTheScreen();
    expect(screen.getByText('Cadeira Ergonômica Presidente')).toBeOnTheScreen();
    expect(screen.getByText(/Pedido PED-2026-00031/)).toBeOnTheScreen();
    expect(await screen.findByText(/Madeira maciça: 18 m3/)).toBeOnTheScreen();
    expect(screen.queryByText(/Tecido/)).toBeNull(); // consumido em outra estação
  });

  it('iniciar com rede é confirmado pelo servidor na hora e vira Finalizar', async () => {
    const app = await abrir('Operador', [etapa()]);

    await userEvent.press(await screen.findByRole('button', { name: 'Iniciar etapa' }));

    expect(await screen.findByText(/Início da etapa registrado e confirmado pelo servidor/)).toBeOnTheScreen();
    expect(app.servidor.totalDeEfeitos).toBe(1);
    expect(await app.fila.listar()).toEqual([]);
    expect(await screen.findByRole('button', { name: 'Finalizar etapa' })).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Iniciar etapa' })).toBeNull();
  });

  it('iniciar sem rede guarda no aparelho, avisa e já mostra a etapa em andamento', async () => {
    const app = await abrir('Operador', [etapa()]);
    await app.definirOnline(false);

    await userEvent.press(await screen.findByRole('button', { name: 'Iniciar etapa' }));

    expect(await screen.findByText(/Início salvo no aparelho\. Será enviado quando houver conexão/)).toBeOnTheScreen();
    expect(app.servidor.totalDeEfeitos).toBe(0);
    expect(await app.fila.contar()).toMatchObject({ pendentes: 1 });
    expect(await screen.findByRole('button', { name: 'Finalizar etapa' })).toBeOnTheScreen();
    expect(screen.getByText(/a enviar/)).toBeOnTheScreen();
  });

  it('a conexão volta e o que ficou guardado vai sozinho ao servidor', async () => {
    const app = await abrir('Operador', [etapa()]);
    await app.definirOnline(false);
    await userEvent.press(await screen.findByRole('button', { name: 'Iniciar etapa' }));
    await screen.findByText(/salvo no aparelho/);

    await app.definirOnline(true);

    await waitFor(() => expect(app.servidor.totalDeEfeitos).toBe(1));
    await waitFor(async () => expect(await app.fila.contar()).toEqual({ pendentes: 0, enviando: 0, rejeitados: 0 }));
  });

  it('servidor recusa por falta de saldo: mostra o motivo e a etapa continua pendente', async () => {
    const app = await abrir('Operador', [etapa()], (a) => {
      a.servidor.regra = () => ({ code: 'RN04', mensagem: 'Madeira maciça: necessário 0,450 m3, disponível 0,200 m3.' });
    });

    await userEvent.press(await screen.findByRole('button', { name: 'Iniciar etapa' }));

    expect(await screen.findByText(/Início recusado pelo servidor: Madeira maciça: necessário 0,450 m3/)).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Iniciar etapa' })).toBeOnTheScreen();
    expect((await app.fila.listar())[0]).toMatchObject({ estado: 'Rejeitado', codigo: 'RN04' });
  });

  it('etapa travada pela anterior não oferece Iniciar e explica por quê', async () => {
    await abrir('Operador', [etapa({ liberada: false, sequencia: 2 })]);

    expect(await screen.findByText(/Aguardando a etapa anterior da ordem ser concluída/)).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Iniciar etapa' })).toBeNull();
  });

  it('etapa em andamento oferece Finalizar e Registrar perda', async () => {
    await abrir('Operador', [etapa({ status: 'EmAndamento' })]);

    expect(await screen.findByRole('button', { name: 'Finalizar etapa' })).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Registrar perda' })).toBeOnTheScreen();
  });

  it('registrar perda exige conexão: sem rede o botão fica desabilitado e a tela avisa', async () => {
    const app = await abrir('Operador', [etapa({ status: 'EmAndamento' })]);
    await app.definirOnline(false);

    const botao = await screen.findByRole('button', { name: 'Registrar perda' });

    expect(botao).toBeDisabled();
    expect(screen.getByText(/Registrar perda e inspeção exige internet/)).toBeOnTheScreen();
  });

  it('o inspetor inicia a Qualidade e a conclui pela inspeção, nunca por Finalizar', async () => {
    const qualidade = etapa({ estacaoId: 5, estacaoNome: 'Qualidade', sequencia: 3, status: 'EmAndamento' });
    await abrir('Inspetor', [qualidade]);

    expect(await screen.findByRole('button', { name: 'Registrar inspeção' })).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Finalizar etapa' })).toBeNull();
  });

  it('etapa que o aparelho não conhece mostra mensagem e caminho de volta', async () => {
    const app = await montarApp();
    await render(<TelaDeDetalheDaEtapa etapaId={999} />, { wrapper: app.Envolver });

    expect(await screen.findByText('Etapa não encontrada')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Voltar' })).toBeOnTheScreen();
  });

  it('os botões de ação têm descrição para o leitor de tela', async () => {
    await abrir('Operador', [etapa()]);

    const iniciar = await screen.findByRole('button', { name: 'Iniciar etapa' });

    expect(iniciar.props.accessibilityHint).toBe('Registra o início e consome o material da etapa');
  });
});
