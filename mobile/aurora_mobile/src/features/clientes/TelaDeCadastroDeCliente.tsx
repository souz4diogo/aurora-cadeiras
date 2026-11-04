import { router } from 'expo-router';
import { useState } from 'react';

import { useServicos } from '@/core/ServicosProvider';
import { interpretarErro } from '@/core/http/problem';
import { validarCliente, type ErrosDoCliente } from '@/features/clientes/clientesService';
import type { DadosDoCliente } from '@/features/clientes/tipos';
import { useSync } from '@/features/sync/SyncProvider';
import { Aviso, Botao, CampoDeTexto, Tela, Texto } from '@/shared/componentes';
import { vibrarAlerta, vibrarSucesso } from '@/shared/vibracao';

const VAZIO: DadosDoCliente = { nome: '', cpfCnpj: '', email: '', telefone: '', cidade: '', uf: '' };

/**
 * Cadastro de cliente (RF06, A60): a tela de cadastro do Vendedor. O cliente é gravado primeiro no SQLite do
 * aparelho e enviado ao servidor em seguida, ou quando a conexão voltar (docs/08 §1.1).
 */
export function TelaDeCadastroDeCliente() {
  const { clientes } = useServicos();
  const { online, sincronizarAgora, atualizarContagem } = useSync();
  const [dados, setDados] = useState<DadosDoCliente>(VAZIO);
  const [erros, setErros] = useState<ErrosDoCliente>({});
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [cadastrado, setCadastrado] = useState<{ nome: string; enviado: boolean } | null>(null);

  const mudar = (campo: keyof DadosDoCliente) => (texto: string) => setDados((atual) => ({ ...atual, [campo]: texto }));

  async function salvar(): Promise<void> {
    setErro(null);
    const problemas = validarCliente(dados);
    setErros(problemas);
    if (Object.keys(problemas).length > 0) return;

    setSalvando(true);
    try {
      const gravado = await clientes.cadastrar(dados);
      vibrarSucesso();
      await atualizarContagem();
      // Tenta enviar já; sem rede, o cliente fica guardado e vai quando a conexão voltar
      await sincronizarAgora().catch(() => undefined);
      const restante = (await clientes.listar()).some((c) => c.id === gravado.id);
      setCadastrado({ nome: gravado.nome, enviado: !restante });
    } catch (falha) {
      vibrarAlerta();
      setErro(interpretarErro(falha).message);
    } finally {
      setSalvando(false);
    }
  }

  function cadastrarOutro(): void {
    setDados(VAZIO);
    setErros({});
    setCadastrado(null);
  }

  if (cadastrado) {
    return (
      <Tela>
        <Aviso
          tipo={cadastrado.enviado ? 'sucesso' : 'aviso'}
          texto={
            cadastrado.enviado
              ? `${cadastrado.nome} foi cadastrado.`
              : `${cadastrado.nome} está salvo neste aparelho e será enviado quando houver conexão. Acompanhe na fila de sincronização.`
          }
        />
        <Botao titulo="Cadastrar outro cliente" aoPressionar={cadastrarOutro} />
        {!cadastrado.enviado ? (
          <Botao titulo="Ver fila de sincronização" variante="secundario" aoPressionar={() => router.push('/fila')} />
        ) : null}
        <Botao titulo="Voltar" variante="secundario" aoPressionar={() => router.back()} />
      </Tela>
    );
  }

  return (
    <Tela>
      {!online ? (
        <Aviso tipo="info" texto="Sem conexão. Você pode cadastrar: o cliente fica salvo no aparelho e é enviado quando a internet voltar." />
      ) : null}
      {erro ? <Aviso tipo="erro" texto={erro} /> : null}

      <CampoDeTexto rotulo="Nome" value={dados.nome} onChangeText={mudar('nome')} maxLength={150} autoCapitalize="words" erro={erros.nome} />
      <CampoDeTexto
        rotulo="CPF ou CNPJ"
        value={dados.cpfCnpj}
        onChangeText={mudar('cpfCnpj')}
        keyboardType="number-pad"
        maxLength={18}
        placeholder="Somente números"
        erro={erros.cpfCnpj}
      />
      <CampoDeTexto
        rotulo="E-mail (opcional)"
        value={dados.email}
        onChangeText={mudar('email')}
        keyboardType="email-address"
        autoCapitalize="none"
        autoCorrect={false}
        maxLength={150}
        erro={erros.email}
      />
      <CampoDeTexto
        rotulo="Telefone (opcional)"
        value={dados.telefone}
        onChangeText={mudar('telefone')}
        keyboardType="phone-pad"
        maxLength={20}
        erro={erros.telefone}
      />
      <CampoDeTexto rotulo="Cidade (opcional)" value={dados.cidade} onChangeText={mudar('cidade')} maxLength={80} erro={erros.cidade} />
      <CampoDeTexto
        rotulo="UF (opcional)"
        value={dados.uf}
        onChangeText={mudar('uf')}
        autoCapitalize="characters"
        autoCorrect={false}
        maxLength={2}
        placeholder="Ex.: SP"
        erro={erros.uf}
      />

      <Texto secundario tamanho="pequeno">Os dados ficam salvos no aparelho até o servidor confirmar o cadastro.</Texto>
      <Botao titulo="Cadastrar cliente" carregando={salvando} aoPressionar={() => void salvar()} />
    </Tela>
  );
}
