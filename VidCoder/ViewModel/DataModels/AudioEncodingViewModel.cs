using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.SourceData;
using System.Collections.ObjectModel;
using System.Windows.Input;
using VidCoder.Services;
using HandBrake.Interop;

namespace VidCoder.ViewModel
{
    public class AudioEncodingViewModel : ViewModelBase
    {
        private EncodingViewModel encodingDialogVM;
        private bool initializing;

        private AudioEncoding audioEncoding;
        private ObservableCollection<TargetStreamViewModel> targetStreams;
        private int targetStreamIndex;
        private ObservableCollection<AudioEncoderViewModel> audioEncoders;
        private AudioEncoderViewModel selectedAudioEncoder;
        private ObservableCollection<MixdownViewModel> mixdownChoices;
        private MixdownViewModel selectedMixdown;
        private string sampleRate;
        private List<int> bitrateChoices;
        private int bitrate;
        private double drc;

        private ICommand removeAudioEncodingCommand;

        private OutputFormat outputFormat;

        public AudioEncodingViewModel(AudioEncoding audioEncoding, Title selectedTitle, List<int> chosenAudioTracks, OutputFormat outputFormat, EncodingViewModel encodingDialogVM)
        {
            this.initializing = true;
            this.encodingDialogVM = encodingDialogVM;

            this.audioEncoding = audioEncoding;
            this.targetStreams = new ObservableCollection<TargetStreamViewModel>();
            this.targetStreamIndex = audioEncoding.InputNumber;

            this.SetChosenTracks(chosenAudioTracks, selectedTitle);

            this.audioEncoders = new ObservableCollection<AudioEncoderViewModel>();
            this.mixdownChoices = new ObservableCollection<MixdownViewModel>();

            this.OutputFormat = outputFormat;
            this.SetAudioEncoder(audioEncoding.Encoder);
            this.SetMixdown(audioEncoding.Mixdown);
            this.sampleRate = audioEncoding.SampleRate;
            this.bitrate = audioEncoding.Bitrate;
            this.drc = audioEncoding.Drc;

            this.initializing = false;
        }

        public AudioEncoding NewAudioEncoding
        {
            get
            {
                AudioEncoding newAudioEncoding = new AudioEncoding();
                newAudioEncoding.InputNumber = this.TargetStreamIndex;

                AudioEncoder encoder = this.SelectedAudioEncoder.Encoder;
                newAudioEncoding.Encoder = encoder;

                if (encoder != AudioEncoder.Ac3Passthrough && encoder != AudioEncoder.DtsPassthrough)
                {
                    newAudioEncoding.Mixdown = this.SelectedMixdown.Mixdown;
                    newAudioEncoding.Bitrate = this.Bitrate;
                    newAudioEncoding.SampleRate = this.SampleRate;
                    newAudioEncoding.Drc = this.Drc;
                }

                return newAudioEncoding;
            }
        }

        public ObservableCollection<TargetStreamViewModel> TargetStreams
        {
            get
            {
                return this.targetStreams;
            }
        }

        /// <summary>
        /// Gets or sets the target stream index for this encoding. 0 is All.
        /// </summary>
        public int TargetStreamIndex
        {
            get
            {
                return this.targetStreamIndex;
            }

            set
            {
                if (value >= 0)
                {
                    this.targetStreamIndex = value;
                    this.NotifyPropertyChanged("TargetStreamIndex");
                    this.encodingDialogVM.RefreshAudioPreview();
                    this.encodingDialogVM.UpdateAudioEncodings();
                    this.MarkModified();
                }
            }
        }

        public ObservableCollection<AudioEncoderViewModel> AudioEncoders
        {
            get
            {
                return this.audioEncoders;
            }
        }

        public AudioEncoderViewModel SelectedAudioEncoder
        {
            get
            {
                return this.selectedAudioEncoder;
            }

            set
            {
                this.selectedAudioEncoder = value;
                this.NotifyPropertyChanged("SelectedAudioEncoder");
                this.NotifyPropertyChanged("EncoderSettingsVisible");
                this.MarkModified();

                if (value != null)
                {
                    this.RefreshMixdownChoices();
                    this.RefreshBitrateChoices();
                    if (value.Encoder != AudioEncoder.Ac3Passthrough && value.Encoder != AudioEncoder.DtsPassthrough)
                    {
                        if (this.Bitrate == 0)
                        {
                            this.Bitrate = 160;
                        }

                        if (this.SampleRate == null)
                        {
                            this.SampleRate = "48";
                        }
                    }

                    this.encodingDialogVM.RefreshExtensionChoice();
                    this.encodingDialogVM.RefreshAudioPreview();
                    this.encodingDialogVM.UpdateAudioEncodings();
                }
            }
        }

