-- スキーマ (起動のたびに DatabaseService が実行する。CREATE ... IF NOT EXISTS なので何度実行してもよい)
-- Tenants と AdminUsers (運営者はテナントに属さない) のほかのすべての表は TenantId を持ち、主キー・一意・索引・外部キーの先頭に置く
-- テナントのわからない要求で引く列と、テナントをまたぐ裏の処理の索引だけ TenantId を付けない

PRAGMA journal_mode = WAL;

CREATE TABLE IF NOT EXISTS Tenants (
    Id             TEXT     NOT NULL,
    Code           TEXT     NOT NULL,
    Name           TEXT     NOT NULL,
    BrandName      TEXT     NOT NULL,
    LogoImageName  TEXT,
    Theme          TEXT,
    Status         TEXT     NOT NULL,
    SuspendedAt    TEXT,
    ClosedAt       TEXT,
    CreatedAt      TEXT     NOT NULL,
    UpdatedAt      TEXT     NOT NULL,
    Version        INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code)
);

-- 店舗と公開したメニューは互いに指すので、今のメニューへの外部キーはコミットのときに確かめる
CREATE TABLE IF NOT EXISTS Stores (
    TenantId            TEXT     NOT NULL,
    Id                  TEXT     NOT NULL,
    Code                TEXT     NOT NULL,
    Name                TEXT     NOT NULL,
    TimeZone            TEXT     NOT NULL,
    OpenTime            TEXT     NOT NULL,
    CloseTime           TEXT     NOT NULL,
    LastOrderTime       TEXT,
    OrderingPaused      INTEGER  NOT NULL,
    PausedMessage       TEXT,
    TaxRounding         TEXT     NOT NULL,
    MaxQuantityPerLine  INTEGER  NOT NULL,
    MaxLinesPerOrder    INTEGER  NOT NULL,
    Languages           TEXT     NOT NULL,
    PaymentMethods      TEXT     NOT NULL,
    ElectronicReceipt   INTEGER  NOT NULL,
    Features            TEXT     NOT NULL,
    StaffPinHash        TEXT     NOT NULL,
    SettingsVersion     INTEGER  NOT NULL,
    MenuPublicationId   TEXT,
    IsActive            INTEGER  NOT NULL,
    CreatedAt           TEXT     NOT NULL,
    UpdatedAt           TEXT     NOT NULL,
    Version             INTEGER  NOT NULL,
    PRIMARY KEY (TenantId, Id),
    UNIQUE (TenantId, Code),
    FOREIGN KEY (TenantId) REFERENCES Tenants (Id),
    FOREIGN KEY (TenantId, MenuPublicationId) REFERENCES MenuPublications (TenantId, Id) DEFERRABLE INITIALLY DEFERRED
);

CREATE TABLE IF NOT EXISTS CallReasons (
    TenantId   TEXT     NOT NULL,
    StoreId    TEXT     NOT NULL,
    Code       TEXT     NOT NULL,
    Name       TEXT     NOT NULL,
    SortOrder  INTEGER  NOT NULL,
    IsActive   INTEGER  NOT NULL,
    PRIMARY KEY (TenantId, StoreId, Code),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id)
);

CREATE TABLE IF NOT EXISTS DiningTables (
    TenantId   TEXT     NOT NULL,
    Id         TEXT     NOT NULL,
    StoreId    TEXT     NOT NULL,
    Name       TEXT     NOT NULL,
    Area       TEXT,
    Capacity   INTEGER  NOT NULL,
    SortOrder  INTEGER  NOT NULL,
    IsActive   INTEGER  NOT NULL,
    CreatedAt  TEXT     NOT NULL,
    UpdatedAt  TEXT     NOT NULL,
    Version    INTEGER  NOT NULL,
    PRIMARY KEY (TenantId, Id),
    UNIQUE (TenantId, StoreId, Name),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id)
);

