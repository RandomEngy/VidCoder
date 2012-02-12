using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public interface IVideoPlayer
	{
		bool Installed { get; }

		void PlayTitle(string discPath, int title);

		string Id { get; }

		string Display { get; }
	}
}
