import { render, screen, userEvent, waitFor } from '@testing-library/react-native';
import { router } from 'expo-router';

import { TelaDeCadastroDeCliente } from '@/features/clientes/TelaDeCadastroDeCliente';
import { TelaDePedidos } from '@/features/pedidos/TelaDePedidos';
import { TelaDaFila } from '@/features/sync/TelaDaFila';
import { montarApp } from '../helpers/montarApp';

async function preencher(nome = 'Móveis Lima', documento = '12345678000195'): Promise<void> {
  const usuario = userEvent.setup();
  await usuario.type(screen.getByLabelText('Nome'), nome);
  await usuario.type(screen.getByLabelText('CPF ou CNPJ'), documento);
  await usuario.press(screen.getByRole('button', { name: 'Cadastrar cliente' }));
}

describe('cadastro de cliente pelo Vendedor (A60)', () => {
  it('online: grava no aparelho, envia na hora e confirma', async () => {
    const app = await montarApp('Vendedor');
    await render(<TelaDeCadastroDeCliente />, { wrapper: app.Envolver });

    await preencher();

    expect(await screen.findByText(/Móveis Lima foi cadastrado\./)).toBeOnTheScreen();
    expect(app.clientesApi.cadastrados).toEqual([{ nome: 'Móveis Lima', cpfCnpj: '12345678000195' }]);
    expect(await app.servicos.clientes.listar()).toEqual([]);
  });

  it('offline: o cliente fica salvo no SQLite, o aviso explica, e vai quando a conexão volta', async () => {
    const app = await montarApp('Vendedor');
    await app.definirOnline(false);
    await render(<TelaDeCadastroDeCliente />, { wrapper: app.Envolver });
    expect(screen.getByText(/Sem conexão\. Você pode cadastrar/)).toBeOnTheScreen();

    await preencher();

    expect(await screen.findByText(/está salvo neste aparelho e será enviado quando houver conexão/)).toBeOnTheScreen();
    expect(await app.servicos.clientes.listar()).toHaveLength(1);
    expect(app.clientesApi.cadastrados).toEqual([]);

    await app.definirOnline(true);

    await waitFor(() => expect(app.clientesApi.cadastrados).toHaveLength(1));
    await waitFor(async () => expect(await app.servicos.clientes.listar()).toEqual([]));
  });

  it('mostra o erro do campo e não grava nada quando o formulário é inválido', async () => {
    const app = await montarApp('Vendedor');
    await render(<TelaDeCadastroDeCliente />, { wrapper: app.Envolver });

    await preencher('Ana', '123');

    expect(await screen.findByText(/Informe um CPF \(11 dígitos\) ou CNPJ/)).toBeOnTheScreen();
    expect(await app.servicos.clientes.listar()).toEqual([]);
  });

  it('documento já presente na fila do aparelho é recusado com a mensagem', async () => {
    const app = await montarApp('Vendedor');
    await app.definirOnline(false);
    await app.servicos.clientes.cadastrar({ nome: 'A', cpfCnpj: '12345678000195', email: '', telefone: '', cidade: '', uf: '' });
    await render(<TelaDeCadastroDeCliente />, { wrapper: app.Envolver });

    await preencher();

    expect(await screen.findByText(/já está na fila de envio deste aparelho/)).toBeOnTheScreen();
  });

  it('o cliente guardado aparece na fila de sincronização e o rejeitado oferece Tentar de novo', async () => {
    const app = await montarApp('Vendedor');
    await app.definirOnline(false);
    await app.servicos.clientes.cadastrar({ nome: 'Ana Souza', cpfCnpj: '12345678901', email: '', telefone: '', cidade: '', uf: '' });
    await render(<TelaDaFila />, { wrapper: app.Envolver });

    expect(await screen.findByText('Clientes cadastrados neste aparelho')).toBeOnTheScreen();
    expect(screen.getByText('Ana Souza')).toBeOnTheScreen();
    expect(screen.getByText('⏳ Aguardando envio')).toBeOnTheScreen();
    expect(screen.queryByText('Fila vazia')).toBeNull();
  });

  it('só o Vendedor vê o botão Cadastrar cliente em Meus pedidos', async () => {
    const vendedor = await montarApp('Vendedor');
    vendedor.http.get.mockResolvedValue({ data: { items: [] } });
    await render(<TelaDePedidos />, { wrapper: vendedor.Envolver });
    const usuario = userEvent.setup();

    await usuario.press(await screen.findByRole('button', { name: 'Cadastrar cliente' }));
    expect(router.push).toHaveBeenCalledWith('/cliente-novo');
  });

  it('o Cliente não vê o botão', async () => {
    const cliente = await montarApp('Cliente');
    cliente.http.get.mockResolvedValue({ data: { items: [] } });
    await render(<TelaDePedidos />, { wrapper: cliente.Envolver });

    expect(await screen.findByText('Nenhum pedido')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Cadastrar cliente' })).toBeNull();
  });
});
