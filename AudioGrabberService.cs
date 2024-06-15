using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StreamingAssistantAudioGrabberSample
{
	class AudioGrabberService : ISampleGrabberCB
	{
		private static readonly Regex instanceNameRegEx = new Regex(@".*\\(.*)$");

		public event EventHandler<NewAudioPacketEventArgs> NewAudioPacket;

		public List<AudioSource> GetAllSources(bool onlyAxisChannels)
		{
			List<AudioSource> sourceList = new List<AudioSource>();
			DsDevice[] audioDevices = DsDevice.GetDevicesOfCat(FilterCategory.AudioInputDevice);
			DsDevice[] videoDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
			foreach (var device in audioDevices.Concat(videoDevices))
			{
				if (!onlyAxisChannels || IsAxisCaptureChannel(device))
				{
					object filterObj;
					device.Mon.BindToObject(null, null, typeof(IBaseFilter).GUID, out filterObj);
					IBaseFilter audioDeviceFilter = (IBaseFilter)filterObj;
					if (HasAudioPin(audioDeviceFilter))
					{
						string instanceId = instanceNameRegEx.Match(device.DevicePath)?.Groups[1]?.Value;
						if (!string.IsNullOrEmpty(instanceId) && !sourceList.Any(s => s.Id == instanceId))
						{
							sourceList.Add(new AudioSource(instanceId, device.Name, device.Mon));
						}
					}
				}
			}

			return sourceList;
		}

		public void Run(AudioSource audioSource, CancellationToken ct = default)
		{
			int hr;

			IGraphBuilder graphBuilder = (IGraphBuilder)new FilterGraph();
			ICaptureGraphBuilder2 captureGraphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
			hr = captureGraphBuilder.SetFiltergraph(graphBuilder);
			DsError.ThrowExceptionForHR(hr);

			object filterObj;
			audioSource.Moniker.BindToObject(null, null, typeof(IBaseFilter).GUID, out filterObj);
			IBaseFilter audioDeviceFilter = (IBaseFilter)filterObj;
			hr = graphBuilder.AddFilter(audioDeviceFilter, "Capture Channel");
			DsError.ThrowExceptionForHR(hr);

			ISampleGrabber sampleGrabber = (ISampleGrabber)new SampleGrabber();
			IBaseFilter sampleGrabberFilter = (IBaseFilter)sampleGrabber;

			hr = graphBuilder.AddFilter(sampleGrabberFilter, "SampleGrabber");
			DsError.ThrowExceptionForHR(hr);

			AMMediaType mediaType = new AMMediaType();
			mediaType.majorType = MediaType.Audio;
			mediaType.subType = MediaSubType.PCM;
			mediaType.formatType = FormatType.WaveEx;
			hr = sampleGrabber.SetMediaType(mediaType);
			DsError.ThrowExceptionForHR(hr);

			hr = captureGraphBuilder.RenderStream(PinCategory.Capture, MediaType.Audio, audioDeviceFilter, null, sampleGrabberFilter);
			DsError.ThrowExceptionForHR(hr);

			hr = sampleGrabber.SetCallback(this, 1);
			DsError.ThrowExceptionForHR(hr);

			IMediaControl mediaControl = (IMediaControl)graphBuilder;
			hr = mediaControl.Run();
			DsError.ThrowExceptionForHR(hr);

			ct.Register(() =>
			{
				hr = mediaControl.Stop();
				DsError.ThrowExceptionForHR(hr);

				Marshal.ReleaseComObject(mediaControl);
				Marshal.ReleaseComObject(sampleGrabber);
				Marshal.ReleaseComObject(sampleGrabberFilter);
			});
		}

		private bool IsAxisCaptureChannel(DsDevice device)
		{
			device.Mon.BindToStorage(null, null, typeof(IPropertyBag).GUID, out object propBagObj);
			var propertyBag = (IPropertyBag)propBagObj;

			if (0 == propertyBag.Read("CLSID", out object clsid, null))
			{
				if ((string)clsid == "{D9BF4085-E5A6-4320-A626-5318CB7D57D0}")
				{
					return true;
				}
			}

			return false;
		}

		public static bool HasAudioPin(IBaseFilter filter)
		{
			IEnumPins enumPins;
			int hr = filter.EnumPins(out enumPins);
			DsError.ThrowExceptionForHR(hr);

			IPin[] pins = new IPin[1];
			IntPtr fetched = IntPtr.Zero;

			while (enumPins.Next(pins.Length, pins, fetched) == 0)
			{
				foreach (var pin in pins)
				{
					if (IsAudioPin(pin))
					{
						Marshal.ReleaseComObject(pin);
						Marshal.ReleaseComObject(enumPins);
						return true;
					}
					Marshal.ReleaseComObject(pin);
				}
			}

			Marshal.ReleaseComObject(enumPins);
			return false;
		}

		private static bool IsAudioPin(IPin pin)
		{
			IEnumMediaTypes enumMediaTypes;
			int hr = pin.EnumMediaTypes(out enumMediaTypes);
			DsError.ThrowExceptionForHR(hr);

			AMMediaType[] mediaTypes = new AMMediaType[1];
			IntPtr fetched = IntPtr.Zero;

			while (enumMediaTypes.Next(mediaTypes.Length, mediaTypes, fetched) == 0)
			{
				foreach (var mediaType in mediaTypes)
				{
					if (mediaType.majorType == MediaType.Audio)
					{
						DsUtils.FreeAMMediaType(mediaType);
						Marshal.ReleaseComObject(enumMediaTypes);
						return true;
					}
					DsUtils.FreeAMMediaType(mediaType);
				}
			}

			Marshal.ReleaseComObject(enumMediaTypes);
			return false;
		}

		public int SampleCB(double SampleTime, IMediaSample pSample)
		{
			throw new NotImplementedException();
		}

		public int BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
		{
			NewAudioPacket?.Invoke(this, new NewAudioPacketEventArgs(SampleTime, pBuffer, BufferLen));
			return 0;
		}
	}
}
