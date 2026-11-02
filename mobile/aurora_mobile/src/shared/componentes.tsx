import type { ReactNode } from 'react';
import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  type StyleProp,
  type TextInputProps,
  type TextProps,
  type TextStyle,
  type ViewStyle,
  RefreshControl,
} from 'react-native';

import { useTema } from '@/shared/tema';
import { vibrarToque } from '@/shared/vibracao';

/** Alvo de toque mínimo (docs/08 §8: operadores com luva ou pressa). */
export const ALVO_MINIMO = 56;

type Tamanho = 'pequeno' | 'normal' | 'grande' | 'titulo';
const TAMANHOS: Record<Tamanho, number> = { pequeno: 14, normal: 17, grande: 20, titulo: 26 };

interface PropsDeTexto extends TextProps {
  tamanho?: Tamanho;
  negrito?: boolean;
  secundario?: boolean;
  cor?: string;
}

/** Texto que respeita a escala do sistema e a escolhida em Configurações. */
export function Texto({ tamanho = 'normal', negrito, secundario, cor, style, ...resto }: PropsDeTexto) {
  const { cores, escala } = useTema();
  return (
    <Text
      {...resto}
      style={[
        {
          fontSize: TAMANHOS[tamanho] * escala,
          color: cor ?? (secundario ? cores.textoSecundario : cores.texto),
          fontWeight: negrito ? '700' : '400',
        },
        style,
      ]}
    />
  );
}

type Variante = 'primario' | 'secundario' | 'perigo';

interface PropsDeBotao {
  titulo: string;
  aoPressionar: () => void;
  variante?: Variante;
  carregando?: boolean;
  desabilitado?: boolean;
  /** Frase lida pelo leitor de tela depois do nome do botão. */
  dica?: string;
  estilo?: StyleProp<ViewStyle>;
}

/** Botão grande (mínimo 56 dp), com papel, estado e dica para o leitor de tela e vibração leve ao tocar. */
export function Botao({ titulo, aoPressionar, variante = 'primario', carregando, desabilitado, dica, estilo }: PropsDeBotao) {
  const { cores, altoContraste } = useTema();
  const inativo = desabilitado || carregando;
  const fundo =
    variante === 'primario' ? cores.primaria : variante === 'perigo' ? cores.perigo : cores.superficie;
  const texto =
    variante === 'primario' ? cores.textoSobrePrimaria : variante === 'perigo' ? cores.textoSobrePerigo : cores.primaria;
  const comBorda = variante === 'secundario' || altoContraste;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={titulo}
      accessibilityHint={dica}
      accessibilityState={{ disabled: !!inativo, busy: !!carregando }}
      disabled={inativo}
      onPress={() => {
        vibrarToque();
        aoPressionar();
      }}
      style={({ pressed }) => [
        estilos.botao,
        { backgroundColor: fundo, borderColor: comBorda ? texto : fundo, opacity: inativo ? 0.5 : pressed ? 0.85 : 1 },
        estilo,
      ]}
    >
      {carregando ? (
        <ActivityIndicator color={texto} />
      ) : (
        <Texto tamanho="grande" negrito cor={texto} style={estilos.textoDoBotao}>
          {titulo}
        </Texto>
      )}
    </Pressable>
  );
}

interface PropsDeCampo extends Omit<TextInputProps, 'style'> {
  rotulo: string;
  erro?: string | null;
}

/** Campo de texto com rótulo visível (lido pelo leitor de tela) e mensagem de erro com ícone e texto. */
export function CampoDeTexto({ rotulo, erro, ...resto }: PropsDeCampo) {
  const { cores, escala } = useTema();
  return (
    <View style={estilos.campo}>
      <Texto negrito>{rotulo}</Texto>
      <TextInput
        {...resto}
        accessibilityLabel={rotulo}
        placeholderTextColor={cores.textoSecundario}
        style={[
          estilos.entrada,
          {
            color: cores.texto,
            backgroundColor: cores.superficie,
            borderColor: erro ? cores.perigo : cores.borda,
            fontSize: TAMANHOS.normal * escala,
          },
        ]}
      />
      {erro ? <Texto tamanho="pequeno" cor={cores.perigo}>{`⚠ ${erro}`}</Texto> : null}
    </View>
  );
}

