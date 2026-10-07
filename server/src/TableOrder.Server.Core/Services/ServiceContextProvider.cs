namespace TableOrder.Server.Core.Services;

// 入口 (API、管理画面) が始めた文脈を、業務の処理が読む
public abstract class ServiceContextProvider
{
    public abstract ServiceContext Current { get; }
}
