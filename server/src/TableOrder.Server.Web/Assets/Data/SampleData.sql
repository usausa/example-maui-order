-- サンプルのデータ (開発の環境とテスト。テナントが 1 つもないときだけ DatabaseService が入れる)
-- @now は入れた時刻、@menuVersion と @menuContent は Menu.json のメニュー
-- ID は表の番号を入れた固定値 (4 つ目の区切り)。メニューの品と持ち場の ID は Menu.json と同じ
-- 2 つのテナントに同じ店舗コード (001) の店舗を置き、テナントで分けられていることを確かめられるようにする
-- テナントは別のチェーン (名前・ロゴ・色) にし、店舗の設定 (言語、支払方法、機能、スタッフの PIN) も変えて、登録し直すだけで替わることを確かめる
-- 来店の開き方はデモがスタッフ (既定)、検証用が席 (テーブル端末の待受で人数を入れて始める)
-- スタッフの PIN はデモが 1234、検証用が 5678 (ハッシュは PBKDF2-HMAC-SHA256、100000 回)
-- 管理画面の利用者のパスワードはどれも tableorder-dev (ハッシュは ASP.NET Core Identity の形式。PBKDF2-HMAC-SHA512、100000 回)

INSERT INTO
    Tenants (Id, Code, Name, BrandName, LogoImageName, Theme, Status, SuspendedAt, ClosedAt, CreatedAt, UpdatedAt, Version)
VALUES
    ('00000000-0000-0000-0001-000000000001', 'demo', 'デモ', '{"ja":"バニーズ","en":"Bunny''s"}', 'logo-bunnys.96e5d680.png', NULL, 'Active', NULL, NULL, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000002', 'test', '検証用', '{"ja":"あおぞら食堂","en":"Aozora Diner"}', 'logo-aozora.ce7bb2a8.png', '[{"role":"PrimaryColor","color":"#1E5FA8"},{"role":"PrimaryPressedColor","color":"#164A84"},{"role":"OnPrimaryColor","color":"#FFFFFF"},{"role":"PrimaryContainerColor","color":"#DCE8F7"},{"role":"OnPrimaryContainerColor","color":"#123A66"},{"role":"SecondaryColor","color":"#0F2742"},{"role":"SecondaryPressedColor","color":"#22405F"},{"role":"OnSecondaryColor","color":"#FFFFFF"},{"role":"CanvasColor","color":"#EEF2F6"},{"role":"SurfaceVariantColor","color":"#E1E7EE"},{"role":"OnSurfaceColor","color":"#1A2430"},{"role":"OnSurfaceVariantColor","color":"#5A6675"},{"role":"OutlineColor","color":"#C9D2DC"},{"role":"OutlineVariantColor","color":"#DEE4EA"},{"role":"DisabledColor","color":"#E2E6EB"},{"role":"OnDisabledColor","color":"#98A2AE"}]', 'Active', NULL, NULL, @now, @now, 1);

INSERT INTO
    Stores (TenantId, Id, Code, Name, TimeZone, OpenTime, CloseTime, LastOrderTime, OrderingPaused, PausedMessage, TaxRounding, MaxQuantityPerLine, MaxLinesPerOrder, Languages, PaymentMethods, ElectronicReceipt, Features, StaffPinHash, SettingsVersion, MenuPublicationId, IsActive, CreatedAt, UpdatedAt, Version)
VALUES
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', '001', '{"ja":"駅前店","en":"Ekimae"}', 'Asia/Tokyo', '05:00', '04:00', NULL, 0, NULL, 'Floor', 9, 20, '["ja","en"]', '["QrCode","CreditCard"]', 1, '{}', '{"iterations":100000,"salt":"Wh88not9ak8uHAuajXxuXw==","hash":"+U/ovdh7Hsbwgp7uF330Ww+bi7ps1aHHx4xKWA2a7nI="}', 1, '00000000-0000-0000-0009-000000000001', 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000002', '00000000-0000-0000-0002-000000000002', '001', '{"ja":"本店","en":"Main"}', 'Asia/Tokyo', '05:00', '04:00', NULL, 0, NULL, 'Floor', 9, 20, '["ja"]', '["QrCode"]', 1, '{"splitPayment":false,"lastOrderNoticeMinutes":15,"finishSeconds":20,"visitOpening":"Table"}', '{"iterations":100000,"salt":"D56NfGtaSTgnFqW0w9Lh8A==","hash":"VOeq8IqoQmFrTDrR5Rjzp79khLLbEz2W4OleYGV8o/M="}', 1, '00000000-0000-0000-0009-000000000002', 1, @now, @now, 1);

