using Omu.ValueInjecter;
using VidCoderCommon.Utilities.Injection;

namespace VidCoderCommon.Model;

    public class FileSubtitle
    {
        public string CharacterCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the subtitle track should be marked as default.
        /// </summary>
        public bool Default { get; set; }

	/// <summary>
	/// Gets or sets a value indicating the subtitle track should be burned in.
	/// </summary>
	public bool BurnedIn { get; set; }

        public string FileName { get; set; }

	public string Name { get; set; }

        public string LanguageCode { get; set; }

        public int Offset { get; set; }

        public FileSubtitle Clone()
        {
        var subtitle = new FileSubtitle();
        subtitle.InjectFrom<CloneInjection>(this);

        return subtitle;
        }
    }