import { AxiosError, type AxiosAdapter, type InternalAxiosRequestConfig } from 'axios';

export interface RespostaFalsa {
  status: number;
  data?: unknown;
}

export type Tratador = (config: InternalAxiosRequestConfig) => RespostaFalsa | Promise<RespostaFalsa>;

/**
 * Adaptador de rede do axios para testes. O axios só converte status 4xx/5xx em erro dentro dos adaptadores
 * reais, então este faz o mesmo, para o interceptor ver o que veria no aparelho.
 */
export function adaptadorFalso(tratador: Tratador): { adapter: AxiosAdapter; chamadas: InternalAxiosRequestConfig[] } {
  const chamadas: InternalAxiosRequestConfig[] = [];
  const adapter: AxiosAdapter = async (config) => {
    chamadas.push(config);
    const { status, data = {} } = await tratador(config);
    const resposta = { data, status, statusText: String(status), headers: {}, config };
    if (status >= 200 && status < 300) return resposta;
    throw new AxiosError(`Request failed with status code ${status}`, 'ERR_BAD_REQUEST', config, null, resposta);
  };
  return { adapter, chamadas };
}

export function cabecalho(config: InternalAxiosRequestConfig, nome: string): string | undefined {
  const valor = config.headers.get(nome);
  return typeof valor === 'string' ? valor : undefined;
}
