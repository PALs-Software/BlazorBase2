using System.Text;
using BlazorBase.Components.Assistant;
using Xunit;

namespace BlazorBase.Components.Test.Assistant;

public sealed class ServerSentEventReaderTests
{
    private static async Task<List<ServerSentEvent>> ReadAllAsync(Stream stream)
    {
        var messages = new List<ServerSentEvent>();

        await foreach (var message in ServerSentEventReader.ReadAsync(stream))
            messages.Add(message);

        return messages;
    }

    [Fact]
    public async Task ASingleMessage_HasItsEventNameAndData()
    {
        var messages = await ReadAllAsync(Chunked("event: text_delta\ndata: {\"text\":\"Hi\"}\n\n", 1000));

        var message = Assert.Single(messages);
        Assert.Equal("text_delta", message.Event);
        Assert.Equal("{\"text\":\"Hi\"}", message.Data);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public async Task AStreamDeliveredInTinyChunks_YieldsTheSameMessages(int chunkSize)
    {
        var text = "event: a\ndata: one\n\ndata: two\ndata: three\n\n: keep-alive\n\nevent: b\ndata: ä€😀\n\n";

        var messages = await ReadAllAsync(Chunked(text, chunkSize));

        Assert.Equal(3, messages.Count);
        Assert.Equal(new ServerSentEvent("a", "one"), messages[0]);
        Assert.Equal(new ServerSentEvent(null, "two\nthree"), messages[1]);
        Assert.Equal(new ServerSentEvent("b", "ä€😀"), messages[2]);
    }

    [Fact]
    public async Task MultipleDataLines_AreJoinedWithALineFeed()
    {
        var messages = await ReadAllAsync(Chunked("data: {\ndata:  \"a\": 1\ndata: }\n\n", 5));

        Assert.Equal("{\n \"a\": 1\n}", Assert.Single(messages).Data);
    }

    [Fact]
    public async Task CrLfAndLoneCrLineEndings_AreAccepted()
    {
        var messages = await ReadAllAsync(Chunked("event: x\r\ndata: 1\r\n\r\ndata: 2\r\r", 4));

        Assert.Equal(["1", "2"], messages.Select(message => message.Data));
    }

    [Fact]
    public async Task CommentsAndUnknownFields_AreIgnored()
    {
        var messages = await ReadAllAsync(Chunked(": ping\nid: 5\nretry: 100\ndata: x\n\n", 6));

        Assert.Equal(new ServerSentEvent(null, "x"), Assert.Single(messages));
    }

    [Fact]
    public async Task AMessageTheStreamEndsInTheMiddleOf_IsDropped()
    {
        var messages = await ReadAllAsync(Chunked("data: complete\n\ndata: partial", 4));

        Assert.Equal("complete", Assert.Single(messages).Data);
    }

    [Fact]
    public async Task ABlankLineWithoutData_DispatchesNothing()
    {
        var messages = await ReadAllAsync(Chunked("event: lonely\n\n\n\ndata: x\n\n", 8));

        Assert.Equal(new ServerSentEvent(null, "x"), Assert.Single(messages));
    }

    [Fact]
    public async Task CancellingTheToken_StopsReading()
    {
        using var source = new CancellationTokenSource();
        var stream = new BlockingStream("data: first\n\n");

        var read = Task.Run(async () =>
        {
            await foreach (var message in ServerSentEventReader.ReadAsync(stream, source.Token))
                Assert.Equal("first", message.Data);
        });

        await Task.Delay(50);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
    }

    internal static Stream Chunked(string text, int chunkSize) => new ChunkedStream(Encoding.UTF8.GetBytes(text), chunkSize);

    private sealed class ChunkedStream(byte[] bytes, int chunkSize) : Stream
    {
        private int Position0;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var length = Math.Min(Math.Min(count, chunkSize), bytes.Length - Position0);
            Array.Copy(bytes, Position0, buffer, offset, length);
            Position0 += length;
            return length;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class BlockingStream(string first) : Stream
    {
        private readonly byte[] Bytes = Encoding.UTF8.GetBytes(first);
        private int Position0;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position0 < Bytes.Length)
            {
                var length = Math.Min(buffer.Length, Bytes.Length - Position0);
                Bytes.AsMemory(Position0, length).CopyTo(buffer);
                Position0 += length;
                return length;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
