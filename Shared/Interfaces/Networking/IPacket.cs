using System.IO;

namespace Shared.Interfaces.Networking
{	public interface IPacket
	{
		void Serialize(BinaryWriter writer);
		void Deserialize(BinaryReader reader);

		void OnDispatched();

	}
}
