namespace TableOrder.Server.Web.Application.Account;

using System.Security.Cryptography;

// 管理者が出す仮のパスワード。読み違えやすい文字 (0、O、1、l、I など) を除いた 16 文字を、伝えやすいように 4 文字ごとに区切る
public static class TemporaryPassword
{
    private const string Alphabet = "abcdefghijkmnpqrstuvwxyzACDEFGHJKLMNPQRTUVWXY2345679";

    private const int GroupLength = 4;

    private const int GroupCount = 4;

    public static string Create()
    {
        var chars = new char[(GroupLength * GroupCount) + GroupCount - 1];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = (i % (GroupLength + 1)) == GroupLength ? '-' : Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
