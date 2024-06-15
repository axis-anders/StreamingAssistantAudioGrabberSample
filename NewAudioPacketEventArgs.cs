using System;

namespace StreamingAssistantAudioGrabberSample
{
	public class NewAudioPacketEventArgs : EventArgs
	{
		public NewAudioPacketEventArgs(double sampleTime, IntPtr buffer, int bufferLength)
		{
			SampleTime = sampleTime;
			Buffer = buffer;
			BufferLength = bufferLength;
		}

		public double SampleTime { get; }

		public IntPtr Buffer { get; }

		public int BufferLength { get; }
	}
}