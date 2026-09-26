namespace VideoCall.Server.Api.Dtos;

/// <summary>
/// äãÇĞÌ ÇáÇÓÊÌÇÈÉ ÇáÎÇÕÉ ÈæÇÌåÉ HTTP ááŞÑÇÁÉ İŞØ.
/// ÊÓÊÎÏã áÊÍæíá ÈíÇäÇÊ ÇáÎÇÏã Åáì JSON ãÚ İÕáåÇ Úä äãÇĞÌ Domain æÈÑæÊæßæá TCP.
/// </summary>
public sealed record ServerStatusResponse(string Status, int ConnectedClients, string Uptime);

/// <summary>
/// íÚÑÖ ŞÇÆãÉ ÇáãÓÊÎÏãíä ÇáãÊÕáíä æÚÏÏåã.
/// </summary>
public sealed record OnlineUsersResponse(IReadOnlyList<string> OnlineUsers, int Count);

/// <summary>
/// íãËá ãáÎÕ ÛÑİÉ æÃÚÖÇÁåÇ.
/// </summary>
public sealed record RoomSummary(string Name, IReadOnlyList<string> Members);

/// <summary>
/// íãËá ŞÇÆãÉ ÇáÛÑİ ÇáÍÇáíÉ.
/// </summary>
public sealed record RoomsResponse(IReadOnlyList<RoomSummary> Rooms);

/// <summary>
/// íãËá ÌáÓÉ æÓÇÆØ äÔØÉ æÇáãÓÊÎÏãíä ÇáãÔÇÑßíä İíåÇ.
/// </summary>
public sealed record ActiveSessionSummary(string SessionId, IReadOnlyList<string> Participants);

/// <summary>
/// íãËá ŞÇÆãÉ ÌáÓÇÊ ÇáæÓÇÆØ ÇáäÔØÉ.
/// </summary>
public sealed record SessionsResponse(IReadOnlyList<ActiveSessionSummary> ActiveCalls);
