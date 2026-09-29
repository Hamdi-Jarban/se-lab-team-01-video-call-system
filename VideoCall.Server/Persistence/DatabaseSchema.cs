namespace VideoCall.Server.Persistence;

// مخطط قاعدة البيانات. كل عبارة idempotent (آمنة عند التشغيل أكثر من مرة).
// سياسة الحذف: لا يوجد حذف فعلي للمستخدمين/الرسائل (IsActive/IsDeleted)، لذلك العلاقات مع
// Users و Messages من نوع NO ACTION لحماية السجل. أعضاء المحادثة يُحذفون تبعًا لمحادثتهم (CASCADE).
internal static class DatabaseSchema
{
    public static readonly string[] Statements =
    {
        // ---------- Users ----------
        @"IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
CREATE TABLE dbo.Users (
    UserId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Username     NVARCHAR(64)  COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    PasswordHash NVARCHAR(256) NOT NULL,
    DisplayName  NVARCHAR(100) NOT NULL,
    CreatedAt    DATETIME2(3)  NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    LastLoginAt  DATETIME2(3)  NULL,
    IsActive     BIT           NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT CK_Users_Username CHECK (LEN(LTRIM(RTRIM(Username))) >= 1)
)",

        // ---------- UserSessions ----------
        @"IF OBJECT_ID(N'dbo.UserSessions', N'U') IS NULL
CREATE TABLE dbo.UserSessions (
    SessionId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserSessions PRIMARY KEY,
    UserId         INT              NOT NULL,
    ConnectedAt    DATETIME2(3)     NOT NULL CONSTRAINT DF_UserSessions_ConnectedAt DEFAULT SYSUTCDATETIME(),
    DisconnectedAt DATETIME2(3)     NULL,
    IsActive       BIT              NOT NULL CONSTRAINT DF_UserSessions_IsActive DEFAULT 1,
    CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE NO ACTION
)",

        // ---------- Conversations ----------
        // Type: 0 = Private, 1 = Group. PrivateKey = ""minUserId:maxUserId"" لمنع تكرار المحادثة الخاصة.
        @"IF OBJECT_ID(N'dbo.Conversations', N'U') IS NULL
CREATE TABLE dbo.Conversations (
    ConversationId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Conversations PRIMARY KEY,
    Type            TINYINT       NOT NULL,
    Name            NVARCHAR(100) NULL,
    CreatedByUserId INT           NOT NULL,
    CreatedAt       DATETIME2(3)  NOT NULL CONSTRAINT DF_Conversations_CreatedAt DEFAULT SYSUTCDATETIME(),
    IsActive        BIT           NOT NULL CONSTRAINT DF_Conversations_IsActive DEFAULT 1,
    PrivateKey      NVARCHAR(40)  NULL,
    CONSTRAINT CK_Conversations_Type CHECK (Type IN (0, 1)),
    CONSTRAINT CK_Conversations_PrivateKey CHECK ((Type = 0 AND PrivateKey IS NOT NULL) OR (Type = 1 AND PrivateKey IS NULL)),
    CONSTRAINT FK_Conversations_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId) ON DELETE NO ACTION
)",

        // ---------- ConversationMembers ----------
        @"IF OBJECT_ID(N'dbo.ConversationMembers', N'U') IS NULL
CREATE TABLE dbo.ConversationMembers (
    ConversationId INT          NOT NULL,
    UserId         INT          NOT NULL,
    JoinedAt       DATETIME2(3) NOT NULL CONSTRAINT DF_ConversationMembers_JoinedAt DEFAULT SYSUTCDATETIME(),
    LeftAt         DATETIME2(3) NULL,
    IsActive       BIT          NOT NULL CONSTRAINT DF_ConversationMembers_IsActive DEFAULT 1,
    CONSTRAINT PK_ConversationMembers PRIMARY KEY (ConversationId, UserId),
    CONSTRAINT FK_ConversationMembers_Conversations FOREIGN KEY (ConversationId) REFERENCES dbo.Conversations(ConversationId) ON DELETE CASCADE,
    CONSTRAINT FK_ConversationMembers_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE NO ACTION
)",

