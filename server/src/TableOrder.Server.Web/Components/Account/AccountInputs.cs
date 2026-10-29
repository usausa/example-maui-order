namespace TableOrder.Server.Web.Components.Account;

// サインインとアカウントの画面のフォームの値

public sealed class SignInInput
{
    [Required(ErrorMessage = "メールアドレスを入力してください")]
    [EmailAddress(ErrorMessage = "メールアドレスの形式で入力してください")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "パスワードを入力してください")]
    public string Password { get; set; } = string.Empty;
}

public sealed class CodeInput
{
    [Required(ErrorMessage = "コードを入力してください")]
    [StringLength(64, ErrorMessage = "コードが長すぎます")]
    public string Code { get; set; } = string.Empty;
}

public sealed class PasswordInput
{
    [Required(ErrorMessage = "現在のパスワードを入力してください")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "新しいパスワードを入力してください")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "新しいパスワードは 12 文字以上にしてください")]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "確認用のパスワードが一致しません")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// 押したボタンを見分けるだけのフォーム (多要素を外す、回復用のコードを出し直す)
public sealed class ActionInput
{
    public string Action { get; set; } = string.Empty;
}