        public bool EncoderSettingsVisible
        {
            get
            {
                if (this.SelectedAudioEncoder == null)
                {
                    return false;
                }

                return this.SelectedAudioEncoder.Encoder != AudioEncoder.Ac3Passthrough && this.SelectedAudioEncoder.Encoder != AudioEncoder.DtsPassthrough;
            }
        }

        public ObservableCollection<MixdownViewModel> MixdownChoices
        {
            get
            {
                return this.mixdownChoices;
            }
        }

        public MixdownViewModel SelectedMixdown
        {
            get
            {
                return this.selectedMixdown;
            }

            set
            {
                this.selectedMixdown = value;
                this.NotifyPropertyChanged("SelectedMixdown");

                if (value != null)
                {
                    this.encodingDialogVM.UpdateAudioEncodings();
                    this.MarkModified();
                }
            }
        }

        public string SampleRate
        {
            get
            {
                return this.sampleRate;
            }

            set
            {
                this.sampleRate = value;
                this.NotifyPropertyChanged("SampleRate");
                this.encodingDialogVM.UpdateAudioEncodings();
                this.MarkModified();
            }
        }

        public List<int> BitrateChoices
        {
            get
            {
                return this.bitrateChoices;
            }

            set
            {
                this.bitrateChoices = value;
                this.NotifyPropertyChanged("BitrateChoices");
            }
        }

        public int Bitrate
        {
            get
            {
                return this.bitrate;
            }

            set
            {
                this.bitrate = value;
                this.NotifyPropertyChanged("Bitrate");
                this.encodingDialogVM.RefreshAudioPreview();
                this.encodingDialogVM.UpdateAudioEncodings();
                this.MarkModified();
            }
        }

        public double Drc
        {
            get
            {
                return this.drc;
            }

            set
            {
                this.drc = value;
                this.NotifyPropertyChanged("Drc");
                this.encodingDialogVM.UpdateAudioEncodings();
                this.MarkModified();
            }
        }

        public OutputFormat OutputFormat
        {
            get
            {
                return this.outputFormat;
            }

            set
            {
                this.outputFormat = value;
                this.RefreshEncoderChoices();
            }
        }

        public ICommand RemoveAudioEncodingCommand
        {
            get
            {
                if (this.removeAudioEncodingCommand == null)
                {
                    this.removeAudioEncodingCommand = new RelayCommand(param =>
                    {
                        this.encodingDialogVM.RemoveAudioEncoding(this);
                    });
                }

                return this.removeAudioEncodingCommand;
            }
        }

        public void SetChosenTracks(List<int> chosenAudioTracks, Title selectedTitle)
        {
            DispatchService.Invoke(() =>
            {
                int previousIndex = this.TargetStreamIndex;

                this.targetStreams.Clear();
                this.targetStreams.Add(new TargetStreamViewModel { Text = "All" });

                //for (int i = this.targetStreams.Count - 1; i > 0; i--)
                //{
                //    this.targetStreams.RemoveAt(i);
                //}

                int shownStreams = Math.Max(previousIndex, chosenAudioTracks.Count);

                for (int i = 0; i < shownStreams; i++)
                {
                    string details = null;
                    if (i < chosenAudioTracks.Count && selectedTitle != null)
                    {
                        details = selectedTitle.AudioTracks[chosenAudioTracks[i] - 1].NoTrackDisplay;
                    }

                    this.targetStreams.Add(new TargetStreamViewModel { Text = "Stream " + (i + 1), TrackDetails = details });
                }

                // Set to -1, then back to real index in order to force a refresh on the ComboBox
                this.targetStreamIndex = -1;
                this.NotifyPropertyChanged("TargetStreamIndex");

                this.targetStreamIndex = previousIndex;
                this.NotifyPropertyChanged("TargetStreamIndex");
            });
        }

