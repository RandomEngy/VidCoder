using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Services
{
	public class PastebinService
	{
		public async Task<string> SubmitToPastebinAsync(string text, string pasteName)
		{
			var formContent = new FormUrlEncodedContent(
				new Dictionary<string, string>
				{
					{ "api_option", "paste" },
					//{ "api_user_key", "" },
					{ "api_dev_key", "068bcd32252b1e2f932eb3bbaa61e7dc" },
					{ "api_paste_code", text },
					{ "api_paste_name", pasteName }
				});

			var client = new HttpClient();
			HttpResponseMessage response = await client.PostAsync("https://pastebin.com/api/api_post.php", formContent);

			return await response.Content.ReadAsStringAsync();
		}
	}
}
