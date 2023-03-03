using PipeMethodCalls;
using PipeMethodCalls.NetJson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoderCommon.Services;

public class AutomationClient
{
	public async Task<AutomationResult> RunActionAsync(Expression<Action<IVidCoderAutomation>> action)
	{
		for (int i = 0; i < 30; i++)
		{
			var encodeResult = await TryActionAsync(action).ConfigureAwait(false);
			if (encodeResult != AutomationResult.ConnectionFailed)
			{
				return encodeResult;
			}

			await Task.Delay(1000).ConfigureAwait(false);
		}

		return AutomationResult.ConnectionFailed;
	}

	public async Task<AutomationResult> TryActionAsync(Expression<Action<IVidCoderAutomation>> action)
	{
		try
		{
			string betaString = string.Empty;
			if (CommonUtilities.Beta)
			{
				betaString = "Beta";
			}

			using (var client = new PipeClient<IVidCoderAutomation>(new NetJsonPipeSerializer(), "VidCoderAutomation" + betaString))
			{
				client.SetLogger(Console.WriteLine);

				await client.ConnectAsync().ConfigureAwait(false);
				await client.InvokeAsync(action).ConfigureAwait(false);
			}

			return AutomationResult.Success;
		}
		catch (PipeInvokeFailedException)
		{
			throw;
		}
		catch (Exception)
		{
			return AutomationResult.ConnectionFailed;
		}
	}
}
