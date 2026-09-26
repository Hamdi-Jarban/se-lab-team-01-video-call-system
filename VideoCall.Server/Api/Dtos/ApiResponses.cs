namespace VideoCall.Server.Api.Dtos;

/// <summary>
/// نماذج الاستجابة الخاصة بواجهة HTTP للقراءة فقط.
/// تستخدم لتحويل بيانات الخادم إلى JSON مع فصلها عن نماذج Domain وبروتوكول TCP.
/// </summary>
public sealed record ServerStatusResponse(string Status, int ConnectedClients, string Uptime);

/// <summary>
/// يعرض قائمة المستخدمين المتصلين وعددهم.
/// </summary>
public sealed record OnlineUsersResponse(IReadOnlyList<string> OnlineUsers, int Count);

/// <summary>
/// يمثل ملخص غرفة وأعضاءها.
/// </summary>
public sealed record RoomSummary(string Name, IReadOnlyList<string> Members);

/// <summary>
/// يمثل قائمة الغرف الحالية.
/// </summary>
public sealed record RoomsResponse(IReadOnlyList<RoomSummary> Rooms);

/// <summary>
/// يمثل جلسة وسائط نشطة والمستخدمين المشاركين فيها.
/// </summary>
public sealed record ActiveSessionSummary(string SessionId, IReadOnlyList<string> Participants);

/// <summary>
/// يمثل قائمة جلسات الوسائط النشطة.
/// </summary>
public sealed record SessionsResponse(IReadOnlyList<ActiveSessionSummary> ActiveCalls);