        // ---------- Messages ----------
        @"IF OBJECT_ID(N'dbo.Messages', N'U') IS NULL
CREATE TABLE dbo.Messages (
    MessageId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Messages PRIMARY KEY,
    ConversationId INT           NOT NULL,
    SenderId       INT           NOT NULL,
    Content        NVARCHAR(4000) NOT NULL,
    SentAt         DATETIME2(3)  NOT NULL CONSTRAINT DF_Messages_SentAt DEFAULT SYSUTCDATETIME(),
    EditedAt       DATETIME2(3)  NULL,
    IsDeleted      BIT           NOT NULL CONSTRAINT DF_Messages_IsDeleted DEFAULT 0,
    CONSTRAINT CK_Messages_Content CHECK (LEN(LTRIM(RTRIM(Content))) > 0),
    CONSTRAINT FK_Messages_Conversations FOREIGN KEY (ConversationId) REFERENCES dbo.Conversations(ConversationId) ON DELETE NO ACTION,
    CONSTRAINT FK_Messages_Users FOREIGN KEY (SenderId) REFERENCES dbo.Users(UserId) ON DELETE NO ACTION
)",

        // ---------- Calls ----------
        // CallType: 0 = Private, 1 = Room. Status: 0 Ringing, 1 Connected, 2 Rejected, 3 Ended, 4 TimedOut, 5 Interrupted
        @"IF OBJECT_ID(N'dbo.Calls', N'U') IS NULL
CREATE TABLE dbo.Calls (
    CallId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Calls PRIMARY KEY,
    ConversationId  INT              NULL,
    RoomId          NVARCHAR(100)    NULL,
    StartedByUserId INT              NOT NULL,
    StartedAt       DATETIME2(3)     NOT NULL CONSTRAINT DF_Calls_StartedAt DEFAULT SYSUTCDATETIME(),
    EndedAt         DATETIME2(3)     NULL,
    CallType        TINYINT          NOT NULL,
    Status          TINYINT          NOT NULL,
    CONSTRAINT CK_Calls_CallType CHECK (CallType IN (0, 1)),
    CONSTRAINT CK_Calls_Status CHECK (Status BETWEEN 0 AND 5),
    CONSTRAINT FK_Calls_Users FOREIGN KEY (StartedByUserId) REFERENCES dbo.Users(UserId) ON DELETE NO ACTION,
    CONSTRAINT FK_Calls_Conversations FOREIGN KEY (ConversationId) REFERENCES dbo.Conversations(ConversationId) ON DELETE NO ACTION
)",

        // ---------- Indexes ----------
        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Conversations_PrivateKey' AND object_id = OBJECT_ID(N'dbo.Conversations'))
CREATE UNIQUE INDEX UX_Conversations_PrivateKey ON dbo.Conversations(PrivateKey) WHERE PrivateKey IS NOT NULL",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserSessions_UserId_IsActive' AND object_id = OBJECT_ID(N'dbo.UserSessions'))
CREATE INDEX IX_UserSessions_UserId_IsActive ON dbo.UserSessions(UserId, IsActive)",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ConversationMembers_UserId_IsActive' AND object_id = OBJECT_ID(N'dbo.ConversationMembers'))
CREATE INDEX IX_ConversationMembers_UserId_IsActive ON dbo.ConversationMembers(UserId, IsActive) INCLUDE (ConversationId)",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Messages_ConversationId_MessageId' AND object_id = OBJECT_ID(N'dbo.Messages'))
CREATE INDEX IX_Messages_ConversationId_MessageId ON dbo.Messages(ConversationId, MessageId DESC)",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Messages_SenderId' AND object_id = OBJECT_ID(N'dbo.Messages'))
CREATE INDEX IX_Messages_SenderId ON dbo.Messages(SenderId)",

        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Calls_StartedByUserId' AND object_id = OBJECT_ID(N'dbo.Calls'))
CREATE INDEX IX_Calls_StartedByUserId ON dbo.Calls(StartedByUserId, StartedAt DESC)"
    };
}
