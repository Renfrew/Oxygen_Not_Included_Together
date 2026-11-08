
namespace Shared.Interfaces.Networking
{
	public interface IBulkablePacket : IPacket
	{
		int MaxPackSize { get; }
		uint IntervalMs { get; }
	}
}
