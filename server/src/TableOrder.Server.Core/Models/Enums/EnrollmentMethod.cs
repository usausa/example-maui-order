namespace TableOrder.Server.Core.Models.Enums;

// 端末の登録の受け口 (PairingCode は管理画面で出す短いコード、EnrollmentToken は EMM で配る長い値)
public enum EnrollmentMethod
{
    PairingCode,
    EnrollmentToken
}
