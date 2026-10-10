namespace TableOrder.Client.Rest;

using System.IO;
using System.Net;
using System.Net.Http;

public sealed class RestConnectionTests
{
    // 通信の部品が投げる例外は、送り直せば通る見込みのある失敗 (Unavailable) にして返し、例外を投げない
    // (Android の通信は、時間切れで閉じた接続などで WebException を投げる)
    [Theory]
    [InlineData(typeof(WebException))]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(IOException))]
    public async Task TransportFailureIsUnavailable(Type exceptionType)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        var options = new OrderServerOptions { HandlerFactory = () => new FailingHandler(exception) };
        using var connection = new RestConnection(new TestDeviceContext(), options, TimeProvider.System);

        // Act
        var result = await connection.PostAnonymousAsync(
            "devices/token",
            new DeviceTokenRequest { Assertion = "assertion" },
            ClientJsonContext.Default.DeviceTokenRequest,
            ClientJsonContext.Default.DeviceTokenResponse,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ApiStatus.Unavailable, result.Status);
        Assert.Same(exception, result.Exception);
    }

    private sealed class TestDeviceContext : IDeviceContext
    {
        public string ApiEndPoint => "http://localhost/";

        public Guid? DeviceId { get; } = Guid.NewGuid();

        public IDeviceKey Key { get; } = new TestDeviceKey();
    }

    // 送るたびに同じ例外を投げる通信のハンドラ
    private sealed class FailingHandler : HttpMessageHandler
    {
        private readonly Exception exception;

        public FailingHandler(Exception exception)
        {
            this.exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
