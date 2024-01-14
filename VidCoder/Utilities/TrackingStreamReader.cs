using System;
using System.IO;
using System.Text;

namespace VidCoder;

/// <summary>
/// A StreamReader that exposes the current byte position.
/// </summary>
/// <remarks>
/// Does not support files with BOM, and requires encoding to be specified.
/// This works for us because our log files are always UTF-8 with no BOM.
/// </remarks>
public class TrackingStreamReader : TextReader, IDisposable
{
	private const int BufferBytes = 1024;

	private Stream stream;

	private Encoding encoding;
	private Decoder decoder;

	private byte[] byteBuffer = new byte[BufferBytes];
	private char[] charBuffer;
	private int charPos;
	private int charLen;
	// Record the number of valid bytes in the byteBuffer, for a few checks.
	private int byteLen;

	public TrackingStreamReader(Stream stream, Encoding encoding)
	{
		this.stream = stream;
		this.encoding = encoding;
		this.decoder = encoding.GetDecoder();
		this.charBuffer = new char[encoding.GetMaxCharCount(BufferBytes)];
	}

	public override string ReadLine()
	{
		if (charPos == charLen)
		{
			if (ReadBuffer() == 0) return null;
		}

		StringBuilder sb = null;
		do
		{
			int i = charPos;
			do
			{
				char ch = charBuffer[i];
				// Note the following common line feed chars:
				// \n - UNIX   \r\n - DOS   \r - Mac
				if (ch == '\r' || ch == '\n')
				{
					string s;
					if (sb != null)
					{
						sb.Append(charBuffer, charPos, i - charPos);
						s = sb.ToString();
					}
					else
					{
						s = new string(charBuffer, charPos, i - charPos);
					}
					charPos = i + 1;
					if (ch == '\r' && (charPos < charLen || ReadBuffer() > 0))
					{
						if (charBuffer[charPos] == '\n') charPos++;
					}
					return s;
				}
				i++;
			} while (i < charLen);
			i = charLen - charPos;
			if (sb == null) sb = new StringBuilder(i + 80);
			sb.Append(charBuffer, charPos, i);
		} while (ReadBuffer() > 0);
		return sb.ToString();
	}

	public long BytePosition
	{
		get
		{
			return this.stream.Position - this.encoding.GetByteCount(charBuffer, charPos, charLen - charPos);
		}
	}

	internal virtual int ReadBuffer()
	{
		charLen = 0;
		charPos = 0;
		byteLen = 0;

		do
		{
			byteLen = stream.Read(byteBuffer, 0, byteBuffer.Length);

			if (byteLen == 0)
			{
				// We're at EOF
				return charLen;
			}

			charLen += decoder.GetChars(byteBuffer, 0, byteLen, charBuffer, charLen);
		} while (charLen == 0);

		return charLen;
	}

	protected override void Dispose(bool disposing)
	{
		// Dispose of our resources if this StreamReader is closable.
		// Note that Console.In should be left open.
		try
		{
			// Note that Stream.Close() can potentially throw here. So we need to 
			// ensure cleaning up internal resources, inside the finally block.  
			if (disposing && stream != null)
			{
				stream.Close();
			}
		}
		finally
		{
			if (stream != null)
			{
				stream = null;
				encoding = null;
				decoder = null;
				byteBuffer = null;
				charBuffer = null;
				charPos = 0;
				charLen = 0;
				base.Dispose(disposing);
			}
		}
	}
}
