namespace TableOrder.Client.SignalR;

using TableOrder.Contract.Events;

public sealed class EventSequencerTests
{
    // 渡した通知 (接続と seq。null は追いかけられなくなった知らせ)
    private readonly List<(long Connection, long? Seq)> emitted = [];

    private readonly EventSequencer sequencer;

    public EventSequencerTests()
    {
        sequencer = new EventSequencer((connection, item) => emitted.Add((connection, item?.Seq)));
    }

    // はじめての ready の番号から数え、ready の前に届いた通知はその番号より後のものだけを渡す
    [Fact]
    public void FirstReadyStartsFromReadySeq()
    {
        // Arrange
        var id = sequencer.Begin();
        sequencer.Receive(id, Item(4));
        sequencer.Receive(id, Item(6));

        // Act
        var request = sequencer.Ready(id, 5);
        sequencer.Receive(id, Item(7));

        // Assert
        Assert.Null(request);
        (long, long?)[] expected = [(id, 6), (id, 7)];
        Assert.Equal(expected, emitted);
    }

    // つなぎ直したときは、ready より前に届いた通知もためておき、抜けた通知と合わせて seq の順に渡す (抜けた通知を読み飛ばさない)
    [Fact]
    public void ReconnectHoldsEventsUntilCatchUp()
    {
        // Arrange
        var id = sequencer.Begin();
        sequencer.Ready(id, 10);

        // Act / Assert: 切れてつなぎ直す間に届いた通知は渡さない
        sequencer.Interrupt(id);
        sequencer.Receive(id, Item(13));
        var request = sequencer.Ready(id, 13);
        Assert.NotNull(request);
        Assert.Equal(10, request.After);
        Assert.Empty(emitted);

        // Act / Assert: 抜けた通知を読んだら、ためた通知と合わせて渡し、そのあとはすぐに渡す
        sequencer.Complete(request, Content(13, 11, 12, 13));
        sequencer.Receive(id, Item(14));
        (long, long?)[] expected = [(id, 11), (id, 12), (id, 13), (id, 14)];
        Assert.Equal(expected, emitted);
    }

    // つなぎ直しが重なったら、最後に始めた読み込みだけを使う (先の読み込みで、あとに届いた通知まで渡さない)
    [Fact]
    public void OnlyLatestCatchUpIsUsed()
    {
        // Arrange
        var id = sequencer.Begin();
        sequencer.Ready(id, 10);
        sequencer.Interrupt(id);
        var first = sequencer.Ready(id, 15)!;
        sequencer.Interrupt(id);
        sequencer.Receive(id, Item(21));
        var second = sequencer.Ready(id, 21)!;

        // Act / Assert: 先の読み込みは使わない
        sequencer.Complete(first, Content(15, 11, 15));
        Assert.Empty(emitted);

        // Act / Assert: 最後の読み込みで、ためた通知と合わせて渡す
        sequencer.Complete(second, Content(21, 11, 15, 18, 21));
        (long, long?)[] expected = [(id, 11), (id, 15), (id, 18), (id, 21)];
        Assert.Equal(expected, emitted);
    }

    // 抜けた通知を読めなければ、追いかけられなくなったことを知らせ、ready の番号から数え直す
    [Fact]
    public void FailedCatchUpExpires()
    {
        // Arrange
        var id = sequencer.Begin();
        sequencer.Ready(id, 10);
        sequencer.Interrupt(id);
        sequencer.Receive(id, Item(12));
        var request = sequencer.Ready(id, 12)!;

        // Act
        sequencer.Complete(request, null);
        sequencer.Receive(id, Item(12));
        sequencer.Receive(id, Item(13));

        // Assert
        (long, long?)[] expected = [(id, null), (id, 13)];
        Assert.Equal(expected, emitted);
    }

    // 接続を作り直したら、前の接続の通知と読み込みは使わず、新しい接続の ready から数え直す
    [Fact]
    public void NewConnectionIgnoresPreviousConnection()
    {
        // Arrange
        var previous = sequencer.Begin();
        sequencer.Ready(previous, 100);
        sequencer.Interrupt(previous);
        var request = sequencer.Ready(previous, 105)!;

        // Act
        var current = sequencer.Begin();
        sequencer.Receive(previous, Item(106));
        sequencer.Complete(request, Content(105, 101, 105));
        sequencer.Ready(current, 5);
        sequencer.Receive(current, Item(6));

        // Assert
        Assert.Equal(current, sequencer.Connection);
        (long, long?)[] expected = [(current, 6)];
        Assert.Equal(expected, emitted);
    }

    private static EventListResponseItem Item(long seq) =>
        new() { Seq = seq, Type = "test", OccurredAt = DateTimeOffset.UnixEpoch };

    private static EventListResponse Content(long lastSeq, params long[] seqs) =>
        new() { LastSeq = lastSeq, Items = seqs.Select(Item).ToList() };
}
