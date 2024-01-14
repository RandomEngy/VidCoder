using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using VidCoder.Resources;

namespace VidCoder.Model;

public class EncodeCompleteAction
{
	public EncodeCompleteActionType ActionType { get; set; }

	public EncodeCompleteTrigger Trigger { get; set; }

	public bool ShowTriggerInDisplay { get; set; }

	public string DriveLetter { get; set; }

    public override string ToString()
    {
        var converter = new EnumStringConverter<EncodeCompleteActionType>();
        string actionDisplayString = converter.Convert(this.ActionType);

        if (this.ActionType == EncodeCompleteActionType.EjectDisc)
        {
            actionDisplayString = string.Format(actionDisplayString, this.DriveLetter);
        }

	    if (!this.ShowTriggerInDisplay)
	    {
		    return actionDisplayString;
		}

	    string displayFormat;
	    if (this.Trigger == EncodeCompleteTrigger.DoneWithQueue)
	    {
		    displayFormat = MainRes.WithQueueFormat;
	    }
	    else
	    {
		    displayFormat = MainRes.WithCurrentJobsFormat;
	    }

	    return string.Format(CultureInfo.CurrentUICulture, displayFormat, actionDisplayString);
    }

    public bool Equals(EncodeCompleteAction action2)
	{
		return action2 != null && this.ActionType == action2.ActionType && this.DriveLetter == action2.DriveLetter && this.Trigger == action2.Trigger;
	}
}
