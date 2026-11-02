import { render, screen, userEvent, waitFor } from '@testing-library/react-native';
import { Alert, type AlertButton } from 'react-native';

import { TelaDaFila } from '@/features/sync/TelaDaFila';
import type { NovoApontamento } from '@/features/sync/tipos';
import { montarApp, type AppDeTeste } from '../helpers/montarApp';

function item(id: string, tipo: 'Inicio' | 'Fim' = 'Inicio'): NovoApontamento {
  return {
    clientRequestId: id,
    tipo,
    etapaId: 341,
    numeroOp: 'OP-2026-00058',
    estacaoNome: 'Corte',
    dataHoraCliente: '2026-11-02T12:00:00.000Z',
  };
}

async function abrir(): Promise<AppDeTeste> {
  const app = await montarApp();
  // A fila é montada antes de a tela abrir, como no aparelho depois de um dia de trabalho
  await app.fila.enfileirar(item('rejeitado'));
  await app.fila.marcarRejeitado('rejeitado', 'RN04', 'Madeira maciça: saldo insuficiente.');
  await app.fila.enfileirar(item('pendente', 'Fim'));
  await app.definirOnline(false);
  return app;
}

describe('fila de sincronização', () => {
  it('lista o pendente e o rejeitado com ícone, texto e o motivo da recusa', async () => {
    const app = await abrir();
    await render(<TelaDaFila />, { wrapper: app.Envolver });

    expect(await screen.findByText('⏳ Aguardando envio')).toBeOnTheScreen();
    expect(screen.getByText('✖ Rejeitado')).toBeOnTheScreen();
    expect(screen.getByText('Madeira maciça: saldo insuficiente.')).toBeOnTheScreen();
    expect(screen.getByText(/Sem conexão\. Os apontamentos ficam guardados/)).toBeOnTheScreen();
  });

  it('só o rejeitado oferece Tentar de novo e Descartar', async () => {
    const app = await abrir();
    await render(<TelaDaFila />, { wrapper: app.Envolver });

    await screen.findByText('✖ Rejeitado');

    expect(screen.getAllByRole('button', { name: 'Tentar de novo' })).toHaveLength(1);
    expect(screen.getAllByRole('button', { name: 'Descartar' })).toHaveLength(1);
  });

  it('sem conexão o botão Enviar agora fica desabilitado', async () => {
    const app = await abrir();
    await render(<TelaDaFila />, { wrapper: app.Envolver });

    expect(await screen.findByRole('button', { name: 'Enviar agora' })).toBeDisabled();
  });

  it('Tentar de novo reenvia o mesmo item e a fila esvazia quando o servidor aceita', async () => {
    const app = await abrir();
    await render(<TelaDaFila />, { wrapper: app.Envolver });
    await screen.findByText('✖ Rejeitado');
    await app.definirOnline(true); // a conexão volta (e a fila pendente sai sozinha)

    await userEvent.press(screen.getByRole('button', { name: 'Tentar de novo' }));

    await waitFor(() => expect(app.servidor.efeitos.get('rejeitado')).toBe(1));
    expect(await screen.findByText('Fila vazia')).toBeOnTheScreen();
  });

  it('Descartar pede confirmação antes de apagar o apontamento', async () => {
    const app = await abrir();
    const alerta = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
    await render(<TelaDaFila />, { wrapper: app.Envolver });
    await screen.findByText('✖ Rejeitado');

    await userEvent.press(screen.getByRole('button', { name: 'Descartar' }));

    expect(alerta).toHaveBeenCalledTimes(1);
    expect((await app.fila.listar()).map((i) => i.clientRequestId)).toContain('rejeitado'); // ainda não apagou
    const botoes = alerta.mock.calls[0][2] as AlertButton[];
    botoes.find((b) => b.text === 'Descartar')?.onPress?.();
    await waitFor(async () => expect((await app.fila.listar()).map((i) => i.clientRequestId)).not.toContain('rejeitado'));
    alerta.mockRestore();
  });

  it('fila vazia diz que está tudo enviado', async () => {
    const app = await montarApp();
    await render(<TelaDaFila />, { wrapper: app.Envolver });

    expect(await screen.findByText('Fila vazia')).toBeOnTheScreen();
  });
});
