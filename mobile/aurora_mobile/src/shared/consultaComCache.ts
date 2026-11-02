import type { CacheJsonRepository } from '@/core/db/cacheJsonRepository';
import { ehErroDeRede, interpretarErro } from '@/core/http/problem';

export interface ResultadoDaConsulta<T> {
  dados: T;
  /** O servidor não respondeu e estes são os últimos dados conhecidos. */
  doCache: boolean;
  atualizadoEm: string;
}

/**
 * Consulta somente leitura com cache: servidor primeiro (e guarda a resposta); sem rede, o último dado conhecido.
 * Sem rede e sem cache, devolve o erro de conexão para a tela explicar. Outros erros viram mensagem em português.
 */
export async function consultarComCache<T>(
  cache: CacheJsonRepository,
  chave: string,
  buscar: () => Promise<T>,
): Promise<ResultadoDaConsulta<T>> {
  try {
    const dados = await buscar();
    const atualizadoEm = await cache.guardar(chave, dados);
    return { dados, doCache: false, atualizadoEm };
  } catch (erro) {
    if (!ehErroDeRede(erro)) throw interpretarErro(erro);
    const guardado = await cache.obter<T>(chave);
    if (!guardado) throw interpretarErro(erro);
    return { dados: guardado.valor, doCache: true, atualizadoEm: guardado.atualizadoEm };
  }
}
