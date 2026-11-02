import type { AxiosInstance } from 'axios';
import type { ReactNode } from 'react';
import { act } from '@testing-library/react-native';

import type { Perfil, UsuarioLogado } from '@/core/auth/tokenStore';
import { CacheJsonRepository } from '@/core/db/cacheJsonRepository';
import type { Db } from '@/core/db/db';
import { ServicosProvider } from '@/core/ServicosProvider';
import type { Servicos } from '@/core/servicos';
import { AuthProvider, useAuth } from '@/features/auth/AuthProvider';
import type { AuthService } from '@/features/auth/authService';
import { EstacoesService } from '@/features/estacoes/estacoesService';
import { EtapaCacheRepository } from '@/features/etapas/etapaCacheRepository';
import type { EtapasApi } from '@/features/etapas/etapasApi';
import { EtapasService } from '@/features/etapas/etapasService';
import { MateriaisService } from '@/features/etapas/materiaisService';
import type { EtapaDaEstacao, Estacao, MaterialDaEtapa, OrdemProducaoDetalhe } from '@/features/etapas/tipos';
import { EstoqueService } from '@/features/estoque/estoqueService';
import { PedidosService } from '@/features/pedidos/pedidosService';
import { PerdasService } from '@/features/perdas/perdasService';
import { PreferenciasRepository } from '@/features/preferencias/preferenciasRepository';
import { InspecaoService } from '@/features/qualidade/inspecaoService';
import { FilaRepository } from '@/features/sync/filaRepository';
import { SyncProvider } from '@/features/sync/SyncProvider';
import { SyncService } from '@/features/sync/syncService';
import { ProvedorDeTema } from '@/shared/tema';
import { __emitir as emitirEstadoDaRede, __redefinir as redefinirRede } from './netinfoFalso';
import { ServidorDeSincronizacaoFalso, erroDeRede } from './servidorFalso';
import { criarBancoDeTeste } from './sqliteEmMemoria';

/** Como o layout raiz: as telas protegidas só montam depois que a sessão foi restaurada. */
function AguardaSessao({ children }: { children: ReactNode }) {
  const { estado } = useAuth();
  return estado.status === 'logado' ? <>{children}</> : null;
}

const ESTACOES: Estacao[] = [
  { id: 1, nome: 'Corte', ordem: 1, geraEtapa: true },
  { id: 2, nome: 'Montagem da Estrutura', ordem: 2, geraEtapa: true },
  { id: 5, nome: 'Qualidade', ordem: 5, geraEtapa: true },
];

export interface AppDeTeste {
  db: Db;
  servicos: Servicos;
  servidor: ServidorDeSincronizacaoFalso;
  fila: FilaRepository;
  /** O que o servidor devolve em GET /etapas/minhas; o teste altera para simular o servidor mudando. */
  doServidor: { etapas: EtapaDaEstacao[]; materiais: MaterialDaEtapa[]; ordem: OrdemProducaoDetalhe | null };
  api: { minhas: jest.Mock; registrarPerda: jest.Mock };
  http: { get: jest.Mock; post: jest.Mock };
  auth: { restaurar: jest.Mock; entrar: jest.Mock; sair: jest.Mock };
  /** Liga ou desliga a rede de mentira (API, sincronização e NetInfo). */
  definirOnline(online: boolean): Promise<void>;
  Envolver: (props: { children: ReactNode }) => ReactNode;
}

/**
 * Monta o app como no aparelho: repositórios e serviços de verdade sobre um SQLite em memória, com os providers de
 * sessão, tema e sincronização. Só a rede é de mentira. Cada teste recebe um app novo.
 */
