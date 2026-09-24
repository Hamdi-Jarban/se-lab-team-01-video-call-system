namespace VideoCall.Shared.Messages;

public enum MessageType
{
    // Issue #3: ÅÑÓÇá ØáÈ ãßÇáãÉ ÎÇÕÉ ÚÈÑ ŞäÇÉ TCP.
    CallRequest,

    // Issue #3: ÅÔÚÇÑ ÇáÚãíá ÈÇäÊåÇÁ ãÏÉ ÇäÊÙÇÑ ÇáÑÏ.
    CallTimedOut,

    // Issue #3: ÅÔÚÇÑ ÇáÚãíá ÈİÔá ØáÈ ÇáãßÇáãÉ.
    CallError
}
