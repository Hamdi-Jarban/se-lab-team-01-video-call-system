namespace VideoCall.Shared.Messages;

public enum MessageType
{
    // === Issue #1: ÇáãÕÇÏŞÉ æÊÓÌíá ÇáÏÎæá (ÇáÎÇÕÉ Èß) ===
    LoginRequest,
    LoginResponse,

    // === ÇáÃÎØÇÁ æÇáÑÓÇÆá ÇáÚÇãÉ ááäÙÇã (ÇáÎÇÕÉ Èß) ===
    Error,
    Disconnect,
    // Issue #3: ÅÑÓÇá ØáÈ ãßÇáãÉ ÎÇÕÉ ÚÈÑ ŞäÇÉ TCP.
    CallRequest,
    // Issue #3: ÅÔÚÇÑ ÇáÚãíá ÈÇäÊåÇÁ ãÏÉ ÇäÊÙÇÑ ÇáÑÏ.
    CallTimedOut,

    // Issue #3: ÅÔÚÇÑ ÇáÚãíá ÈİÔá ØáÈ ÇáãßÇáãÉ.
    CallError
}