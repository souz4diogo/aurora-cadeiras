namespace Aurora.Domain.Enums;

/// <summary>Perfis de acesso; os valores são os PerfilId da tabela Perfil (seed).</summary>
public enum PerfilUsuario : byte
{
    Administrador = 1,
    GerenteProducao = 2,
    Operador = 3,
    Vendedor = 4,
    Almoxarife = 5,
    Inspetor = 6,
    Cliente = 7,
}
