namespace Aurora.Application.Comum;

public static class Paginacao
{
    public const int TamanhoMaximo = 100;

    /// <summary>Ajusta a página (mínimo 1) e o tamanho (1 a <see cref="TamanhoMaximo"/>) pedidos pelo cliente.</summary>
    public static (int Pagina, int Tamanho) Normalizar(int pagina, int tamanho) =>
        (Math.Max(pagina, 1), Math.Clamp(tamanho, 1, TamanhoMaximo));
}
