namespace VideoCall.Server.Domain;

/// <summary>
/// ãÓÄæá Úä ÊäİíĞ ÚãáíÇÊ ÊäÙíİ ÌáÓÉ ÇáÚãíá ÈÚÏ ÅÛáÇŞ ÇÊÕÇá TCP¡
/// ãËá ÅÒÇáÉ ÇáãÓÊÎÏã ãä ŞÇÆãÉ ÇáãÊÕáíä æÊÍÏíË ÇáãÍÇÏËÇÊ ÇáãÑÊÈØÉ Èå.
/// </summary>
public interface IConnectionLifecycleHandler
{
    /// <summary>
    /// íÚÇáÌ ÅÛáÇŞ ÌáÓÉ ÇáÚãíá æíäİĞ ÚãáíÇÊ ÇáÊäÙíİ ÇáãØáæÈÉ.
    /// </summary>
    /// <param name="session">ÌáÓÉ ÇáÚãíá ÇáÊí ÃõÛáŞÊ.</param>
    /// <param name="ct">ÑãÒ ÅáÛÇÁ ÇáÚãáíÉ.</param>
    Task HandleDisconnectAsync(IClientHandler session,CancellationToken ct);
}
