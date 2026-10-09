namespace TableOrder.Server.Web.Components.Account;

// サインインとアカウントの画面のフォームの値

public sealed class SignInInput
{
    [Required(ErrorMessage = "メールアドレスを入れてください。")]
    [EmailAddress(ErrorMessage = "メールアドレスの形で入れてください。")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "パスワードを入れてください。")]
    public string Password { get; set; } = string.Empty;
}

public sealed class CodeInput
{
    [Required(ErrorMessage = "コードを入れてください。")]
    [StringLength(64, ErrorMessage = "コードが長すぎます。")]
    public string Code { get; set; } = string.Empty;
}

public sealed class PasswordInput
{
    [Required(ErrorMessage = "今のパスワードを入れてください。")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "新しいパスワードを入れてください。")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "新しいパスワードは 12 文字以上にしてください。")]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "確かめのパスワードが新しいパスワードと違います。")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// 押したボタンを見分けるだけのフォーム (多要素を外す、回復用のコードを出し直す)
public sealed class ActionInput
{
    public string Action { get; set; } = string.Empty;
}
