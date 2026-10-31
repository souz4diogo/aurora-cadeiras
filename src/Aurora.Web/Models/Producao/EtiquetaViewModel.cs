using Aurora.Web.Api.Producao;

namespace Aurora.Web.Models.Producao;

/// <param name="QrSvg">SVG do QR code já pronto para o HTML (gerado pelo servidor a partir de um código nosso, nunca de texto digitado).</param>
public sealed record EtiquetaViewModel(OrdemProducaoResposta Ordem, string QrSvg);
