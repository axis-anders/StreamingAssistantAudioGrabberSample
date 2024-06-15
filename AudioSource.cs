using System.Runtime.InteropServices.ComTypes;

namespace StreamingAssistantAudioGrabberSample
{
	internal class AudioSource
	{
		public AudioSource(string id, string friendlyName, IMoniker moniker)
		{
			Id = id;
			FriendlyName = friendlyName;
			Moniker = moniker;
		}
		public string Id { get; }

		public string FriendlyName { get; }

		public IMoniker Moniker { get; }

		public override string ToString() => FriendlyName;
	}
}