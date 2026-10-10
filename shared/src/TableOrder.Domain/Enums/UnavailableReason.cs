namespace TableOrder.Domain.Enums;

// 出せる条件のルール (Availability) を満たさない理由。端末は品の印と知らせ、サーバは断る理由に使う
public enum UnavailableReason
{
    // 満たす (出せる)
    None,
    // 時間帯のどれの中でもない
    Daypart,
    // 子どもを求めるのに、子どもがいない
    Children
}
