namespace Aurora.Web.Models.Navegacao;

/// <summary>Entrada do menu principal e atalho da página inicial, visível só para os perfis listados.</summary>
public sealed record ItemDeMenu(string Titulo, string Descricao, string Controller, string Perfis);
