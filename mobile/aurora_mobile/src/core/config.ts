/**
 * URL base da API, definida em tempo de build por EXPO_PUBLIC_API_BASE_URL (docs/08 §1).
 * No emulador Android o host da máquina é 10.0.2.2; 5011 é a porta do `dotnet run` da API.
 * Em produção a URL é HTTPS. Nada secreto vive aqui: o app não embute chaves.
 */
export const API_BASE_URL: string =
  process.env.EXPO_PUBLIC_API_BASE_URL ?? 'http://10.0.2.2:5011/api/v1';

/**
 * Fora do desenvolvimento a API só pode ser falada por HTTPS (docs/08 §9, RNF03). HTTP simples serve ao emulador e ao
 * `expo start`; um build de produção com URL HTTP é recusado, em vez de trafegar token e dados em claro.
 */
export function urlDaApiInsegura(url: string, emDesenvolvimento: boolean): boolean {
  return !emDesenvolvimento && !/^https:\/\//i.test(url.trim());
}

export const URL_DA_API_E_INSEGURA = urlDaApiInsegura(API_BASE_URL, __DEV__);

/** Máximo de apontamentos por chamada de sincronização (docs/08 §5). */
export const TAMANHO_DO_LOTE = 50;

/** Intervalo da sincronização periódica com o app em primeiro plano (docs/08 §5). */
export const INTERVALO_DE_SINCRONIZACAO_MS = 5 * 60 * 1000;

/** Tempo máximo de espera de uma chamada à API. */
export const TIMEOUT_DA_API_MS = 15_000;