-- 管理画面の利用者。運営者はテナントに属さないので、Tenants と同じくテナントの外に置く (TenantId は運営者のとき NULL)
-- サインインはテナントのわからないまま、すべてのテナントで一意のメールアドレスで引く
CREATE TABLE IF NOT EXISTS AdminUsers (
    Id                  TEXT     NOT NULL,
    TenantId            TEXT,
    Role                TEXT     NOT NULL,
    Email               TEXT     NOT NULL,
    NormalizedEmail     TEXT     NOT NULL,
    Name                TEXT     NOT NULL,
    PasswordHash        TEXT     NOT NULL,
    MustChangePassword  INTEGER  NOT NULL,
    SecurityStamp       TEXT     NOT NULL,
    AccessFailedCount   INTEGER  NOT NULL,
    LockoutEnd          TEXT,
    TwoFactorEnabled    INTEGER  NOT NULL,
    AuthenticatorKey    TEXT,
    RecoveryCodes       TEXT,
    LastSignInAt        TEXT,
    IsActive            INTEGER  NOT NULL,
    CreatedAt           TEXT     NOT NULL,
    UpdatedAt           TEXT     NOT NULL,
    Version             INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (NormalizedEmail),
    UNIQUE (TenantId, Id),
    FOREIGN KEY (TenantId) REFERENCES Tenants (Id)
);

-- 店舗の担当が受け持つ店舗
CREATE TABLE IF NOT EXISTS AdminUserStores (
    TenantId  TEXT  NOT NULL,
    UserId    TEXT  NOT NULL,
    StoreId   TEXT  NOT NULL,
    PRIMARY KEY (TenantId, UserId, StoreId),
    FOREIGN KEY (TenantId, UserId) REFERENCES AdminUsers (TenantId, Id),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id)
);

CREATE TABLE IF NOT EXISTS Devices (
    TenantId      TEXT     NOT NULL,
    Id            TEXT     NOT NULL,
    StoreId       TEXT     NOT NULL,
    Kind          TEXT     NOT NULL,
    Name          TEXT     NOT NULL,
    TableId       TEXT,
    PublicKey     TEXT     NOT NULL,
    IsActive      INTEGER  NOT NULL,
    RegisteredAt  TEXT     NOT NULL,
    RevokedAt     TEXT,
    CreatedAt     TEXT     NOT NULL,
    UpdatedAt     TEXT     NOT NULL,
    Version       INTEGER  NOT NULL,
    PRIMARY KEY (TenantId, Id),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id),
    FOREIGN KEY (TenantId, TableId) REFERENCES DiningTables (TenantId, Id)
);
-- トークンの要求で、テナントのわからないまま端末を引く
CREATE UNIQUE INDEX IF NOT EXISTS UX_Devices_Id ON Devices (Id);
-- 登録で、テナントのわからないまま同じ鍵の端末を引く (登録し直した端末の前の登録を無効にする)
CREATE INDEX IF NOT EXISTS IX_Devices_PublicKey ON Devices (PublicKey);
CREATE INDEX IF NOT EXISTS IX_Devices_StoreId ON Devices (TenantId, StoreId);
-- すぐに拒む一覧で、テナントをまたいで近ごろ無効にした端末を引く
CREATE INDEX IF NOT EXISTS IX_Devices_RevokedAt ON Devices (RevokedAt) WHERE RevokedAt IS NOT NULL;

CREATE TABLE IF NOT EXISTS DeviceStations (
    TenantId   TEXT  NOT NULL,
    DeviceId   TEXT  NOT NULL,
    StationId  TEXT  NOT NULL,
    PRIMARY KEY (TenantId, DeviceId, StationId),
    FOREIGN KEY (TenantId, DeviceId) REFERENCES Devices (TenantId, Id)
);

