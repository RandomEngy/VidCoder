using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon;

namespace VidCoder;

public class XamlStatics
{
	public static XamlStatics Instance { get; } = new XamlStatics();

	public bool IsBeta => CommonUtilities.Beta;
}
