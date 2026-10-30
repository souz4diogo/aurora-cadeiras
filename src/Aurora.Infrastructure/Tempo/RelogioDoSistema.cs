using Aurora.Application.Abstracoes;

namespace Aurora.Infrastructure.Tempo;

public sealed class RelogioDoSistema : IRelogio
{
    public DateTime AgoraUtc => DateTime.UtcNow;
}
