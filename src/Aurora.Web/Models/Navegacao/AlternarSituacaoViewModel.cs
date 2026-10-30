namespace Aurora.Web.Models.Navegacao;

/// <summary>Dados do botão de ativar/inativar de uma linha de lista.</summary>
public sealed record AlternarSituacaoViewModel(int Id, string Nome, bool Ativo);