CREATE TABLE IF NOT EXISTS DeviceStatuses (
    TenantId      TEXT     NOT NULL,
    DeviceId      TEXT     NOT NULL,
    AppVersion    TEXT,
    BatteryLevel  NUMERIC,
    IsCharging    INTEGER,
    LastSeenAt    TEXT     NOT NULL,
    PRIMARY KEY (TenantId, DeviceId),
    FOREIGN KEY (TenantId, DeviceId) REFERENCES Devices (TenantId, Id)
);

CREATE TABLE IF NOT EXISTS DeviceEnrollments (
    TenantId     TEXT     NOT NULL,
    Id           TEXT     NOT NULL,
    StoreId      TEXT     NOT NULL,
    Kind         TEXT     NOT NULL,
    Method       TEXT     NOT NULL,
    PairingCode  TEXT,
    TokenHash    BLOB,
    TableId      TEXT,
    StationIds   TEXT,
    MaxUses      INTEGER  NOT NULL,
    UsedCount    INTEGER  NOT NULL,
    ExpiresAt    TEXT     NOT NULL,
    CreatedAt    TEXT     NOT NULL,
    RevokedAt    TEXT,
    PRIMARY KEY (TenantId, Id),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id),
    FOREIGN KEY (TenantId, TableId) REFERENCES DiningTables (TenantId, Id)
);
-- 端末の登録で、テナントのわからないままコードとトークンで引く
CREATE UNIQUE INDEX IF NOT EXISTS UX_DeviceEnrollments_PairingCode ON DeviceEnrollments (PairingCode) WHERE PairingCode IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS UX_DeviceEnrollments_TokenHash ON DeviceEnrollments (TokenHash) WHERE TokenHash IS NOT NULL;

CREATE TABLE IF NOT EXISTS ApiClients (
    TenantId    TEXT     NOT NULL,
    Id          TEXT     NOT NULL,
    Name        TEXT     NOT NULL,
    StoreId     TEXT,
    SecretHash  BLOB     NOT NULL,
    Scopes      TEXT     NOT NULL,
    IsActive    INTEGER  NOT NULL,
    CreatedAt   TEXT     NOT NULL,
    RevokedAt   TEXT,
    PRIMARY KEY (TenantId, Id),
    FOREIGN KEY (TenantId) REFERENCES Tenants (Id),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id)
);
-- client credentials で、テナントのわからないままクライアントを引く
CREATE UNIQUE INDEX IF NOT EXISTS UX_ApiClients_Id ON ApiClients (Id);

CREATE TABLE IF NOT EXISTS WebhookEndpoints (
    TenantId    TEXT     NOT NULL,
    Id          TEXT     NOT NULL,
    Name        TEXT     NOT NULL,
    Url         TEXT     NOT NULL,
    Secret      TEXT     NOT NULL,
    EventTypes  TEXT     NOT NULL,
    StoreId     TEXT,
    IsActive    INTEGER  NOT NULL,
    CreatedAt   TEXT     NOT NULL,
    UpdatedAt   TEXT     NOT NULL,
    Version     INTEGER  NOT NULL,
    PRIMARY KEY (TenantId, Id),
    FOREIGN KEY (TenantId) REFERENCES Tenants (Id),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id)
);

CREATE TABLE IF NOT EXISTS WebhookDeliveries (
    TenantId        TEXT     NOT NULL,
    Id              TEXT     NOT NULL,
    EndpointId      TEXT     NOT NULL,
    StoreId         TEXT     NOT NULL,
    Seq             INTEGER  NOT NULL,
    Payload         TEXT     NOT NULL,
    Status          TEXT     NOT NULL,
    Attempts        INTEGER  NOT NULL,
    NextAttemptAt   TEXT,
    LastStatusCode  INTEGER,
    LastError       TEXT,
    CreatedAt       TEXT     NOT NULL,
    DeliveredAt     TEXT,
    PRIMARY KEY (TenantId, Id),
    UNIQUE (TenantId, EndpointId, StoreId, Seq),
    FOREIGN KEY (TenantId, EndpointId) REFERENCES WebhookEndpoints (TenantId, Id)
);
-- テナントをまたいで送り直す
CREATE INDEX IF NOT EXISTS IX_WebhookDeliveries_NextAttemptAt ON WebhookDeliveries (NextAttemptAt) WHERE Status = 'Pending';

