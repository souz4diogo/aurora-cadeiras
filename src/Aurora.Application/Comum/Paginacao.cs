namespace Aurora.Application.Comum;

public static class Paginacao
{
    public const int TamanhoMaximo = 100;

    /// <summary>Teto da página: com 100 por página, o deslocamento máximo (≈100 milhões) cabe num int.</summary>
    public const int PaginaMaxima = 1_000_000;

    /// <summary>Ajusta a página (1 a <see cref="PaginaMaxima"/>) e o tamanho (1 a <see cref="TamanhoMaximo"/>) pedidos pelo cliente.</summary>
    public static (int Pagina, int Tamanho) Normalizar(int pagina, int tamanho) =>
        (Math.Clamp(pagina, 1, PaginaMaxima), Math.Clamp(tamanho, 1, TamanhoMaximo));
}
