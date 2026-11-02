import { StyleSheet, Switch, View } from 'react-native';

import { useLogin } from '@/features/auth/useLogin';
import { Aviso, Botao, CampoDeTexto, Tela, Texto } from '@/shared/componentes';
import { useTema } from '@/shared/tema';

/** Tela de login (RF01): e-mail, senha, "manter conectado" e mensagem de erro em português. */
export function TelaDeLogin() {
  const vm = useLogin();
  const { cores } = useTema();

  return (
    <Tela>
      <View style={estilos.topo}>
        <Texto tamanho="titulo" negrito accessibilityRole="header">
          Aurora Cadeiras
        </Texto>
        <Texto secundario>Entre com o e-mail e a senha que a fábrica deu a você.</Texto>
      </View>

      {vm.erro ? <Aviso tipo="erro" texto={vm.erro} /> : null}

      <CampoDeTexto
        rotulo="E-mail"
        value={vm.email}
        onChangeText={vm.setEmail}
        erro={vm.erroDoEmail}
        keyboardType="email-address"
        autoCapitalize="none"
        autoComplete="email"
        autoCorrect={false}
        textContentType="emailAddress"
      />
      <CampoDeTexto
        rotulo="Senha"
        value={vm.senha}
        onChangeText={vm.setSenha}
        erro={vm.erroDaSenha}
        secureTextEntry
        autoCapitalize="none"
        autoComplete="current-password"
        textContentType="password"
        onSubmitEditing={() => void vm.enviar()}
      />

      <View style={estilos.linha}>
        <Switch
          value={vm.manterConectado}
          onValueChange={vm.setManterConectado}
          accessibilityLabel="Manter conectado"
          accessibilityHint="Se ligado, o app não pede a senha de novo ao ser reaberto"
          trackColor={{ true: cores.primaria, false: cores.borda }}
          thumbColor={cores.superficie}
        />
        <Texto>Manter conectado</Texto>
      </View>

      <Botao titulo="Entrar" aoPressionar={() => void vm.enviar()} carregando={vm.enviando} />
    </Tela>
  );
}

const estilos = StyleSheet.create({
  topo: { gap: 6, paddingVertical: 12 },
  linha: { flexDirection: 'row', alignItems: 'center', gap: 12, minHeight: 56 },
});