CREATE TABLE IF NOT EXISTS MenuPublications (
    TenantId     TEXT  NOT NULL,
    Id           TEXT  NOT NULL,
    StoreId      TEXT  NOT NULL,
    MenuVersion  TEXT  NOT NULL,
    Content      TEXT  NOT NULL,
    PublishedAt  TEXT  NOT NULL,
    ApiClientId  TEXT,
    PRIMARY KEY (TenantId, Id),
    UNIQUE (TenantId, StoreId, MenuVersion),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id)
);

CREATE TABLE IF NOT EXISTS Stocks (
    TenantId    TEXT     NOT NULL,
    StoreId     TEXT     NOT NULL,
    TargetId    TEXT     NOT NULL,
    TargetKind  TEXT     NOT NULL,
    Status      TEXT     NOT NULL,
    Remaining   INTEGER,
    UpdatedAt   TEXT     NOT NULL,
    PRIMARY KEY (TenantId, StoreId, TargetId),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id)
);

CREATE TABLE IF NOT EXISTS Visits (
    TenantId        TEXT     NOT NULL,
    Id              TEXT     NOT NULL,
    StoreId         TEXT     NOT NULL,
    TableId         TEXT     NOT NULL,
    BusinessDate    TEXT     NOT NULL,
    Adults          INTEGER  NOT NULL,
    Children        INTEGER  NOT NULL,
    Status          TEXT     NOT NULL,
    OpenedBy        TEXT     NOT NULL,
    OpenedDeviceId  TEXT,
    OpenedAt        TEXT     NOT NULL,
    ClosedBy        TEXT,
    ClosedAt        TEXT,
    ClosedStaffId   TEXT,
    CreatedAt       TEXT     NOT NULL,
    UpdatedAt       TEXT     NOT NULL,
    Version         INTEGER  NOT NULL,
    PRIMARY KEY (TenantId, Id),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id),
    FOREIGN KEY (TenantId, TableId) REFERENCES DiningTables (TenantId, Id)
);
-- 1 つのテーブルに開いている来店を 2 つ作らない
CREATE UNIQUE INDEX IF NOT EXISTS UX_Visits_TableId ON Visits (TenantId, TableId) WHERE Status IN ('Open', 'Paying');
CREATE INDEX IF NOT EXISTS IX_Visits_BusinessDate ON Visits (TenantId, StoreId, BusinessDate);

CREATE TABLE IF NOT EXISTS VisitConfirmations (
    TenantId     TEXT  NOT NULL,
    VisitId      TEXT  NOT NULL,
    RuleId       TEXT  NOT NULL,
    DeviceId     TEXT,
    ConfirmedAt  TEXT  NOT NULL,
    PRIMARY KEY (TenantId, VisitId, RuleId),
    FOREIGN KEY (TenantId, VisitId) REFERENCES Visits (TenantId, Id)
);

CREATE TABLE IF NOT EXISTS Orders (
    TenantId     TEXT     NOT NULL,
    Id           TEXT     NOT NULL,
    StoreId      TEXT     NOT NULL,
    VisitId      TEXT     NOT NULL,
    OrderNo      INTEGER  NOT NULL,
    Source       TEXT     NOT NULL,
    DeviceId     TEXT,
    MenuVersion  TEXT     NOT NULL,
    RequestHash  BLOB     NOT NULL,
    OrderedAt    TEXT     NOT NULL,
    PRIMARY KEY (TenantId, Id),
    UNIQUE (TenantId, VisitId, OrderNo),
    FOREIGN KEY (TenantId, VisitId) REFERENCES Visits (TenantId, Id)
);

