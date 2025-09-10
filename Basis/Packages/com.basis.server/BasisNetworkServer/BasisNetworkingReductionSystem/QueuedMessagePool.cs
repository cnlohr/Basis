using System.Collections.Concurrent;
namespace BasisNetworkServer.BasisNetworkingReductionSystem
{
    public class QueuedMessagePool
    {
        private static ConcurrentBag<QueuedMessage> pool = new();

        public static QueuedMessage Rent()
        {
            return pool.TryTake(out var msg) ? msg : new QueuedMessage();
        }

        public static void Return(QueuedMessage msg)
        {
            msg.FromPeer = null;
            pool.Add(msg);
        }
    }
}
