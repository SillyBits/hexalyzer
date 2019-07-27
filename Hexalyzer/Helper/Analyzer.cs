using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Hexalyzer.Datatypes;


namespace Hexalyzer.Helper
{

	public static class Analyzers
	{
		public class Finding
		{
			public long NodeOffset;

			public Type Type;
			public long DataOffset;
			public long DataLength;

			public Finding(Type type, long offset, long length)
			{
				Type = type;
				DataOffset = offset;
				DataLength = length;
			}
		}


		/// <summary>
		/// Test data buffer against a specific value
		/// </summary>
		/// <param name="data">Data to check</param>
		/// <param name="offset">Where to start in data</param>
		/// <param name="value">Specific value to look for</param>
		/// <param name="minlen">Minimum length of check (default=1)</param>
		/// <param name="maxlen">Maximum length of check (default=8)</param>
		/// <param name="sign">Which type to check: -1:signed, 0:both, +1:unsigned</param>
		/// <returns>Type discovered, or null if no match found</returns>
		public static Type IsOfValue(IReadOnlyList<byte> data, long offset, long value, int minlen = 1, int maxlen = 8, int sign = 0)
		{
			if (minlen <= 8 && maxlen >= 8 && offset + 8 <= data.Count)
			{
				if (sign >= 0 && Helpers.ToUInt64(data, (int)offset) == (ulong)value)
					return typeof(ulong);

				if (sign <= 0 && Helpers.ToInt64(data, (int)offset) == value)
					return typeof(long);
			}

			if (minlen <= 4 && maxlen >= 4 && value <= uint.MaxValue && offset + 4 <= data.Count)
			{
				if (sign >= 0 && Helpers.ToUInt32(data, (int)offset) == value)
					return typeof(uint);

				if (sign <= 0 && value <= int.MaxValue)
					if (Helpers.ToInt32(data, (int)offset) == value)
						return typeof(int);
			}

			if (minlen <= 2 && maxlen >= 2 && value < ushort.MaxValue && offset + 2 <= data.Count)
			{
				if (sign >= 0 && Helpers.ToUInt16(data, (int)offset) == value)
					return typeof(ushort);

				if (sign <= 0 && value <= short.MaxValue)
					if (Helpers.ToInt16(data, (int)offset) == value)
						return typeof(short);
			}

			if (minlen <= 1 && maxlen >= 1 && value < byte.MaxValue && offset + 1 <= data.Count)
			{
				if (sign >= 0 && Helpers.ToByte(data, (int)offset) == value)
					return typeof(byte);

				if (sign <= 0 && value <= sbyte.MaxValue)
					if (Helpers.ToSByte(data, (int)offset) == value)
						return typeof(sbyte);
			}

			return null;
		}

		/// <summary>
		/// Check for valid 8bit string
		/// </summary>
		/// <param name="data">Data to check</param>
		/// <param name="offset">Where to start in data</param>
		/// <param name="length">Assumed length of string</param>
		/// <returns>Outcome</returns>
		public static bool IsAsciiString(IReadOnlyList<byte> data, long offset, long length)
		{
			if (offset + length > data.Count)
				return false;

			// Null-terminator present?
			if (data[(int)(offset + length - 1)] != 0)
				return false;

			for (long idx = offset; idx < offset + length - 1; ++idx)
			{
				byte b = data[(int)idx];

				if (b > 127)
					return false;
				if (b < 32 && !_ValidCtrlChars.Contains(b))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Check for valid 16bit string
		/// </summary>
		/// <param name="data">Data to check</param>
		/// <param name="offset">Where to start in data</param>
		/// <param name="length">Assumed length of string</param>
		/// <returns>Outcome</returns>
		public static bool IsWideString(IReadOnlyList<byte> data, long offset, long length)
		{
			long len2 = length << 1;

			if (offset + len2 > data.Count)
				return false;

			// Null-terminator present?
			if (data[(int)(offset + len2 - 2)] != 0 || data[(int)(offset + len2 - 1)] != 0) 
				return false;

			for (long idx = offset; idx < offset + len2 - 2; idx += 2)
			{
				byte b1 = data[(int) idx     ];
				byte b2 = data[(int)(idx + 1)];

				if (b1 < 128)
				{
					if (b2 != 0)
						return false;
					if (b1 < 32 && !_ValidCtrlChars.Contains(b1))
						return false;
				}
				else if (0xC2 <= b1 && b1 <= 0xDF)
				{
					if (b2 < 0x80 || b2 > 0xBF)
						return false;
				}
			}

			return true;
		}

		//TODO: Add UTF-32 checker, but do need some good examples to get implementation right


		private static byte[] _ValidCtrlChars = new byte[] { (byte)'\t', (byte)'\n', (byte)'\r' };

	}

}