INSERT INTO
    CallReasons (TenantId, StoreId, Code, Name, SortOrder, IsActive)
VALUES
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', 'Staff', '{"ja":"店員を呼ぶ","en":"Call staff"}', 1, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', 'Water', '{"ja":"お水","en":"Water"}', 2, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', 'Plates', '{"ja":"取り皿","en":"Small plates"}', 3, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', 'Cutlery', '{"ja":"スプーン・フォーク","en":"Spoon & fork"}', 4, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', 'KidsTableware', '{"ja":"子ども用の食器","en":"Kids tableware"}', 5, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', 'Clear', '{"ja":"お皿を下げる","en":"Clear the table"}', 6, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', 'Payment', '{"ja":"お会計の相談","en":"Help with payment"}', 7, 1),
    ('00000000-0000-0000-0001-000000000002', '00000000-0000-0000-0002-000000000002', 'Staff', '{"ja":"店員を呼ぶ","en":"Call staff"}', 1, 1),
    ('00000000-0000-0000-0001-000000000002', '00000000-0000-0000-0002-000000000002', 'Water', '{"ja":"お水","en":"Water"}', 2, 1);

INSERT INTO
    DiningTables (TenantId, Id, StoreId, Name, Area, Capacity, SortOrder, IsActive, CreatedAt, UpdatedAt, Version)
VALUES
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000101', '00000000-0000-0000-0002-000000000001', '1', NULL, 4, 1, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000102', '00000000-0000-0000-0002-000000000001', '2', NULL, 4, 2, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000103', '00000000-0000-0000-0002-000000000001', '3', NULL, 4, 3, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000104', '00000000-0000-0000-0002-000000000001', '4', NULL, 4, 4, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000105', '00000000-0000-0000-0002-000000000001', '5', NULL, 4, 5, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000106', '00000000-0000-0000-0002-000000000001', '6', NULL, 4, 6, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000107', '00000000-0000-0000-0002-000000000001', '7', NULL, 4, 7, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000108', '00000000-0000-0000-0002-000000000001', '8', NULL, 4, 8, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000109', '00000000-0000-0000-0002-000000000001', '9', NULL, 6, 9, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000110', '00000000-0000-0000-0002-000000000001', '10', NULL, 6, 10, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000111', '00000000-0000-0000-0002-000000000001', '11', NULL, 6, 11, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0004-000000000112', '00000000-0000-0000-0002-000000000001', '12', NULL, 6, 12, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000002', '00000000-0000-0000-0004-000000000201', '00000000-0000-0000-0002-000000000002', '1', NULL, 4, 1, 1, @now, @now, 1),
    ('00000000-0000-0000-0001-000000000002', '00000000-0000-0000-0004-000000000202', '00000000-0000-0000-0002-000000000002', '2', NULL, 4, 2, 1, @now, @now, 1);

-- 管理画面の利用者 (運営者、デモの管理者、デモの駅前店の担当、検証用の管理者)
INSERT INTO
    AdminUsers (Id, TenantId, Role, Email, NormalizedEmail, Name, PasswordHash, MustChangePassword, SecurityStamp, AccessFailedCount, LockoutEnd, TwoFactorEnabled, AuthenticatorKey, RecoveryCodes, LastSignInAt, IsActive, CreatedAt, UpdatedAt, Version)
VALUES
    ('00000000-0000-0000-000a-000000000001', NULL, 'Operator', 'operator@example.com', 'OPERATOR@EXAMPLE.COM', '運営者', 'AQAAAAIAAYagAAAAEFUZ9G5DSRmc5FDzR7xsq19vaKk8rloDerq0s1WwK+31y5ob6Mc31wcztL3nJ4GV5A==', 0, '90CB62F1964710C209BFB664DA8F57F0', 0, NULL, 0, NULL, NULL, NULL, 1, @now, @now, 1),
    ('00000000-0000-0000-000a-000000000002', '00000000-0000-0000-0001-000000000001', 'TenantAdmin', 'admin@demo.example.com', 'ADMIN@DEMO.EXAMPLE.COM', 'デモの管理者', 'AQAAAAIAAYagAAAAEMU6/lbHyMjfCIJgFT6bHU0k9qzamTPqKjfX+cEKcpKMofHQYbqKhDModxUVduPtRw==', 0, '0097B5B3DADD5486AB23F6EFB6EC866A', 0, NULL, 0, NULL, NULL, NULL, 1, @now, @now, 1),
    ('00000000-0000-0000-000a-000000000003', '00000000-0000-0000-0001-000000000001', 'StoreStaff', 'staff@demo.example.com', 'STAFF@DEMO.EXAMPLE.COM', '駅前店の担当', 'AQAAAAIAAYagAAAAEOiIrAJqa0Gw3UUfrH1BehP5YpUnaSVbG9otTEUNLbk8XEuJdoP4pHqEJ47nyX/4Pw==', 0, '524A3EF53C55B12E1E29023B5DFE3E88', 0, NULL, 0, NULL, NULL, NULL, 1, @now, @now, 1),
    ('00000000-0000-0000-000a-000000000004', '00000000-0000-0000-0001-000000000002', 'TenantAdmin', 'admin@test.example.com', 'ADMIN@TEST.EXAMPLE.COM', '検証用の管理者', 'AQAAAAIAAYagAAAAEGKZrrCAHkZqjnGApTIQxaG6tvv6bsjzn2s/AYVyXTAutKk463G/T7irvDswU7CDHQ==', 0, '57A017DF88CF94D1B9307153917867F8', 0, NULL, 0, NULL, NULL, NULL, 1, @now, @now, 1);

