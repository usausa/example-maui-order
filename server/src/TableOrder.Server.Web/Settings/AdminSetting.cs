namespace TableOrder.Server.Web.Settings;

public sealed class AdminSetting
{
    // 初めの運営者 (運営者がひとりもいなければ、起動のときにこのメールアドレスと仮のパスワードで作る。秘密の値で渡す)
    public string? InitialOperatorEmail { get; set; }

    public string? InitialOperatorPassword { get; set; }
}
