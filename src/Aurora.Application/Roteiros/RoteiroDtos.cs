namespace Aurora.Application.Roteiros;

public sealed record RoteiroEtapaDto(byte Sequencia, byte EstacaoId, string Estacao, bool GeraEtapa);

public sealed record RoteiroDto(int ProdutoId, IReadOnlyList<RoteiroEtapaDto> Etapas);

/// <summary>Corpo do PUT: ids das estações na ordem em que o produto as percorre (a sequência é a posição na lista).</summary>
public sealed record RoteiroRequest(IReadOnlyList<byte> Estacoes);