INSERT INTO
    AdminUserStores (TenantId, UserId, StoreId)
VALUES
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-000a-000000000003', '00000000-0000-0000-0002-000000000001');

-- 開発で使うペアリングコード (期限を遠くにし、何台でも登録できる。本番のコードは 10 分、1 台)
INSERT INTO
    DeviceEnrollments (TenantId, Id, StoreId, Kind, Method, PairingCode, TokenHash, TableId, StationIds, MaxUses, UsedCount, ExpiresAt, CreatedAt, RevokedAt)
VALUES
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0007-000000000001', '00000000-0000-0000-0002-000000000001', 'Table', 'PairingCode', '100001', NULL, '00000000-0000-0000-0004-000000000101', NULL, 1000, 0, '9999-12-31 00:00:00.0000000', @now, NULL),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0007-000000000002', '00000000-0000-0000-0002-000000000001', 'Table', 'PairingCode', '100002', NULL, '00000000-0000-0000-0004-000000000102', NULL, 1000, 0, '9999-12-31 00:00:00.0000000', @now, NULL),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0007-000000000003', '00000000-0000-0000-0002-000000000001', 'Hall', 'PairingCode', '100101', NULL, NULL, NULL, 1000, 0, '9999-12-31 00:00:00.0000000', @now, NULL),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0007-000000000004', '00000000-0000-0000-0002-000000000001', 'Kitchen', 'PairingCode', '100201', NULL, NULL, '["0000005b-0000-0000-0000-000000000000","0000005c-0000-0000-0000-000000000000","0000005d-0000-0000-0000-000000000000"]', 1000, 0, '9999-12-31 00:00:00.0000000', @now, NULL),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0007-000000000005', '00000000-0000-0000-0002-000000000001', 'Reception', 'PairingCode', '100301', NULL, NULL, NULL, 1000, 0, '9999-12-31 00:00:00.0000000', @now, NULL),
    ('00000000-0000-0000-0001-000000000002', '00000000-0000-0000-0007-000000000006', '00000000-0000-0000-0002-000000000002', 'Table', 'PairingCode', '200001', NULL, '00000000-0000-0000-0004-000000000201', NULL, 1000, 0, '9999-12-31 00:00:00.0000000', @now, NULL);

INSERT INTO
    MenuPublications (TenantId, Id, StoreId, MenuVersion, Content, PublishedAt, ApiClientId)
VALUES
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0009-000000000001', '00000000-0000-0000-0002-000000000001', @menuVersion, @menuContent, @now, NULL),
    ('00000000-0000-0000-0001-000000000002', '00000000-0000-0000-0009-000000000002', '00000000-0000-0000-0002-000000000002', @menuVersion, @menuContent, @now, NULL);

-- パンケーキは売り切れ、サーロインステーキは残り 3 点 (駅前店だけ)
INSERT INTO
    Stocks (TenantId, StoreId, TargetId, TargetKind, Status, Remaining, UpdatedAt)
VALUES
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', '00000fa2-0000-0000-0000-000000000000', 'Item', 'SoldOut', NULL, @now),
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', '000003ec-0000-0000-0000-000000000000', 'Item', 'Limited', 3, @now);

INSERT INTO
    EventSequences (TenantId, StoreId, LastSeq)
VALUES
    ('00000000-0000-0000-0001-000000000001', '00000000-0000-0000-0002-000000000001', 0),
    ('00000000-0000-0000-0001-000000000002', '00000000-0000-0000-0002-000000000002', 0);
