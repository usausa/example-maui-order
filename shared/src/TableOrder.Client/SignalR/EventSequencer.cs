namespace TableOrder.Client.SignalR;

using TableOrder.Contract.Events;

// 抜けた通知の読み方 (つなぎ直したあとの ready で作る。After より後を読む)
internal sealed record CatchUpRequest(long Connection, long Id, long After, long ReadySeq);

// 通知の数え方 (SignalROrderEvents が使う)。接続ごとに ready の番号から数え始め、seq の順に重ならずに渡す
// つなぎ直すとき (切れた、閉じたので始め直す) から、ready を受けて抜けた通知を読み終えるまでは、届いた通知をためる
// (ハブはグループに入れてから番号を読んで ready を送るので、ready より先に届いた通知で番号を進めると、抜けた通知を読み飛ばす)
// 接続を作り直したら数え直し、前の接続の通知と読み込みは使わない。つなぎ直しが重なったら、最後に始めた読み込みだけを使う
internal sealed class EventSequencer
{
    private readonly Lock sync = new();

    // 渡す通知 (接続と通知。null は追いかけられなくなった知らせ)。ロックの中で呼ぶので、待たずに返す
    private readonly Action<long, EventListResponseItem?> emit;

    // 数え始めの位置を決める前と、つなぎ直しの途中に届いた通知
    private readonly List<EventListResponseItem> buffered = [];

    private long connection;

    // 数え始めの位置を決めた (今の接続で ready を受けた)
    private bool counting;

    // つなぎ直しの途中 (ready を受けて抜けた通知を読み終えるまで)
    private bool catchingUp;

    // 最後に始めた抜けた通知の読み込み
    private long catchUp;

    // 渡した (読み飛ばしたものも含めて見た) 最後の通し番号
    private long lastSeq;

    public EventSequencer(Action<long, EventListResponseItem?> emit)
    {
        this.emit = emit;
    }

    // 今の接続
    public long Connection
    {
        get
        {
            lock (sync)
            {
                return connection;
            }
        }
    }

    // 接続を作り直す。前の接続の通知と読み込みは使わない
    public long Begin()
    {
        lock (sync)
        {
            connection++;
            counting = false;
            catchingUp = false;
            buffered.Clear();
            lastSeq = 0;
            return connection;
        }
    }

    // 接続が切れた (つなぎ直す)。ready を受けて抜けた通知を読み終えるまで、届いた通知をためる
    public void Interrupt(long id)
    {
        lock (sync)
        {
            if ((id == connection) && counting)
            {
                catchingUp = true;
            }
        }
    }

    // ready を受けた。はじめての ready なら数え始めて null を、つなぎ直したあとなら抜けた通知の読み方を返す
    public CatchUpRequest? Ready(long id, long seq)
    {
        lock (sync)
        {
            if (id != connection)
            {
                return null;
            }

            if (!counting)
            {
                counting = true;
                lastSeq = seq;
                Flush();
                return null;
            }

            catchingUp = true;
            catchUp++;
            return new CatchUpRequest(id, catchUp, lastSeq, seq);
        }
    }

    public void Receive(long id, EventListResponseItem item)
    {
        lock (sync)
        {
            if (id != connection)
            {
                return;
            }

            if (!counting || catchingUp)
            {
                buffered.Add(item);
                return;
            }

            Enqueue(item);
        }
    }

    // 抜けた通知を読み終えた。ためた通知と合わせて seq の順に渡す
    // 読めなかったら (content が null) 今の番号から数え直し、追いかけられなくなったことを知らせる (端末は今の状態を読み直す)
    public void Complete(CatchUpRequest request, EventListResponse? content)
    {
        lock (sync)
        {
            if ((request.Connection != connection) || (request.Id != catchUp))
            {
                return;
            }

            if (content is not null)
            {
                buffered.AddRange(content.Items);
                Flush();
                lastSeq = Math.Max(lastSeq, content.LastSeq);
            }
            else
            {
                buffered.Clear();
                lastSeq = Math.Max(lastSeq, request.ReadySeq);
                emit(connection, null);
            }

            catchingUp = false;
        }
    }

    // ためた通知を seq の順に渡す (ロックの中で呼ぶ)
    private void Flush()
    {
        foreach (var item in buffered.OrderBy(static x => x.Seq))
        {
            Enqueue(item);
        }

        buffered.Clear();
    }

    // 見た通し番号より後の通知だけを渡す (ロックの中で呼ぶ)
    private void Enqueue(EventListResponseItem item)
    {
        if (item.Seq <= lastSeq)
        {
            return;
        }

        lastSeq = item.Seq;
        emit(connection, item);
    }
}
