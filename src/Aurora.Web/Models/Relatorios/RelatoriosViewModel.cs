using Aurora.Web.Api.Relatorios;

namespace Aurora.Web.Models.Relatorios;

public sealed record FormatoDeArquivo(string Codigo, string Rotulo);

public sealed record SituacaoOpcao(string Valor, string Rotulo);

/// <param name="Relatorios">Só os relatórios que o perfil logado pode gerar (a API decide).</param>
public sealed record RelatoriosViewModel(IReadOnlyList<RelatorioDisponivelResposta> Relatorios, int AnoAtual)
{
    public IReadOnlyList<FormatoDeArquivo> Formatos { get; } = [new("pdf", "PDF"), new("xlsx", "Excel")];

    /// <summary>Situações do filtro; por ora só o relatório de pedidos filtra por situação.</summary>
    public IReadOnlyList<SituacaoOpcao> Situacoes(string tipo) => tipo == "pedidos"
        ?
        [
            new("Aberto", "Aberto"),
            new("Confirmado", "Confirmado"),
            new("EmProducao", "Em produção"),
            new("Pronto", "Pronto"),
            new("Expedido", "Expedido"),
            new("Cancelado", "Cancelado"),
        ]
        : [];
}