        private void RefreshEncoderChoices()
        {
            AudioEncoderViewModel lastSelectedEncoder = this.SelectedAudioEncoder;
            this.AudioEncoders.Clear();

            if (this.OutputFormat == OutputFormat.Mkv)
            {
                this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Faac });
                this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Lame });
                this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Ac3Passthrough });
                this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.DtsPassthrough });
                this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Vorbis });
            }
            else
            {
                this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Faac });
                this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Lame });
                this.AudioEncoders.Add(new AudioEncoderViewModel { Encoder = AudioEncoder.Ac3Passthrough });
            }

            AudioEncoderViewModel newSelectedVM = null;
            if (lastSelectedEncoder != null)
            {
                newSelectedVM = this.AudioEncoders.SingleOrDefault(encoderVM => encoderVM.Encoder == lastSelectedEncoder.Encoder);
            }

            if (newSelectedVM == null)
            {
                newSelectedVM = this.AudioEncoders.Single(encoderVM => encoderVM.Encoder == AudioEncoder.Faac);
            }

            this.SelectedAudioEncoder = newSelectedVM;
        }

        private void RefreshMixdownChoices()
        {
            MixdownViewModel lastSelectedMixdown = this.SelectedMixdown;
            this.MixdownChoices.Clear();
            switch (this.SelectedAudioEncoder.Encoder)
            {
                case AudioEncoder.Faac:
                    this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.Mono });
                    this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.Stereo });
                    this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.DolbySurround });
                    this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.DolbyProLogicII });
                    this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.SixChannelDiscrete });
                    break;
                case AudioEncoder.Lame:
                case AudioEncoder.Vorbis:
                    this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.Stereo });
                    this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.DolbySurround });
                    this.MixdownChoices.Add(new MixdownViewModel { Mixdown = Mixdown.DolbyProLogicII });
                    break;
                default:
                    break;
            }

            MixdownViewModel newSelectedMixdown = null;
            if (lastSelectedMixdown != null)
            {
                newSelectedMixdown = this.MixdownChoices.SingleOrDefault(mixdownVM => mixdownVM.Mixdown == lastSelectedMixdown.Mixdown);
            }

            if (newSelectedMixdown == null)
            {
                newSelectedMixdown = this.MixdownChoices.SingleOrDefault(mixdownVM => mixdownVM.Mixdown == Mixdown.DolbyProLogicII);
            }

            this.SelectedMixdown = newSelectedMixdown;
        }

        private void RefreshBitrateChoices()
        {
            switch (this.SelectedAudioEncoder.Encoder)
            {
                case AudioEncoder.Faac:
                    this.BitrateChoices = new List<int> { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160 };
                    if (this.Bitrate > 160)
                    {
                        this.Bitrate = 160;
                    }

                    break;
                case AudioEncoder.Lame:
                case AudioEncoder.Vorbis:
                    this.BitrateChoices = new List<int> { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
                    break;
                default:
                    break;
            }
        }

        private void SetAudioEncoder(AudioEncoder encoder)
        {
            AudioEncoderViewModel newSelectedVM = null;
            newSelectedVM = this.AudioEncoders.SingleOrDefault(encoderVM => encoderVM.Encoder == encoder);

            if (newSelectedVM == null)
            {
                newSelectedVM = this.AudioEncoders.Single(encoderVM => encoderVM.Encoder == AudioEncoder.Faac);
            }

            this.SelectedAudioEncoder = newSelectedVM;
        }

        private void SetMixdown(Mixdown mixdown)
        {
            MixdownViewModel newSelectedMixdown = null;
            newSelectedMixdown = this.MixdownChoices.SingleOrDefault(mixdownVM => mixdownVM.Mixdown == mixdown);

            if (newSelectedMixdown == null)
            {
                newSelectedMixdown = this.MixdownChoices.SingleOrDefault(mixdownVM => mixdownVM.Mixdown == Mixdown.DolbyProLogicII);
            }

            this.SelectedMixdown = newSelectedMixdown;
        }

        private void MarkModified()
        {
            if (!this.initializing)
            {
                this.encodingDialogVM.IsModified = true;
            }
        }
    }
}
