using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamingAssistantAudioGrabberSample
{
	class MainWindowModelView : INotifyPropertyChanged
	{
		private readonly AudioGrabberService audioGrabberService = new AudioGrabberService();

		private bool onlyAxisDevices;
		private List<AudioSource> audioSources;
		private AudioSource selectedAudioSources;
		private string status = "Not connected";

		private TimeSpan sampleTime;
		private int packetCount;
		private double audioLevelPercent;

		private CancellationTokenSource cts;

		public MainWindowModelView()
		{
			audioGrabberService.NewAudioPacket += OnNewAudioPacket;
			Init();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public bool OnlyAxisDevices
		{
			get => onlyAxisDevices;
			set
			{
				if (onlyAxisDevices != value)
				{
					onlyAxisDevices = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OnlyAxisDevices)));
					Init();
				}
			}
		}

		public List<AudioSource> AudioSources
		{
			get => audioSources;
			set
			{
				audioSources = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AudioSources)));
			}
		}

		public AudioSource SelectedAudioSource
		{
			get => selectedAudioSources;
			set
			{
				if (selectedAudioSources != value)
				{
					cts?.Cancel();
					packetCount = 0;

					cts = new CancellationTokenSource();

					selectedAudioSources = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedAudioSource)));

					try
					{
						if (selectedAudioSources != null)
						{
							audioGrabberService.Run(selectedAudioSources, cts.Token);
							Status = "Streaming";
						}
						else
						{
							Status = "Not connected";
						}
					}
					catch (Exception ex)
					{
						Status = $"Error: {ex.Message.Replace("\r\n", " ")}";
					}
				}
			}
		}

		public string Status
		{
			get => status;
			set
			{
				status = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
			}
		}

		public TimeSpan SampleTime
		{
			get => sampleTime;
			set
			{
				sampleTime = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SampleTime)));
			}
		}

		public int PacketCount
		{
			get => packetCount;
			set
			{
				packetCount = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PacketCount)));
			}
		}

		public double AudioLevelPercent
		{
			get => audioLevelPercent;
			set
			{
				audioLevelPercent = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AudioLevelPercent)));
			}
		}

		private void Init()
		{
			AudioSources = audioGrabberService.GetAllSources(OnlyAxisDevices);
		}

		private void OnNewAudioPacket(object sender, NewAudioPacketEventArgs e)
		{
			PacketCount++;
			SampleTime = TimeSpan.FromSeconds(e.SampleTime);
			AudioLevelPercent = Math.Round(100 * CalculateRMSLevel(GetAudioData(e)), 0);
		}

		private short[] GetAudioData(NewAudioPacketEventArgs e)
		{
			short[] destination = new short[e.BufferLength/2];
			Marshal.Copy(e.Buffer, destination, 0, destination.Length);
			return destination;
		}

		public double CalculateRMSLevel(short[] audioData)
		{
			double sumOfSquares = 0.0;
			foreach (var sample in audioData)
			{
				double sampleValue = sample / (double)short.MaxValue;
				sumOfSquares += sampleValue * sampleValue;
			}

			double meanSquare = sumOfSquares / audioData.Length;
			double rmsLevel = Math.Sqrt(meanSquare);

			return rmsLevel;
		}
	}
}
