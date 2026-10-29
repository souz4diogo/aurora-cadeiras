using Aurora.Domain.Excecoes;
using Microsoft.Data.SqlClient;

namespace Aurora.Infrastructure.Erros;

/// <summary>Traduz os erros das procedures (THROW 51001-51010) e de integridade em exceções de regra de negócio.</summary>
public static class SqlErroTradutor
{
    public static RegraDeNegocioException? Traduzir(SqlException excecao)
    {
        var mensagem = excecao.Message;
        return excecao.Number switch
        {
            51001 => new("RN04", mensagem, TipoErro.Conflito),
            51002 => new("RN05", mensagem, TipoErro.Conflito),
            51003 => new("RN01", mensagem, TipoErro.Conflito),
            51004 => new("RN07", mensagem, TipoErro.Conflito),
            51005 => new("STATUS_INVALIDO", mensagem, TipoErro.Conflito),
            51006 => new("RN06", mensagem, TipoErro.EntidadeNaoProcessavel),
            51007 => new("RECEBIMENTO_EXCEDENTE", mensagem, TipoErro.EntidadeNaoProcessavel),
            51008 => new("NAO_ENCONTRADO", mensagem, TipoErro.NaoEncontrado),
            51009 => new("MOVIMENTO_IMUTAVEL", mensagem, TipoErro.Conflito),
            51010 => new("PARAMETRO_INVALIDO", mensagem, TipoErro.EntidadeNaoProcessavel),
            2601 or 2627 => new("REGISTRO_DUPLICADO", "Já existe um registro com estes dados.", TipoErro.Conflito),
            547 => new("VIOLACAO_DE_INTEGRIDADE", "A operação viola uma regra de integridade dos dados.", TipoErro.Conflito),
            _ => null,
        };
    }
}