interface PropsDeCartao {
  children: ReactNode;
  estilo?: StyleProp<ViewStyle>;
  aoPressionar?: () => void;
  rotuloDeAcessibilidade?: string;
  dica?: string;
}

export function Cartao({ children, estilo, aoPressionar, rotuloDeAcessibilidade, dica }: PropsDeCartao) {
  const { cores } = useTema();
  const base = [estilos.cartao, { backgroundColor: cores.superficie, borderColor: cores.borda }, estilo];
  if (!aoPressionar) return <View style={base}>{children}</View>;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={rotuloDeAcessibilidade}
      accessibilityHint={dica}
      onPress={aoPressionar}
      style={({ pressed }) => [...base, { opacity: pressed ? 0.85 : 1 }]}
    >
      {children}
    </Pressable>
  );
}

type TipoDeAviso = 'erro' | 'aviso' | 'sucesso' | 'info';

/** Faixa de mensagem: o estado vem por ícone e texto, nunca só por cor (docs/08 §8). */
export function Aviso({ tipo, texto }: { tipo: TipoDeAviso; texto: string }) {
  const { cores } = useTema();
  const [icone, fundo, cor] =
    tipo === 'erro'
      ? ['⚠ Erro:', cores.fundoErro, cores.perigo]
      : tipo === 'aviso'
        ? ['⚠ Atenção:', cores.fundoAviso, cores.aviso]
        : tipo === 'sucesso'
          ? ['✔ Pronto:', cores.fundoSucesso, cores.sucesso]
          : ['ℹ Aviso:', cores.superficie, cores.texto];
  return (
    <View
      accessibilityRole={tipo === 'erro' ? 'alert' : undefined}
      accessibilityLiveRegion="polite"
      style={[estilos.aviso, { backgroundColor: fundo, borderColor: cor }]}
    >
      <Texto cor={cor}>
        <Texto negrito cor={cor}>{`${icone} `}</Texto>
        {texto}
      </Texto>
    </View>
  );
}

export function EstadoVazio({ titulo, texto, acao }: { titulo: string; texto: string; acao?: ReactNode }) {
  return (
    <View style={estilos.vazio} accessible accessibilityLabel={`${titulo}. ${texto}`}>
      <Texto tamanho="grande" negrito style={estilos.centro}>{titulo}</Texto>
      <Texto secundario style={estilos.centro}>{texto}</Texto>
      {acao}
    </View>
  );
}

interface PropsDeTela {
  children: ReactNode;
  carregando?: boolean;
  aoAtualizar?: () => void;
  atualizando?: boolean;
}

/** Corpo padrão das telas: rolável, com "puxar para atualizar" quando a tela atualiza dados. */
export function Tela({ children, carregando, aoAtualizar, atualizando }: PropsDeTela) {
  const { cores } = useTema();
  return (
    <ScrollView
      style={{ backgroundColor: cores.fundo }}
      contentContainerStyle={estilos.tela}
      keyboardShouldPersistTaps="handled"
      refreshControl={
        aoAtualizar ? <RefreshControl refreshing={!!atualizando} onRefresh={aoAtualizar} tintColor={cores.primaria} /> : undefined
      }
    >
      {carregando ? <ActivityIndicator size="large" color={cores.primaria} accessibilityLabel="Carregando" /> : children}
    </ScrollView>
  );
}

export const estilosComuns = StyleSheet.create({
  linha: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  espaco: { gap: 12 },
});

const estilos = StyleSheet.create({
  botao: {
    minHeight: ALVO_MINIMO,
    paddingHorizontal: 20,
    paddingVertical: 12,
    borderRadius: 12,
    borderWidth: 2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  textoDoBotao: { textAlign: 'center' },
  campo: { gap: 6 },
  entrada: { minHeight: ALVO_MINIMO, borderWidth: 2, borderRadius: 12, paddingHorizontal: 14 },
  cartao: { borderWidth: 1, borderRadius: 14, padding: 16, gap: 6 },
  aviso: { borderWidth: 2, borderRadius: 12, padding: 12 },
  vazio: { alignItems: 'center', gap: 10, paddingVertical: 40, paddingHorizontal: 16 },
  centro: { textAlign: 'center' } satisfies TextStyle,
  tela: { padding: 16, gap: 14, flexGrow: 1 },
});
