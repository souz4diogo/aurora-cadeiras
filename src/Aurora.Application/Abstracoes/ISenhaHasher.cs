namespace Aurora.Application.Abstracoes;

public interface ISenhaHasher
{
    string Hash(string senha);

    bool Verificar(string hash, string senha);
}
