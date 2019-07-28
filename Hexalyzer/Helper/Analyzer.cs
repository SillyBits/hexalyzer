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
		public static Type IsOfValue(IAccessor<byte> data, long offset, long value, int minlen = 1, int maxlen = 8, int sign = 0)
		{
			if (minlen <= 8 && maxlen >= 8 && offset + 8 <= data.LongCount)
			{
				if (sign >= 0 && Helpers.ToUInt64(data, offset) == (ulong)value)
					return typeof(ulong);

				if (sign <= 0 && Helpers.ToInt64(data, offset) == value)
					return typeof(long);
			}

			if (minlen <= 4 && maxlen >= 4 && value <= uint.MaxValue && offset + 4 <= data.LongCount)
			{
				if (sign >= 0 && Helpers.ToUInt32(data, offset) == value)
					return typeof(uint);

				if (sign <= 0 && value <= int.MaxValue)
					if (Helpers.ToInt32(data, offset) == value)
						return typeof(int);
			}

			if (minlen <= 2 && maxlen >= 2 && value < ushort.MaxValue && offset + 2 <= data.LongCount)
			{
				if (sign >= 0 && Helpers.ToUInt16(data, offset) == value)
					return typeof(ushort);

				if (sign <= 0 && value <= short.MaxValue)
					if (Helpers.ToInt16(data, offset) == value)
						return typeof(short);
			}

			if (minlen <= 1 && maxlen >= 1 && value < byte.MaxValue && offset + 1 <= data.LongCount)
			{
				if (sign >= 0 && Helpers.ToByte(data, offset) == value)
					return typeof(byte);

				if (sign <= 0 && value <= sbyte.MaxValue)
					if (Helpers.ToSByte(data, offset) == value)
						return typeof(sbyte);
			}

			return null;
		}


		/// <summary>
		/// Check for valid 7bit Ascii char
		/// </summary>
		/// <param name="data">Value</param>
		/// <returns>Outcome</returns>
		public static bool IsAsciiChar(byte data)
		{
			if (data < 32 && !_ValidCtrlChars.Contains(data))
				return false;
			return (data <= 127);
		}

		/// <summary>
		/// Check for valid 7bit Ascii string
		/// </summary>
		/// <param name="data">Data to check</param>
		/// <param name="offset">Where to start in data</param>
		/// <param name="length">Assumed length of string</param>
		/// <returns>Outcome</returns>
		public static bool IsAsciiString(IAccessor<byte> data, long offset, long length)
		{
			if (offset + length > data.LongCount)
				return false;

			// Null-terminator present?
			if (data[offset + length - 1] != 0)
				return false;

			for (long idx = offset; idx < offset + length - 1; ++idx)
				if (!IsAsciiChar(data[idx]))
					return false;

			return true;
		}


		/// <summary>
		/// Check for valid 16bit UCS-2 char
		/// </summary>
		/// <param name="data">Value to check</param>
		/// <returns>Outcome</returns>
		public static bool IsWideChar(ushort data)
		{
			byte b1 = (byte)(data & 0xFF);
			data >>= 8;
			byte b2 = (byte)(data & 0xFF);

			return IsWideChar(b1, b2);
		}

		/// <summary>
		/// Check for valid 16bit UCS-2 char
		/// </summary>
		/// <param name="byte1">1st byte to check (00##)</param>
		/// <param name="byte2">2nd byte to check (##00)</param>
		/// <returns>Outcome</returns>
		public static bool IsWideChar(byte byte1, byte byte2)
		{
			if (0xC2 <= byte1 && byte1 <= 0xDF)
				return (0x80 <= byte2 && byte2 <= 0xBF);

			return (byte2 == 0) && IsAsciiChar(byte1);
		}

		/// <summary>
		/// Check for valid 16bit UCS-2 string
		/// </summary>
		/// <param name="data">Data to check</param>
		/// <param name="offset">Where to start in data</param>
		/// <param name="length">Assumed length of string</param>
		/// <returns>Outcome</returns>
		public static bool IsWideString(IAccessor<byte> data, long offset, long length)
		{
			long len2 = length << 1;

			if (offset + len2 > data.LongCount)
				return false;

			// Null-terminator present?
			if (data[offset + len2 - 2] != 0 || data[offset + len2 - 1] != 0) 
				return false;

			for (long idx = offset; idx < offset + len2 - 2; idx += 2)
			{
				byte b1 = data[idx    ];
				byte b2 = data[idx + 1];

				if (!IsWideChar(b1, b2))
					return false;
			}

			return true;
		}


		//TODO: Add UTF-32 checker, but do need some good examples to get implementation right


		private static byte[] _ValidCtrlChars = new byte[] { 0, (byte)'\t', (byte)'\n', (byte)'\r' };

	}

}
