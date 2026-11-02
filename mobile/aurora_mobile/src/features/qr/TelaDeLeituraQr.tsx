import { CameraView, useCameraPermissions } from 'expo-camera';
import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { useLeitorDeQr } from '@/features/qr/useLeitorDeQr';
import { Aviso, Botao, CampoDeTexto, Tela, Texto } from '@/shared/componentes';

/** Ler QR code (RF11): câmera para a etiqueta, com o código digitado como alternativa (TalkBack, luz ruim). */
export function TelaDeLeituraQr() {
  const [permissao, pedirPermissao] = useCameraPermissions();
  const leitor = useLeitorDeQr();
  const [codigo, setCodigo] = useState('');
  // Depois de um erro a câmera pausa até o usuário tocar em "Ler de novo", para não repetir a mesma falha
  const pausada = leitor.processando || leitor.erro !== null;

  return (
    <Tela>
      {leitor.erro ? <Aviso tipo="erro" texto={leitor.erro} /> : null}

      {permissao?.granted ? (
        <View style={estilos.moldura} accessible accessibilityLabel="Câmera. Aponte para o QR code da etiqueta.">
          <CameraView
            style={StyleSheet.absoluteFill}
            facing="back"
            barcodeScannerSettings={{ barcodeTypes: ['qr'] }}
            onBarcodeScanned={pausada ? undefined : ({ data }) => void leitor.ler(data)}
          />
        </View>
      ) : (
        <View style={estilos.sem}>
          <Texto>
            {permissao && !permissao.canAskAgain
              ? 'A permissão da câmera foi negada. Libere nas configurações do aparelho ou digite o código abaixo.'
              : 'Para ler a etiqueta, o app precisa usar a câmera.'}
          </Texto>
          {permissao?.canAskAgain !== false ? (
            <Botao titulo="Permitir câmera" aoPressionar={() => void pedirPermissao()} />
          ) : null}
        </View>
      )}

      {leitor.erro ? <Botao titulo="Ler de novo" aoPressionar={leitor.limparErro} /> : null}
      {leitor.processando ? <Texto secundario>Procurando a ordem de produção…</Texto> : null}

      <CampoDeTexto
        rotulo="Ou digite o código da etiqueta"
        value={codigo}
        onChangeText={setCodigo}
        autoCapitalize="none"
        autoCorrect={false}
        placeholder="b6d1c0f2-1d1e-4a7e-9d3b-3f7e5c2a9a10"
      />
      <Botao
        titulo="Abrir ordem"
        variante="secundario"
        desabilitado={codigo.trim().length === 0 || leitor.processando}
        aoPressionar={() => void leitor.ler(codigo)}
      />
    </Tela>
  );
}

const estilos = StyleSheet.create({
  moldura: { height: 320, borderRadius: 16, overflow: 'hidden', backgroundColor: '#000' },
  sem: { gap: 12 },
});
