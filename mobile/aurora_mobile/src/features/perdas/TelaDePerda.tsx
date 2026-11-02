import { router } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import { useServicos } from '@/core/ServicosProvider';
import { interpretarErro } from '@/core/http/problem';
import type { TipoDePerda } from '@/features/etapas/etapasApi';
import { validarPerda } from '@/features/perdas/perdasService';
import { useSync } from '@/features/sync/SyncProvider';
import { Aviso, Botao, CampoDeTexto, Tela, Texto } from '@/shared/componentes';
import { vibrarAlerta, vibrarSucesso } from '@/shared/vibracao';

/**
 * Registrar perda (RF12): a tela de cadastro do operador. Refugo ou retrabalho, quantidade e motivo.
 * Exige conexão porque o servidor confere se a etapa está em andamento (docs/08 §5).
 */
export function TelaDePerda({ etapaId }: { etapaId: number }) {
  const { perdas } = useServicos();
  const { online } = useSync();
  const [tipo, setTipo] = useState<TipoDePerda>('Refugo');
  const [quantidade, setQuantidade] = useState('');
  const [motivo, setMotivo] = useState('');
  const [erroDoFormulario, setErroDoFormulario] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [registrada, setRegistrada] = useState(false);
  const [enviando, setEnviando] = useState(false);

  async function registrar(): Promise<void> {
    setErro(null);
    const problema = validarPerda(quantidade, motivo);
    setErroDoFormulario(problema);
    if (problema) return;

    setEnviando(true);
    try {
      await perdas.registrar(etapaId, { tipo, quantidade: Number(quantidade), motivo });
      vibrarSucesso();
      setRegistrada(true);
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
        <Aviso tipo="sucesso" texto={`${tipo} de ${quantidade} un. registrado.`} />
        <Botao titulo="Voltar à etapa" aoPressionar={() => router.back()} />
      </Tela>
    );
  }

  return (
    <Tela>
      {!online ? <Aviso tipo="aviso" texto="Sem conexão. Registrar perda exige internet; tente quando voltar." /> : null}
      {erro ? <Aviso tipo="erro" texto={erro} /> : null}

      <Texto negrito>Tipo da perda</Texto>
      <View style={{ flexDirection: 'row', gap: 12 }}>
        {(['Refugo', 'Retrabalho'] as const).map((opcao) => (
          <Botao
            key={opcao}
            titulo={tipo === opcao ? `✔ ${opcao}` : opcao}
            variante={tipo === opcao ? 'primario' : 'secundario'}
            estilo={{ flex: 1 }}
            dica={opcao === 'Refugo' ? 'Peças descartadas' : 'Peças que voltam para refazer'}
            aoPressionar={() => setTipo(opcao)}
          />
        ))}
      </View>

      <CampoDeTexto
        rotulo="Quantidade"
        value={quantidade}
        onChangeText={setQuantidade}
        keyboardType="number-pad"
        placeholder="Ex.: 2"
      />
      <CampoDeTexto
        rotulo="Motivo"
        value={motivo}
        onChangeText={setMotivo}
        multiline
        maxLength={200}
        placeholder="O que aconteceu?"
        erro={erroDoFormulario}
      />

      <Botao titulo="Registrar perda" carregando={enviando} desabilitado={!online} aoPressionar={() => void registrar()} />
    </Tela>
  );
}
