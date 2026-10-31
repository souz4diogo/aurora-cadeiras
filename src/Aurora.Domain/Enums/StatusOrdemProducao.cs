namespace Aurora.Domain.Enums;

/// <summary>Situação da ordem de produção; os nomes são os valores gravados em OrdemProducao.Status.</summary>
public enum StatusOrdemProducao
{
    Planejada,
    EmAndamento,
    Concluida,
    Cancelada,
}
