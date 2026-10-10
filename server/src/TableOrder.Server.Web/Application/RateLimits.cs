namespace TableOrder.Server.Web.Application;

using System.Net;
using System.Net.Sockets;

public static class RateLimits
{
    // 端末の登録 (接続元ごと)
    public const string Pairing = nameof(Pairing);

    // 管理画面のサインインの送信 (パスワードと多要素のコード。接続元ごと)
    public const string SignIn = nameof(SignIn);

    // 数える接続元の区分。IPv6 は /64 にする (同じ回線の中でアドレスを替えて試し続けさせない)
    public static string PartitionOf(IPAddress? address)
    {
        if (address is null)
        {
            return string.Empty;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
