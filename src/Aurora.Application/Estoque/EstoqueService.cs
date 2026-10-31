using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Application.MateriasPrimas;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Estoque;

public class EstoqueService(
    IEstoqueRepository estoque,
    IMateriaPrimaRepository materiasPrimas,
    IUnitOfWork unitOfWork,
    ICurrentUser usuarioAtual,
    IRelogio relogio,
    IValidator<AjusteDeEstoqueRequest> validador)
{
    /// <summary>Saldos de matéria-prima (RF14); <paramref name="critico"/> filtra as que estão no mínimo ou abaixo (RN08).</summary>
    public async Task<Pagina<SaldoDeMateriaPrimaDto>> ListarSaldosDeMateriaPrimaAsync(
        string? busca, bool? critico, int pagina, int tamanho, CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var (itens, total) = await estoque.ListarSaldosDeMateriaPrimaAsync(Texto.OuNulo(busca), critico, pagina, tamanho, ct);
        return new Pagina<SaldoDeMateriaPrimaDto>(itens, total, pagina, tamanho);
    }

    /// <summary>Saldos de produto acabado: total, reservado a pedidos e disponível (RF14).</summary>
    public async Task<Pagina<SaldoDeProdutoDto>> ListarSaldosDeProdutoAsync(
        string? busca, int pagina, int tamanho, CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var (itens, total) = await estoque.ListarSaldosDeProdutoAsync(Texto.OuNulo(busca), pagina, tamanho, ct);
        return new Pagina<SaldoDeProdutoDto>(itens, total, pagina, tamanho);
    }

    public async Task<Pagina<AlertaDeEstoqueDto>> ListarAlertasAbertosAsync(
        int pagina, int tamanho, CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var (itens, total) = await estoque.ListarAlertasAbertosAsync(pagina, tamanho, ct);
        return new Pagina<AlertaDeEstoqueDto>(itens, total, pagina, tamanho);
    }

    /// <summary>Marca o alerta como lido (RF15). Repetir é inofensivo: o primeiro registro de leitura permanece.</summary>
    public async Task<AlertaDeEstoqueDto> MarcarAlertaComoLidoAsync(int id, CancellationToken ct = default)
    {
        var usuarioId = usuarioAtual.UsuarioId
            ?? throw new RegraDeNegocioException("NAO_AUTENTICADO", "Não autenticado.", TipoErro.NaoAutenticado);
        var alerta = await estoque.ObterAlertaAsync(id, ct)
            ?? throw new RegraDeNegocioException("ALERTA_NAO_ENCONTRADO", "Alerta não encontrado.", TipoErro.NaoEncontrado);

        alerta.MarcarComoLido(usuarioId, relogio.AgoraUtc);
        await unitOfWork.SalvarAsync(ct);

        return await estoque.ObterAlertaDetalhadoAsync(id, ct)
            ?? throw new RegraDeNegocioException("ALERTA_NAO_ENCONTRADO", "Alerta não encontrado.", TipoErro.NaoEncontrado);
    }

    public async Task<Pagina<MovimentoEstoqueDto>> ListarMovimentosAsync(
        int? materiaPrimaId,
        int? produtoId,
        TipoMovimento? tipo,
        OrigemMovimento? origem,
        DateOnly? de,
        DateOnly? ate,
        int pagina,
        int tamanho,
        CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var filtro = new FiltroDeMovimentos(materiaPrimaId, produtoId, tipo, origem, de, ate);
        var (itens, total) = await estoque.ListarMovimentosAsync(filtro, pagina, tamanho, ct);
        return new Pagina<MovimentoEstoqueDto>(itens.Select(MovimentoEstoqueDto.De).ToList(), total, pagina, tamanho);
    }

    /// <summary>Corrige o saldo de uma matéria-prima (RF14): o livro-razão não é editado, ganha um movimento de ajuste.</summary>
    public async Task<AjusteDeEstoqueDto> AjustarAsync(AjusteDeEstoqueRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);

        var antes = await ObterAsync(request.MateriaPrimaId, ct);
        await estoque.AjustarMateriaPrimaAsync(request.MateriaPrimaId, request.NovaQuantidade, request.Motivo.Trim(), ct);
        var depois = await ObterAsync(request.MateriaPrimaId, ct);

        return new AjusteDeEstoqueDto(MateriaPrimaDto.De(depois), antes.EstoqueAtual, depois.EstoqueAtual - antes.EstoqueAtual);
    }

    private async Task<MateriaPrima> ObterAsync(int materiaPrimaId, CancellationToken ct) =>
        (await materiasPrimas.ObterPorIdsAsync([materiaPrimaId], ct)).SingleOrDefault()
        ?? throw new RegraDeNegocioException(
            "MATERIA_PRIMA_NAO_ENCONTRADA", "Matéria-prima não encontrada.", TipoErro.NaoEncontrado);
}
