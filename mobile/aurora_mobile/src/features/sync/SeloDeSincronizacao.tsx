import { router } from 'expo-router';
import { Pressable, StyleSheet, View } from 'react-native';

import { useSync } from '@/features/sync/SyncProvider';
import { Texto } from '@/shared/componentes';
import { useTema } from '@/shared/tema';

/** Frase do estado para o leitor de tela (e para o texto da faixa). */
export function descreverSincronizacao(online: boolean, pendentes: number, rejeitados: number, clientes = 0): string {
  const rede = online ? 'Conectado' : 'Sem conexão';
  const partes = [rede];
  if (pendentes > 0) partes.push(`${pendentes} ${pendentes === 1 ? 'apontamento a enviar' : 'apontamentos a enviar'}`);
  if (clientes > 0) partes.push(`${clientes} ${clientes === 1 ? 'cliente a enviar' : 'clientes a enviar'}`);
  if (rejeitados > 0) partes.push(`${rejeitados} ${rejeitados === 1 ? 'rejeitado' : 'rejeitados'}`);
  return partes.join(' · ');
}

/** Indicador permanente de conexão e de itens pendentes (docs/08 §8). Tocar abre a fila de sincronização. */
export function SeloDeSincronizacao() {
  const { online, sincronizando, contagem, clientes } = useSync();
  const { cores } = useTema();
  const pendentes = contagem.pendentes + contagem.enviando;
  const aEnviarDeClientes = clientes.pendentes + clientes.enviando;
  const rejeitados = contagem.rejeitados + clientes.rejeitados;
  const texto = descreverSincronizacao(online, pendentes, rejeitados, aEnviarDeClientes);
  const problema = !online || rejeitados > 0;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${texto}${sincronizando ? '. Enviando agora' : ''}`}
      accessibilityHint="Abre a fila de sincronização"
      onPress={() => router.push('/fila')}
      style={[
        estilos.faixa,
        { backgroundColor: problema ? cores.fundoAviso : cores.fundoSucesso, borderColor: problema ? cores.aviso : cores.sucesso },
      ]}
    >
      <View style={estilos.linha}>
        <Texto negrito tamanho="pequeno" cor={problema ? cores.aviso : cores.sucesso}>
          {online ? '● Online' : '○ Offline'}
        </Texto>
        <Texto tamanho="pequeno" cor={problema ? cores.aviso : cores.sucesso}>
          {sincronizando ? 'Enviando…' : texto.replace(/^(Conectado|Sem conexão)( · )?/, '')}
        </Texto>
      </View>
    </Pressable>
  );
}

const estilos = StyleSheet.create({
  faixa: { minHeight: 48, paddingHorizontal: 16, paddingVertical: 10, borderBottomWidth: 2, justifyContent: 'center' },
  linha: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
});
