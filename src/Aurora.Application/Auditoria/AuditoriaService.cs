using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Excecoes;

namespace Aurora.Application.Auditoria;

/// <summary>Consulta do histórico de alterações (RNF10); só o Administrador chega aqui (a rota declara o perfil).</summary>
public class AuditoriaService(IAuditoriaRepository auditoria)
{
    /// <summary>Tabelas que têm trigger de auditoria (database/004-triggers.sql).</summary>
    public static IReadOnlyList<string> TabelasAuditadas { get; } = ["MateriaPrima", "OrdemProducao", "Pedido", "Usuario"];

    public async Task<Pagina<AuditoriaDto>> ListarAsync(
        FiltroDeAuditoria filtro, int pagina, int tamanho, CancellationToken ct = default)
    {
        if (filtro.De is not null && filtro.Ate is not null && filtro.De > filtro.Ate)
        {
            throw new RegraDeNegocioException(
                "FILTRO_INVALIDO", "A data inicial não pode ser depois da data final.", TipoErro.Validacao);
        }

        var tabela = Texto.OuNulo(filtro.Tabela);
        if (tabela is not null && !TabelasAuditadas.Contains(tabela, StringComparer.Ordinal))
        {
            throw new RegraDeNegocioException(
                "FILTRO_INVALIDO", $"A tabela \"{tabela}\" não é auditada.", TipoErro.Validacao);
        }

        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var (itens, total) = await auditoria.ListarAsync(
            filtro with { Tabela = tabela, Registro = Texto.OuNulo(filtro.Registro) }, pagina, tamanho, ct);
        return new Pagina<AuditoriaDto>(itens.Select(Montar).ToList(), total, pagina, tamanho);
    }

    private static AuditoriaDto Montar(AuditoriaResumo linha) => new(
        linha.Id,
        linha.Tabela,
        linha.Registro,
        DiferencaDeAuditoria.NomeDaAcao(linha.Acao),
        linha.UsuarioId,
        linha.UsuarioNome,
        linha.DataHora,
        DiferencaDeAuditoria.Calcular(linha.Antes, linha.Depois));
}
