using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using VidCoderCommon.Model;

namespace VidCoderCommon.Model
{
	public class VidCoderContractResolver : DefaultContractResolver
	{
		protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
		{
			JsonProperty property = base.CreateProperty(member, memberSerialization);
			CustomAttributeData data = member.CustomAttributes?.FirstOrDefault(a => a.AttributeType == typeof(DeserializeOnlyAttribute));
			if (data != null)
			{
				property.ShouldSerialize = o => false;
			}

			return property;
		}
	}
}
