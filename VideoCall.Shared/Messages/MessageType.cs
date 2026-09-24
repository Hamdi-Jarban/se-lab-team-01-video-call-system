namespace VideoCall.Shared.Messages;

public enum MessageType
{
    // Issue #3: TCP
    CallRequest,

    // Issue #3: .
    CallTimedOut,

    // Issue #3: .
    CallError,

    // Issue #2: Display list of online users
    OnlineUsersUpdate,

    // Issue #5: Toggle camera on/off
    StopConversationMedia,
    StartConversationMedia,

    // Issue #7: End an ongoing call
    CallEnded
    // === Issue #1: �������� ������ ������ (������ ��) ===
    LoginRequest,
    LoginResponse,

    // === ������� �������� ������ ������ (������ ��) ===
    Error,
    Disconnect,
    // Issue #3: ����� ��� ������ ���� ��� ���� TCP.
    CallRequest,
    // Issue #3: ����� ������ ������� ��� ������ ����.
    CallTimedOut,

    // Issue #3: ����� ������ ���� ��� ��������.
    CallError
}