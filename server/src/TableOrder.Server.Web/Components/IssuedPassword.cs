namespace TableOrder.Server.Web.Components;

// 出した仮のパスワード (出した画面を離れるまで見せる)
public sealed record IssuedPassword(string Email, string Password);
