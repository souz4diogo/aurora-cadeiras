namespace Aurora.Api.Extensions;

internal static partial class LogMensagens
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível semear o administrador inicial (banco indisponível?).")]
    public static partial void FalhaAoSemearAdministrador(ILogger logger, Exception ex);
}
