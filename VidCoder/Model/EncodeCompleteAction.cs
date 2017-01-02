using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public class EncodeCompleteAction
	{
		public EncodeCompleteActionType ActionType { get; set; }

		public string DriveLetter { get; set; }

	    public override string ToString()
	    {
            var converter = new EnumStringConverter<EncodeCompleteActionType>();
            string displayString = converter.Convert(this.ActionType);

            if (this.ActionType == EncodeCompleteActionType.EjectDisc)
            {
                displayString = string.Format(displayString, this.DriveLetter);
            }

            return displayString;
        }

	    public bool Equals(EncodeCompleteAction action2)
		{
			return action2 != null && this.ActionType == action2.ActionType && this.DriveLetter == action2.DriveLetter;
		}
	}
}
