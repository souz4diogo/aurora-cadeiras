const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * A etiqueta da OP guarda o CodigoQr (um GUID) da ordem (DECISOES A45). Qualquer outro QR code lido pela câmera
 * (de um produto, de um site) é recusado aqui, sem chamar a API.
 */
export function extrairCodigoDaOrdem(conteudo: string): string | null {
  const texto = conteudo.trim();
  return GUID.test(texto) ? texto.toLowerCase() : null;
}

export const MENSAGEM_QR_INVALIDO = 'Este QR code não é de uma ordem de produção da Aurora.';
