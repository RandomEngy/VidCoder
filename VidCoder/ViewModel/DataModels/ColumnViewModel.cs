﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Resources;
using ReactiveUI;
using VidCoder.Properties;

namespace VidCoder.ViewModel;

using Resources;

public class ColumnViewModel : ReactiveObject
{
	private static ResourceManager resources = new ResourceManager("VidCoder.Resources.CommonRes", typeof(CommonRes).Assembly);

	public ColumnViewModel(string id)
	{
		this.Id = id;
	}

	public string Id { get; set; }

	public string Title
	{
		get
		{
			return resources.GetString("QueueColumnName" + this.Id);
		}
	}
}
