namespace Aurora.Web.Api.Comum;

/// <summary>Arquivo devolvido pela API (relatório em PDF ou Excel), repassado ao navegador como download.</summary>
public sealed record ArquivoBaixado(byte[] Conteudo, string TipoDeConteudo, string NomeDoArquivo);