export async function montarApp(perfil: Perfil = 'Operador', estacaoEscolhida: number | null = 1): Promise<AppDeTeste> {
  redefinirRede();
  const db = await criarBancoDeTeste();
  const fila = new FilaRepository(db);
  const cacheJson = new CacheJsonRepository(db);
  const preferencias = new PreferenciasRepository(db);
  const servidor = new ServidorDeSincronizacaoFalso();
  const doServidor: AppDeTeste['doServidor'] = { etapas: [], materiais: [], ordem: null };
  let online = true;
  const quando = <T,>(valor: () => T) => async (): Promise<T> => {
    if (!online) throw erroDeRede();
    return valor();
  };

  const api = {
    minhas: jest.fn(async (_estacaoId: number) => {
      if (!online) throw erroDeRede();
      return doServidor.etapas.filter((e) => e.estacaoId === _estacaoId);
    }),
    registrarPerda: jest.fn(async () => {
      if (!online) throw erroDeRede();
    }),
  };
  const etapasApi = {
    ...api,
    estacoes: quando(() => ESTACOES),
    materiaisDoProduto: quando(() => doServidor.materiais),
    ordem: quando(() => doServidor.ordem as OrdemProducaoDetalhe),
    ordemPorQr: quando(() => doServidor.ordem as OrdemProducaoDetalhe),
  } as unknown as EtapasApi;
  const http = {
    get: jest.fn(async () => {
      throw erroDeRede();
    }),
    post: jest.fn(async () => {
      throw erroDeRede();
    }),
  };

  let contador = 0;
  const etapas = new EtapasService({
    api: etapasApi,
    cache: new EtapaCacheRepository(db),
    fila,
    novoId: () => `req-${++contador}`,
  });
  const estacoes = new EstacoesService(etapasApi, cacheJson, preferencias);
  if (estacaoEscolhida !== null) await estacoes.escolher(estacaoEscolhida);

  const usuario: UsuarioLogado = { id: 12, nome: 'Carlos', perfil };
  const auth = {
    restaurar: jest.fn(async () => usuario),
    entrar: jest.fn(async () => usuario),
    sair: jest.fn(async () => {}),
  };

  const sync = new SyncService({
    fila,
    api: {
      sincronizar: async (itens) => {
        servidor.online = online;
        const resultados = await servidor.sincronizar(itens);
        // O servidor real passa a devolver a etapa como em andamento, e a concluída some de /etapas/minhas
        for (const resultado of resultados.filter((r) => r.status === 'Aplicado')) {
          const aplicado = itens.find((i) => i.clientRequestId === resultado.clientRequestId);
          if (!aplicado) continue;
          if (aplicado.tipo === 'Fim') {
            doServidor.etapas = doServidor.etapas.filter((e) => e.etapaId !== aplicado.etapaId);
          } else {
            doServidor.etapas = doServidor.etapas.map((e) =>
              e.etapaId === aplicado.etapaId ? { ...e, status: 'EmAndamento', iniciadaEm: aplicado.dataHoraCliente } : e,
            );
          }
        }
        return resultados;
      },
    },
    aposSincronizar: () => etapas.atualizarCache(1),
  });

  const servicos: Servicos = {
    db,
    http: http as unknown as AxiosInstance,
    auth: auth as unknown as AuthService,
    etapas,
    materiais: new MateriaisService(etapasApi, cacheJson),
    estacoes,
    sync,
    fila,
    perdas: new PerdasService(etapasApi),
    inspecao: new InspecaoService(http as unknown as AxiosInstance, etapasApi),
    estoque: new EstoqueService(http as unknown as AxiosInstance, cacheJson),
    pedidos: new PedidosService(http as unknown as AxiosInstance, cacheJson),
    preferencias,
  };

  return {
    db,
    servicos,
    servidor,
    fila,
    doServidor,
    api,
    http,
    auth,
    async definirOnline(valor: boolean) {
      online = valor;
      servidor.online = valor;
      await act(async () => {
        emitirEstadoDaRede({ isConnected: valor, isInternetReachable: valor });
      });
    },
    Envolver: ({ children }) => (
      <ServicosProvider servicos={servicos}>
        <ProvedorDeTema preferencias={preferencias}>
          <AuthProvider>
            <AguardaSessao>
              <SyncProvider ativo>{children}</SyncProvider>
            </AguardaSessao>
          </AuthProvider>
        </ProvedorDeTema>
      </ServicosProvider>
    ),
  };
}
