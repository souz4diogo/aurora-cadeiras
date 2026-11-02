import { fireEvent, render, screen, userEvent, waitFor } from '@testing-library/react-native';

import { ApiError } from '@/core/http/problem';
import { TelaDeLogin } from '@/features/auth/TelaDeLogin';
import { montarApp, type AppDeTeste } from '../helpers/montarApp';

let app: AppDeTeste;

async function abrirLogin() {
  app = await montarApp();
  await render(<TelaDeLogin />, { wrapper: app.Envolver });
}

describe('tela de login', () => {
  it('mostra os campos e o botão com nomes que o leitor de tela entende', async () => {
    await abrirLogin();

    expect(screen.getByLabelText('E-mail')).toBeOnTheScreen();
    expect(screen.getByLabelText('Senha')).toBeOnTheScreen();
    expect(screen.getByLabelText('Manter conectado')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeOnTheScreen();
  });

  it('campos vazios mostram o que falta e não chamam o servidor', async () => {
    await abrirLogin();

    await userEvent.press(screen.getByRole('button', { name: 'Entrar' }));

    expect(screen.getByText(/Informe o e-mail/)).toBeOnTheScreen();
    expect(screen.getByText(/Informe a senha/)).toBeOnTheScreen();
    expect(app.auth.entrar).not.toHaveBeenCalled();
  });

  it('envia e-mail, senha e a escolha de manter conectado', async () => {
    await abrirLogin();

    await userEvent.type(screen.getByLabelText('E-mail'), 'carlos@aurora.local');
    await userEvent.type(screen.getByLabelText('Senha'), 'senha-segura-1');
    await userEvent.press(screen.getByRole('button', { name: 'Entrar' }));

    await waitFor(() => expect(app.auth.entrar).toHaveBeenCalledWith('carlos@aurora.local', 'senha-segura-1', true));
  });

  it('respeita "manter conectado" desligado', async () => {
    await abrirLogin();
    await userEvent.type(screen.getByLabelText('E-mail'), 'carlos@aurora.local');
    await userEvent.type(screen.getByLabelText('Senha'), 'senha-segura-1');

    await fireEvent(screen.getByLabelText('Manter conectado'), 'valueChange', false);
    await userEvent.press(screen.getByRole('button', { name: 'Entrar' }));

    await waitFor(() => expect(app.auth.entrar).toHaveBeenCalledWith('carlos@aurora.local', 'senha-segura-1', false));
  });

  it('senha errada mostra a mensagem em português e limpa a senha digitada', async () => {
    await abrirLogin();
    app.auth.entrar.mockRejectedValueOnce(new ApiError('CREDENCIAIS_INVALIDAS', 'E-mail ou senha inválidos.', 401));
    await userEvent.type(screen.getByLabelText('E-mail'), 'carlos@aurora.local');
    await userEvent.type(screen.getByLabelText('Senha'), 'errada');

    await userEvent.press(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByText(/E-mail ou senha inválidos/)).toBeOnTheScreen();
    expect(screen.getByLabelText('Senha').props.value).toBe('');
    expect(screen.getByLabelText('E-mail').props.value).toBe('carlos@aurora.local');
  });

  it('o campo de senha esconde o que é digitado', async () => {
    await abrirLogin();

    expect(screen.getByLabelText('Senha').props.secureTextEntry).toBe(true);
  });
});