CREATE TABLE IF NOT EXISTS KitchenTickets (
    TenantId   TEXT  NOT NULL,
    Id         TEXT  NOT NULL,
    StoreId    TEXT  NOT NULL,
    StationId  TEXT  NOT NULL,
    OrderId    TEXT  NOT NULL,
    VisitId    TEXT  NOT NULL,
    Status     TEXT  NOT NULL,
    CreatedAt  TEXT  NOT NULL,
    DoneAt     TEXT,
    PRIMARY KEY (TenantId, Id),
    FOREIGN KEY (TenantId, OrderId) REFERENCES Orders (TenantId, Id),
    FOREIGN KEY (TenantId, VisitId) REFERENCES Visits (TenantId, Id)
);
CREATE INDEX IF NOT EXISTS IX_KitchenTickets_StationId ON KitchenTickets (TenantId, StationId, Status, CreatedAt);

CREATE TABLE IF NOT EXISTS OrderLines (
    TenantId         TEXT     NOT NULL,
    Id               TEXT     NOT NULL,
    StoreId          TEXT     NOT NULL,
    OrderId          TEXT     NOT NULL,
    VisitId          TEXT     NOT NULL,
    LineNo           INTEGER  NOT NULL,
    ItemId           TEXT     NOT NULL,
    ItemCode         TEXT     NOT NULL,
    Name             TEXT     NOT NULL,
    Tags             TEXT     NOT NULL,
    Quantity         INTEGER  NOT NULL,
    UnitPrice        NUMERIC  NOT NULL,
    Amount           NUMERIC  NOT NULL,
    TaxRate          NUMERIC  NOT NULL,
    Timing           TEXT     NOT NULL,
    Status           TEXT     NOT NULL,
    StationId        TEXT,
    ServedBy         TEXT     NOT NULL,
    TicketId         TEXT,
    ReleasedAt       TEXT,
    StartedAt        TEXT,
    ReadyAt          TEXT,
    ServedAt         TEXT,
    ServedStaffId    TEXT,
    CancelledAt      TEXT,
    CancelReason     TEXT,
    CancelStaffId    TEXT,
    SplitFromLineId  TEXT,
    PRIMARY KEY (TenantId, Id),
    FOREIGN KEY (TenantId, OrderId) REFERENCES Orders (TenantId, Id),
    FOREIGN KEY (TenantId, VisitId) REFERENCES Visits (TenantId, Id),
    FOREIGN KEY (TenantId, TicketId) REFERENCES KitchenTickets (TenantId, Id)
);
CREATE INDEX IF NOT EXISTS IX_OrderLines_OrderId ON OrderLines (TenantId, OrderId);
CREATE INDEX IF NOT EXISTS IX_OrderLines_VisitId ON OrderLines (TenantId, VisitId);
CREATE INDEX IF NOT EXISTS IX_OrderLines_TicketId ON OrderLines (TenantId, TicketId);
-- 提供を待つ明細
CREATE INDEX IF NOT EXISTS IX_OrderLines_Ready ON OrderLines (TenantId, StoreId) WHERE Status = 'Ready';

CREATE TABLE IF NOT EXISTS OrderLineOptions (
    TenantId       TEXT     NOT NULL,
    LineId         TEXT     NOT NULL,
    SortOrder      INTEGER  NOT NULL,
    OptionGroupId  TEXT     NOT NULL,
    OptionId       TEXT     NOT NULL,
    Name           TEXT     NOT NULL,
    PriceDelta     NUMERIC  NOT NULL,
    PRIMARY KEY (TenantId, LineId, OptionId),
    FOREIGN KEY (TenantId, LineId) REFERENCES OrderLines (TenantId, Id)
);

