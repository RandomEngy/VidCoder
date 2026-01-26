using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.HbLib;

namespace VidCoder;

public static class HwDecodeUtilities
{
	public static int GetHwDecode()
	{
		int hwDecode = 0;
		if (HandBrakeHardwareEncoderHelper.IsNVDecAvailable && HandBrakeHardwareEncoderHelper.IsNVEncH264Available && Config.EnableNVDec)
		{
			hwDecode = (int)NativeConstants.HB_DECODE_NVDEC;
		}

		if (HandBrakeHardwareEncoderHelper.IsDirectXAvailable && Config.EnableDirectXDecoding)
		{
			hwDecode = (int)NativeConstants.HB_DECODE_MF;
		}

		if (HandBrakeHardwareEncoderHelper.IsQsvAvailable && Config.EnableQuickSyncDecoding)
		{
			hwDecode |= (int)NativeConstants.HB_DECODE_QSV;
		}

		return hwDecode;
	}
}
