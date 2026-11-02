using Aurora.Application.Abstracoes;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Application.Relatorios;

/// <summary>Um relatório que o perfil do usuário pode pedir, com os filtros que a tela deve oferecer.</summary>
public sealed record RelatorioDisponivelDto(string Tipo, string Titulo, bool AceitaPeriodo, bool AceitaStatus, bool AceitaAno);

/// <summary>
/// Relatórios exportáveis em PDF e Excel (RF21). O serviço só orquestra: acha o gerador do tipo (Strategy), confere o
/// perfil, escolhe o exportador do formato (Factory) e devolve o arquivo. Tipos e formatos novos entram sem mexer aqui.
/// </summary>
public class RelatorioService(
    IEnumerable<IGeradorDeRelatorio> geradores,
    IExportadorFactory exportadores,
    ICurrentUser usuarioAtual,
    IRelogio relogio)
{
    /// <summary>Os relatórios que o perfil logado pode gerar (a tela monta a lista a partir daqui).</summary>
    public IReadOnlyList<RelatorioDisponivelDto> ListarDisponiveis() =>
        geradores
            .Where(PodeGerar)
            .Select(g => new RelatorioDisponivelDto(
                g.Tipo,
                g.Titulo,
                g.Filtros.HasFlag(FiltrosDoRelatorio.Periodo),
                g.Filtros.HasFlag(FiltrosDoRelatorio.Status),
                g.Filtros.HasFlag(FiltrosDoRelatorio.Ano)))
            .ToList();

    public async Task<ArquivoGerado> GerarAsync(string tipo, string? formato, FiltroDeRelatorio filtro, CancellationToken ct = default)
    {
        var gerador = geradores.FirstOrDefault(g => string.Equals(g.Tipo, tipo, StringComparison.OrdinalIgnoreCase))
            ?? throw new RegraDeNegocioException(
                "RELATORIO_INEXISTENTE", $"Não existe o relatório \"{tipo}\".", TipoErro.NaoEncontrado);

        if (!PodeGerar(gerador))
        {
            throw new RegraDeNegocioException(
                "SEM_PERMISSAO", "Seu perfil não pode gerar este relatório.", TipoErro.Proibido);
        }

        var exportador = exportadores.Criar(formato)
            ?? throw new RegraDeNegocioException(
                "FORMATO_INVALIDO",
                $"Informe o formato do arquivo: {string.Join(" ou ", exportadores.FormatosDisponiveis)}.",
                TipoErro.Validacao);

        var agora = relogio.AgoraUtc;
        var relatorio = await gerador.GerarAsync(filtro, agora, ct);
        var conteudo = exportador.Exportar(relatorio);
        return new ArquivoGerado(conteudo, exportador.TipoDeConteudo, $"relatorio-{gerador.Tipo}-{agora:yyyyMMdd-HHmm}.{exportador.Extensao}");
    }

    private bool PodeGerar(IGeradorDeRelatorio gerador) =>
        usuarioAtual.Perfil is PerfilUsuario perfil && gerador.Perfis.Contains(perfil);
}
