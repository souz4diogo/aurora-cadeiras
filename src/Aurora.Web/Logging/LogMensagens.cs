namespace Aurora.Web.Logging;

internal static partial class LogMensagens
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao chamar a API")]
    public static partial void FalhaAoChamarApi(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível revogar o refresh token na API ao sair")]
    public static partial void FalhaAoRevogarSessao(ILogger logger, Exception ex);
}
