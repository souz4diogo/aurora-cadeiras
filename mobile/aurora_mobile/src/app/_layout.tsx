import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { ActivityIndicator, View } from 'react-native';

import { ServicosProvider } from '@/core/ServicosProvider';
import { criarServicos, type Servicos } from '@/core/servicos';
import { AuthProvider, eventosDeSessao, useAuth } from '@/features/auth/AuthProvider';
import { SeloDeSincronizacao } from '@/features/sync/SeloDeSincronizacao';
import { SyncProvider } from '@/features/sync/SyncProvider';
import { Aviso, Tela } from '@/shared/componentes';
import { ProvedorDeTema, useTema } from '@/shared/tema';

export default function LayoutRaiz() {
  const [servicos, setServicos] = useState<Servicos | null>(null);
  const [falha, setFalha] = useState(false);

  useEffect(() => {
    criarServicos(() => eventosDeSessao.expirou())
      .then(setServicos)
      .catch(() => setFalha(true));
  }, []);

  if (falha) {
    return (
      <Tela>
        <Aviso tipo="erro" texto="Não foi possível abrir o banco de dados do aparelho. Feche e abra o app de novo." />
      </Tela>
    );
  }
  if (!servicos) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center' }}>
        <ActivityIndicator size="large" accessibilityLabel="Abrindo o aplicativo" />
      </View>
    );
  }

  return (
    <ServicosProvider servicos={servicos}>
      <ProvedorDeTema preferencias={servicos.preferencias}>
        <AuthProvider>
          <Navegacao />
        </AuthProvider>
      </ProvedorDeTema>
    </ServicosProvider>
  );
}

function Navegacao() {
  const { estado } = useAuth();
  const { cores } = useTema();
  const logado = estado.status === 'logado';

  if (estado.status === 'carregando') {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: cores.fundo }}>
        <ActivityIndicator size="large" color={cores.primaria} accessibilityLabel="Carregando" />
      </View>
    );
  }

  return (
    <SyncProvider ativo={logado}>
      <StatusBar style="light" />
      {logado ? <SeloDeSincronizacao /> : null}
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: cores.primaria },
          headerTintColor: cores.textoSobrePrimaria,
          headerTitleStyle: { fontWeight: '700' },
          contentStyle: { backgroundColor: cores.fundo },
        }}
      >
        <Stack.Protected guard={!logado}>
          <Stack.Screen name="login" options={{ headerShown: false }} />
        </Stack.Protected>
        <Stack.Protected guard={logado}>
          <Stack.Screen name="index" options={{ headerShown: false }} />
          <Stack.Screen name="estacao" options={{ title: 'Escolher estação' }} />
          <Stack.Screen name="etapas" options={{ title: 'Minhas etapas' }} />
          <Stack.Screen name="qr" options={{ title: 'Ler QR code' }} />
          <Stack.Screen name="etapa/[id]" options={{ title: 'Etapa' }} />
          <Stack.Screen name="perda/[etapaId]" options={{ title: 'Registrar perda' }} />
          <Stack.Screen name="inspecao/[ordemId]" options={{ title: 'Inspeção de qualidade' }} />
          <Stack.Screen name="estoque" options={{ title: 'Estoque' }} />
          <Stack.Screen name="fila" options={{ title: 'Fila de sincronização' }} />
          <Stack.Screen name="pedidos" options={{ title: 'Meus pedidos' }} />
          <Stack.Screen name="pedido/[id]" options={{ title: 'Pedido' }} />
          <Stack.Screen name="configuracoes" options={{ title: 'Configurações' }} />
          <Stack.Screen name="nao-suportado" options={{ title: 'Aurora Cadeiras' }} />
        </Stack.Protected>
      </Stack>
    </SyncProvider>
  );
}
