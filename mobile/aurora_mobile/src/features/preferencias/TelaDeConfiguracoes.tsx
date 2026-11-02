import Constants from 'expo-constants';
import { Alert, Switch, View } from 'react-native';

import { useAuth, useUsuario } from '@/features/auth/AuthProvider';
import { useSairComSeguranca } from '@/features/auth/useSair';
import { ESCALAS_DE_FONTE } from '@/features/preferencias/preferenciasRepository';
import { Botao, Cartao, Tela, Texto } from '@/shared/componentes';
import { useTema } from '@/shared/tema';

const ROTULOS_DA_FONTE: Record<number, string> = { 1: 'Normal', 1.15: 'Grande', 1.3: 'Muito grande' };

function confirmarPerda(naoEnviados: number): Promise<boolean> {
  return new Promise((resolver) => {
    Alert.alert(
      'Há apontamentos não enviados',
      `${naoEnviados} apontamento(s) ainda não chegaram ao servidor e serão perdidos se você sair agora. Conecte-se à internet e tente de novo, ou saia mesmo assim.`,
      [
        { text: 'Ficar', style: 'cancel', onPress: () => resolver(false) },
        { text: 'Sair mesmo assim', style: 'destructive', onPress: () => resolver(true) },
      ],
      { cancelable: true, onDismiss: () => resolver(false) },
    );
  });
}

/** Configurações (docs/08 §2): sair, tamanho da fonte, alto contraste e versão do app. */
export function TelaDeConfiguracoes() {
  const usuario = useUsuario();
  const { estado } = useAuth();
  const sairComSeguranca = useSairComSeguranca();
  const { cores, escala, altoContraste, definirEscala, definirAltoContraste } = useTema();
  const versao = Constants.expoConfig?.version ?? '—';

  return (
    <Tela>
      <Cartao>
        <Texto negrito tamanho="grande">{usuario.nome}</Texto>
        <Texto secundario>{`Perfil: ${usuario.perfil}`}</Texto>
      </Cartao>

      <Cartao>
        <Texto negrito accessibilityRole="header">Tamanho do texto</Texto>
        <View style={{ gap: 10 }}>
          {ESCALAS_DE_FONTE.map((opcao) => (
            <Botao
              key={opcao}
              titulo={escala === opcao ? `✔ ${ROTULOS_DA_FONTE[opcao]}` : ROTULOS_DA_FONTE[opcao]}
              variante={escala === opcao ? 'primario' : 'secundario'}
              aoPressionar={() => definirEscala(opcao)}
            />
          ))}
        </View>
      </Cartao>

      <Cartao>
        <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12, minHeight: 56 }}>
          <Texto style={{ flex: 1 }}>Alto contraste</Texto>
          <Switch
            value={altoContraste}
            onValueChange={definirAltoContraste}
            accessibilityLabel="Alto contraste"
            accessibilityHint="Troca as cores por preto, branco e amarelo"
            trackColor={{ true: cores.primaria, false: cores.borda }}
            thumbColor={cores.superficie}
          />
        </View>
      </Cartao>

      <Botao
        titulo="Sair"
        variante="perigo"
        desabilitado={estado.status !== 'logado'}
        dica="Encerra a sessão e apaga os dados guardados no aparelho"
        aoPressionar={() => void sairComSeguranca(confirmarPerda)}
      />

      <Texto secundario tamanho="pequeno">{`Aurora Cadeiras · versão ${versao}`}</Texto>
    </Tela>
  );
}
