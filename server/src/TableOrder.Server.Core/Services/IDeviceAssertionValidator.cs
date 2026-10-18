namespace TableOrder.Server.Core.Services;

// 端末の鍵で署名した使い捨ての JWT (トークンの要求) を確かめる。持つのは入口の側 (署名の確かめと、使い捨ての値の記憶)
public interface IDeviceAssertionValidator
{
    // 署名を確かめる前に、端末を引くための Id を読む。形の崩れた値は null
    Guid? ReadDeviceId(string assertion);

    ValueTask<bool> ValidateAsync(string assertion, DeviceEntity device);
}
