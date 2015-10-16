using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Encode;
using Newtonsoft.Json;

namespace HandBrakeInteropExample
{
	public class MainViewModel : ViewModelBase
	{
		public MainViewModel()
		{
			HandBrakeUtils.EnsureGlobalInit();
			HandBrakeUtils.SetDvdNav(true);
		}

		private double progress;

		public double Progress
		{
			get
			{
				return this.progress;
			}

			set
			{
				this.progress = value;
				this.RaisePropertyChanged(() => this.Progress);
			}
		}

		private RelayCommand encodeCommand;

		public RelayCommand EncodeCommand
		{
			get
			{
				return this.encodeCommand ?? (this.encodeCommand = new RelayCommand(() =>
				{
					var instance = new HandBrakeInstance();
					instance.Initialize(1);
					//instance.ScanCompleted += (o, e) =>
					//{
					instance.EncodeCompleted += this.OnEncodeCompleted;
					instance.EncodeProgress += this.OnEncodeProgress;
					instance.StartEncode(JsonConvert.DeserializeObject<JsonEncodeObject>(File.ReadAllText("encode_json.txt")));
					//};

					//instance.StartScan("F:\\", 10, TimeSpan.FromMinutes(1), 1);
				}));
			}
		}

		private void OnEncodeProgress(object sender, HandBrake.ApplicationServices.Interop.EventArgs.EncodeProgressEventArgs e)
		{
			this.Progress = e.FractionComplete;
		}

		private void OnEncodeCompleted(object sender, HandBrake.ApplicationServices.Interop.EventArgs.EncodeCompletedEventArgs e)
		{
			this.Progress = 0;
		}
	}
}