CREATE TABLE IF NOT EXISTS Calls (
    TenantId        TEXT  NOT NULL,
    Id              TEXT  NOT NULL,
    StoreId         TEXT  NOT NULL,
    VisitId         TEXT  NOT NULL,
    ReasonCode      TEXT  NOT NULL,
    Status          TEXT  NOT NULL,
    DeviceId        TEXT,
    CreatedAt       TEXT  NOT NULL,
    AcknowledgedAt  TEXT,
    DoneAt          TEXT,
    PRIMARY KEY (TenantId, Id),
    FOREIGN KEY (TenantId, VisitId) REFERENCES Visits (TenantId, Id)
);
-- 同じ用件の終わっていない呼び出しを増やさない
CREATE UNIQUE INDEX IF NOT EXISTS UX_Calls_ReasonCode ON Calls (TenantId, VisitId, ReasonCode) WHERE Status <> 'Done';
CREATE INDEX IF NOT EXISTS IX_Calls_StoreId ON Calls (TenantId, StoreId) WHERE Status <> 'Done';

CREATE TABLE IF NOT EXISTS Payments (
    TenantId           TEXT     NOT NULL,
    Id                 TEXT     NOT NULL,
    StoreId            TEXT     NOT NULL,
    VisitId            TEXT     NOT NULL,
    Method             TEXT     NOT NULL,
    Amount             NUMERIC  NOT NULL,
    Status             TEXT     NOT NULL,
    QrCode             TEXT,
    ExpiresAt          TEXT,
    Provider           TEXT,
    ProviderReference  TEXT,
    FailureReason      TEXT,
    DeviceId           TEXT,
    CreatedAt          TEXT     NOT NULL,
    UpdatedAt          TEXT     NOT NULL,
    CompletedAt        TEXT,
    PRIMARY KEY (TenantId, Id),
    FOREIGN KEY (TenantId, VisitId) REFERENCES Visits (TenantId, Id)
);
-- 決済サービスの通知で、テナントのわからないまま支払を引く
CREATE UNIQUE INDEX IF NOT EXISTS UX_Payments_ProviderReference ON Payments (Provider, ProviderReference) WHERE ProviderReference IS NOT NULL;
CREATE INDEX IF NOT EXISTS IX_Payments_VisitId ON Payments (TenantId, VisitId);

CREATE TABLE IF NOT EXISTS Receipts (
    TenantId  TEXT  NOT NULL,
    VisitId   TEXT  NOT NULL,
    Token     TEXT  NOT NULL,
    IssuedAt  TEXT  NOT NULL,
    PRIMARY KEY (TenantId, VisitId),
    FOREIGN KEY (TenantId, VisitId) REFERENCES Visits (TenantId, Id)
);
-- 電子レシートの画面で、テナントのわからないまま引く
CREATE UNIQUE INDEX IF NOT EXISTS UX_Receipts_Token ON Receipts (Token);

CREATE TABLE IF NOT EXISTS EventSequences (
    TenantId  TEXT     NOT NULL,
    StoreId   TEXT     NOT NULL,
    LastSeq   INTEGER  NOT NULL,
    PRIMARY KEY (TenantId, StoreId),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id)
);

-- 送る先は種類と、通知が持つテーブル (JSON の配列。NULL は店舗のすべて) と持ち場 (NULL はすべて) で決める
CREATE TABLE IF NOT EXISTS Events (
    TenantId    TEXT     NOT NULL,
    StoreId     TEXT     NOT NULL,
    Seq         INTEGER  NOT NULL,
    Type        TEXT     NOT NULL,
    OccurredAt  TEXT     NOT NULL,
    Data        TEXT     NOT NULL,
    TableIds    TEXT,
    StationId   TEXT,
    PRIMARY KEY (TenantId, StoreId, Seq),
    FOREIGN KEY (TenantId, StoreId) REFERENCES Stores (TenantId, Id)
);
-- テナントをまたいで古いものを消す
CREATE INDEX IF NOT EXISTS IX_Events_OccurredAt ON Events (OccurredAt);
