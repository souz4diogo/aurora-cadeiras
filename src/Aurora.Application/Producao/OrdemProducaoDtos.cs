using Aurora.Domain.Enums;

namespace Aurora.Application.Producao;

/// <summary>Etapa em que a OP está agora: a primeira ainda não concluída.</summary>
public sealed record EtapaAtualResumo(
    int EtapaId,
    byte EstacaoId,
    string EstacaoNome,
    byte Sequencia,
    StatusEtapa Status,
    DateTime? IniciadaEm);

/// <summary>Linha da listagem de OPs (leitura), já com produto, pedido e etapa atual.</summary>
public sealed record OrdemProducaoResumo(
    int Id,
    string Numero,
    int PedidoId,
    string PedidoNumero,
    int ProdutoId,
    string ProdutoCodigo,
    string ProdutoNome,
    int Quantidade,
    StatusOrdemProducao Status,
    DateOnly? PrevisaoConclusao,
    DateTime CriadaEm,
    DateTime? ConcluidaEm,
    int TotalDeEtapas,
    int EtapasConcluidas,
    EtapaAtualResumo? EtapaAtual)
{
    /// <summary>Mesma regra da view vw_PainelProducao: OP em aberto com a previsão vencida.</summary>
    public bool EstaAtrasada(DateOnly hoje) =>
        Status is StatusOrdemProducao.Planejada or StatusOrdemProducao.EmAndamento && PrevisaoConclusao < hoje;
}

public sealed record EtapaAtualDto(
    int EtapaId,
    byte EstacaoId,
    string EstacaoNome,
    byte Sequencia,
    string Status,
    DateTime? IniciadaEm,
    int? MinutosNaEtapa)
{
    public static EtapaAtualDto? De(EtapaAtualResumo? etapa, DateTime agoraUtc) => etapa is null
        ? null
        : new(
            etapa.EtapaId,
            etapa.EstacaoId,
            etapa.EstacaoNome,
            etapa.Sequencia,
            etapa.Status.ToString(),
            etapa.IniciadaEm,
            etapa is { Status: StatusEtapa.EmAndamento, IniciadaEm: { } inicio }
                ? Math.Max(0, (int)(agoraUtc - inicio).TotalMinutes)
                : null);
}

public sealed record OrdemProducaoResumoDto(
    int Id,
    string Numero,
    int PedidoId,
    string PedidoNumero,
    int ProdutoId,
    string ProdutoCodigo,
    string ProdutoNome,
    int Quantidade,
    string Status,
    DateOnly? PrevisaoConclusao,
    bool Atrasada,
    DateTime CriadaEm,
    DateTime? ConcluidaEm,
    int TotalDeEtapas,
    int EtapasConcluidas,
    EtapaAtualDto? EtapaAtual)
{
    public static OrdemProducaoResumoDto De(OrdemProducaoResumo o, DateTime agoraUtc) => new(
        o.Id,
        o.Numero,
        o.PedidoId,
        o.PedidoNumero,
        o.ProdutoId,
        o.ProdutoCodigo,
        o.ProdutoNome,
        o.Quantidade,
        o.Status.ToString(),
        o.PrevisaoConclusao,
        o.EstaAtrasada(DateOnly.FromDateTime(agoraUtc)),
        o.CriadaEm,
        o.ConcluidaEm,
        o.TotalDeEtapas,
        o.EtapasConcluidas,
        EtapaAtualDto.De(o.EtapaAtual, agoraUtc));
}

public sealed record ApontamentoDto(
    long Id,
    int EtapaId,
    string Tipo,
    int UsuarioId,
    string UsuarioNome,
    DateTime DataHoraCliente,
    DateTime DataHoraServidor);

public sealed record PerdaDto(
    int Id,
    int EtapaId,
    string EstacaoNome,
    string Tipo,
    int Quantidade,
    string Motivo,
    int UsuarioId,
    string? UsuarioNome,
    DateTime CriadoEm);

/// <summary>OP com tudo o que a tela de detalhe e a etiqueta precisam (RF09 a RF12).</summary>
public sealed record OrdemProducaoDto(
    OrdemProducaoResumoDto Ordem,
    Guid CodigoQr,
    string ClienteNome,
    IReadOnlyList<EtapaDto> Etapas,
    IReadOnlyList<ApontamentoDto> Apontamentos,
    IReadOnlyList<PerdaDto> Perdas);

/// <summary>Leitura completa de uma OP; a montagem do DTO fica no serviço, que conhece o relógio.</summary>
public sealed record OrdemProducaoDetalhe(
    OrdemProducaoResumo Resumo,
    Guid CodigoQr,
    string ClienteNome,
    IReadOnlyList<EtapaResumo> Etapas,
    IReadOnlyList<ApontamentoDto> Apontamentos,
    IReadOnlyList<PerdaDto> Perdas);
