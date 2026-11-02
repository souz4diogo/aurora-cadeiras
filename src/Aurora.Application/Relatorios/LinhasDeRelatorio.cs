using Aurora.Domain.Enums;

namespace Aurora.Application.Relatorios;

// Linhas de leitura que as consultas dos relatórios devolvem; os geradores as transformam em tabelas.

public sealed record LinhaDePedido(
    string Numero,
    string Cliente,
    string Vendedor,
    DateTime DataPedido,
    DateOnly PrazoEntrega,
    StatusPedido Status,
    int Itens,
    decimal ValorTotal);

public sealed record LinhaDeProducao(
    string NumeroOp,
    string Produto,
    int Quantidade,
    string EstacaoAtual,
    string StatusDaEtapa,
    int? MinutosNaEtapa,
    DateOnly? PrevisaoConclusao,
    bool Atrasada);

public sealed record LinhaDeEstoqueDeMateria(
    string Codigo,
    string Nome,
    string Unidade,
    decimal Saldo,
    decimal Minimo,
    decimal CustoUnitario)
{
    public bool Critico => Saldo <= Minimo;

    public decimal ValorEmEstoque => Saldo * CustoUnitario;
}

public sealed record LinhaDeEstoqueDeProduto(string Codigo, string Nome, int Quantidade, int Reservado)
{
    public int Disponivel => Quantidade - Reservado;
}

public sealed record LinhaDeConsumoPorProduto(string Codigo, string Nome, decimal CustoEstimado);

public sealed record LinhaDeComprasComunitarias(int Ano, int Mes, decimal ValorComunidades, decimal ValorTotal, decimal Percentual);
