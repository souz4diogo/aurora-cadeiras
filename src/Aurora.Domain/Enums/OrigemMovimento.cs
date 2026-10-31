namespace Aurora.Domain.Enums;

/// <summary>O que causou o movimento; os nomes são os valores gravados em MovimentoEstoque.Origem.</summary>
public enum OrigemMovimento
{
    Compra,
    Producao,
    ProducaoConcluida,
    Expedicao,
    Ajuste,
}
