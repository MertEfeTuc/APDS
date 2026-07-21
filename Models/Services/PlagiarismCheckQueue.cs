using System.Threading.Channels;

namespace APDS.Services.PlagiarismCheck
{
    public class PlagiarismCheckJob
    {
        public int PlagiarismCheckId { get; set; }
        public int ActivityId { get; set; }
    }

    public class PlagiarismCheckQueue
    {
        private readonly Channel<PlagiarismCheckJob> _channel =
            Channel.CreateUnbounded<PlagiarismCheckJob>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        public ValueTask EnqueueAsync(PlagiarismCheckJob job, CancellationToken ct = default) =>
            _channel.Writer.WriteAsync(job, ct);

        public IAsyncEnumerable<PlagiarismCheckJob> ReadAllAsync(CancellationToken ct) =>
            _channel.Reader.ReadAllAsync(ct);
    }
}