import type { AxiosInstance } from 'axios';
import { randomUUID } from 'expo-crypto';

import { TokenStoreEscolhivel } from '@/core/auth/tokenStore';
import { URL_DA_API_E_INSEGURA } from '@/core/config';
import { CacheJsonRepository } from '@/core/db/cacheJsonRepository';
import type { Db } from '@/core/db/db';
import { abrirBancoLocal } from '@/core/db/expoDb';
import { apagarDadosLocais } from '@/core/db/migracoes';
import { criarApiClient } from '@/core/http/apiClient';
import { AuthApi } from '@/features/auth/authApi';
import { AuthService } from '@/features/auth/authService';
import { EstacoesService } from '@/features/estacoes/estacoesService';
import { EtapaCacheRepository } from '@/features/etapas/etapaCacheRepository';
import { EtapasApi } from '@/features/etapas/etapasApi';
import { EtapasService } from '@/features/etapas/etapasService';
import { MateriaisService } from '@/features/etapas/materiaisService';
import { EstoqueService } from '@/features/estoque/estoqueService';
import { PedidosService } from '@/features/pedidos/pedidosService';
import { PerdasService } from '@/features/perdas/perdasService';
import { PreferenciasRepository } from '@/features/preferencias/preferenciasRepository';
import { InspecaoService } from '@/features/qualidade/inspecaoService';
import { FilaRepository } from '@/features/sync/filaRepository';
import { SincronizacaoHttpApi } from '@/features/sync/sincronizacaoApi';
import { SyncService } from '@/features/sync/syncService';

const CHAVE_DO_USUARIO = 'usuario_id';

/** Tudo o que as telas usam. As telas recebem isto por contexto (ver ServicosProvider) e nunca montam serviços. */
export interface Servicos {
  db: Db;
  http: AxiosInstance;
  auth: AuthService;
  etapas: EtapasService;
  materiais: MateriaisService;
  estacoes: EstacoesService;
  sync: SyncService;
  fila: FilaRepository;
  perdas: PerdasService;
  inspecao: InspecaoService;
  estoque: EstoqueService;
  pedidos: PedidosService;
  preferencias: PreferenciasRepository;
}

/**
 * Monta os serviços sobre o banco do aparelho e o cliente HTTP. `aoExpirarSessao` é chamado quando a renovação do
 * token falha, e leva o app de volta ao Login.
 */
export async function criarServicos(aoExpirarSessao: () => void): Promise<Servicos> {
  if (URL_DA_API_E_INSEGURA) throw new Error('A URL da API precisa ser HTTPS fora do desenvolvimento.');
  const db = await abrirBancoLocal();
  const tokenStore = new TokenStoreEscolhivel();
  const http = criarApiClient({ tokenStore, aoExpirarSessao });

  const fila = new FilaRepository(db);
  // O envio que o app interrompeu ao fechar volta para a fila; o servidor ignora o que já recebeu
  await fila.recuperarEnvioInterrompido();

  const cacheJson = new CacheJsonRepository(db);
  const preferencias = new PreferenciasRepository(db);
  const etapasApi = new EtapasApi(http);
  const etapas = new EtapasService({ api: etapasApi, cache: new EtapaCacheRepository(db), fila, novoId: randomUUID });
  const estacoes = new EstacoesService(etapasApi, cacheJson, preferencias);
  const sync = new SyncService({
    fila,
    api: new SincronizacaoHttpApi(http),
    aoConfirmar: (itens) => etapas.registrarConfirmados(itens),
    // Depois de enviar, recarrega do servidor a lista da estação escolhida (o servidor é a fonte da verdade)
    aposSincronizar: async () => {
      const estacao = await estacoes.estacaoEscolhida();
      if (estacao !== null) await etapas.atualizarCache(estacao);
    },
  });

  return {
    db,
    http,
    auth: new AuthService({
      api: new AuthApi(http),
      tokenStore,
      limparDadosLocais: () => apagarDadosLocais(db),
      aoAutenticar: async (usuario) => {
        // Outro usuário no mesmo aparelho: o que sobrou do anterior (inclusive apontamentos não enviados) não pode
        // ser enviado em nome de quem acabou de entrar
        const anterior = await preferencias.obter(CHAVE_DO_USUARIO);
        if (anterior !== null && anterior !== String(usuario.id)) await apagarDadosLocais(db);
        await preferencias.definir(CHAVE_DO_USUARIO, String(usuario.id));
      },
    }),
    etapas,
    materiais: new MateriaisService(etapasApi, cacheJson),
    estacoes,
    sync,
    fila,
    perdas: new PerdasService(etapasApi),
    inspecao: new InspecaoService(http, etapasApi),
    estoque: new EstoqueService(http, cacheJson),
    pedidos: new PedidosService(http, cacheJson),
    preferencias,
  };
}
