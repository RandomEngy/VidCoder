/*  VideoSource.cs $
 	
 	   This file is part of the HandBrake source code.
 	   Homepage: <http://handbrake.fr>.
 	   It may be used under the terms of the GNU General Public License. */

using System.Collections.Generic;
using System.IO;
using System.Windows;
using VidCoder.Services;
using HandBrake.SourceData;

namespace VidCoder.Model
{
    /// <summary>
    /// An object representing a video source (DVD or input file).
    /// </summary>
    public class VideoSource
    {
        /// <summary>
        /// Default constructor for this object
        /// </summary>
        public VideoSource()
        {
            this.Titles = new List<Title>();
        }

        /// <summary>
        /// Collection of Titles associated with this DVD
        /// </summary>
        public List<Title> Titles { get; set; }
    }
}