using Aurora.Application.Autenticacao;

namespace Aurora.Application.Tests;

public class PoliticaDeSenhaTests
{
    [Theory]
    [InlineData("1234567890")] // exatamente 10
    [InlineData("Senha-Admin-Teste-1")]
    public void Aceita_senha_com_pelo_menos_10_caracteres(string senha) =>
        Assert.True(PoliticaDeSenha.Atende(senha));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("admin")]
    [InlineData("123456789")] // 9
    [InlineData("          ")] // 10 espaços não é senha
    public void Recusa_senha_curta_vazia_ou_so_de_espacos(string? senha) =>
        Assert.False(PoliticaDeSenha.Atende(senha));

    [Fact]
    public void Recusa_senha_gigante_que_serviria_para_sobrecarregar_o_hash()
    {
        Assert.True(PoliticaDeSenha.Atende(new string('a', PoliticaDeSenha.TamanhoMaximo)));
        Assert.False(PoliticaDeSenha.Atende(new string('a', PoliticaDeSenha.TamanhoMaximo + 1)));
    }

    [Fact]
    public void A_mensagem_cita_os_limites() =>
        Assert.Equal("A senha deve ter de 10 a 200 caracteres.", PoliticaDeSenha.Descricao);
}
