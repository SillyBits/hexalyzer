using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;


namespace Hexalyzer.Datatypes
{

	public class AsciiChar
	{
		public AsciiChar()
		{
			Value = null;
		}

		public AsciiChar(char val)
		{
			Value = val;
		}

		public static AsciiChar FromData(IAccessor<byte> data, long offset = 0)
		{
			if (offset < 0 || offset >= data.LongCount)
				throw new IndexOutOfRangeException();

			return new AsciiChar((char)data[offset]);
		}

		public override string ToString()
		{
			return "'" + Value + "'";
		}

		public char? Value { get; internal set; }
	}


	// Pascal-like ascii string with leading length (Int32)
	public class AsciiString
	{
		public AsciiString()
		{
			Value = null;
		}

		public AsciiString(string val)
		{
			Value = val;
		}

		public static AsciiString FromData(IAccessor<byte> data, long offset = 0)
		{
			if (offset < 0 || offset >= data.LongCount)
				throw new IndexOutOfRangeException();

			if (offset + 4 < data.LongCount)
			{
				int len = Helpers.ToInt32(data, offset);
				offset += 4;
				if (len > 0 && len <= 4096 && offset + len <= data.LongCount)
				{
					if (data[offset + len - 1] == 0)
					{
						//TODO: Check if this .ToArray() is a bottleneck or not
						//return new AsciiString(Encoding.ASCII.GetString(data.ToArray(), (int)offset, len - 1));
						//=> Now passing a shifted accessor
						IAccessor<byte> shifted = data[offset, len];
						return new AsciiString(Encoding.ASCII.GetString(shifted.ToArray(), 0, len - 1));
					}
				}
			}

			return null;
		}

		public override string ToString()
		{
			if (Value == null)
				return "<NULL>";
			//return "[" + Value.Length.ToString() + "]'" + Value + "'";
			return Value[0] != 0 ? "'" + Value + "'" : "<empty>";
		}

		public string Value { get; internal set; }

		public static int LengthOf(IAccessor<byte> data, long offset)
		{
			int len = Helpers.ToInt32(data, offset);
			if (len < 0)
				throw new ArgumentException();
			if (len > 0)
				len += 4;
			return len;
		}
	}

	// Pascal-like unicode string with leading length (Int32)
	public class WideString
	{
		public WideString()
		{
			Value = null;
		}

		public WideString(string val)
		{
			Value = val;
		}

		public static WideString FromData(IAccessor<byte> data, long offset = 0)
		{
			if (offset < 0 || offset >= data.LongCount)
				throw new IndexOutOfRangeException();

			if (offset + 4 < data.LongCount)
			{
				int len = Helpers.ToInt32(data, offset);
				offset += 4;
				if (len > 0 && len <= 4096 && offset + (len * 2) <= data.LongCount)
				{
					if (data[offset + (len * 2) - 2] == 0 && data[offset + (len * 2) - 1] == 0)
					{
						//TODO: Check if this .ToArray() is a bottleneck or not
						//return new WideString(Encoding.Unicode.GetString(data.ToArray(), (int)offset, (len - 1) * 2));
						//=> Now passing a shifted accessor
						IAccessor<byte> shifted = data[offset, len * 2];
						return new WideString(Encoding.Unicode.GetString(shifted.ToArray(), 0, (len - 1) * 2));
					}
				}
			}

			return null;
		}

		public override string ToString()
		{
			if (Value == null)
				return "<NULL>";
			//return "[" + Value.Length.ToString() + "]'" + Value + "'";
			return Value[0] != 0 ? "'" + Value + "'" : "<empty>";
		}

		public string Value { get; internal set; }

		public static int LengthOf(IAccessor<byte> data, long offset)
		{
			int len = Helpers.ToInt32(data, offset);
			if (len < 0)
				throw new ArgumentException();
			if (len > 0)
				len += 4;
			return len;
		}
	}

	// Pascal-like string with leading length (Int32)
	// Either AnsiString (len>0) or WideString (len<0)
	public class VarString
	{
		public VarString()
		{
			Value = null;
		}

		public VarString(string val)
		{
			Value = val;
		}

		public static VarString FromData(IAccessor<byte> data, long offset = 0)
		{
			if (offset < 0 || offset >= data.LongCount)
				throw new IndexOutOfRangeException();

			if (offset + 4 < data.LongCount)
			{
				int len = Helpers.ToInt32(data, offset);
				offset += 4;
				if (len < 0)
				{
					len = (-len);
					if (len <= 4096 && offset + (len * 2) <= data.LongCount)
					{
						if (data[offset + (len * 2) - 2] == 0 && data[offset + (len * 2) - 1] == 0)
						{
							//TODO: Check if this .ToArray() is a bottleneck or not
							//return new VarString(Encoding.Unicode.GetString(data.ToArray(), (int)offset, (len - 1) * 2));
							//=> Now passing a shifted accessor
							IAccessor<byte> shifted = data[offset, len * 2];
							return new VarString(Encoding.Unicode.GetString(shifted.ToArray(), 0, (len - 1) * 2));
						}
					}
				}
				else if (len > 0)
				{
					if (len <= 4096 && offset + len <= data.LongCount)
					{
						if (data[offset + len - 1] == 0)
						{
							//TODO: Check if this .ToArray() is a bottleneck or not
							//return new VarString(Encoding.ASCII.GetString(data.ToArray(), (int)offset, len - 1));
							//=> Now passing a shifted accessor
							IAccessor<byte> shifted = data[offset, len];
							return new VarString(Encoding.ASCII.GetString(shifted.ToArray(), 0, len - 1));
						}
					}
				}
			}

			return null;
		}

		public override string ToString()
		{
			if (Value == null)
				return "<NULL>";
			//return "[" + Value.Length.ToString() + "]'" + Value + "'";
			return Value[0] != 0 ? "'" + Value + "'" : "<empty>";
		}

		public string Value { get; internal set; }

		public static int LengthOf(IAccessor<byte> data, long offset)
		{
			int len = Helpers.ToInt32(data, offset);
			if (len == 0)
				return 0;
			if (len < 0)
				len = (-len) * 2;
			return 4 + len;
		}
	}


	public static class Helpers
	{

		public static bool ToBoolean(IAccessor<byte> data, long offset)
		{
			return data[offset] != 0;
		}

		public static sbyte ToSByte(IAccessor<byte> data, long offset)
		{
			return (sbyte)data[offset];
		}
		public static byte ToByte(IAccessor<byte> data, long offset)
		{
			return data[offset];
		}

		public static char ToChar(IAccessor<byte> data, long offset)
		{
			return (char)ToUInt16(data, offset);
		}

		public static short ToInt16(IAccessor<byte> data, long offset)
		{
			return (short)((data[offset + 1] << 8) + data[offset]);
		}
		public static ushort ToUInt16(IAccessor<byte> data, long offset)
		{
			return (ushort)((data[offset + 1] << 8) + data[offset]);
		}

		public static int ToInt32(IAccessor<byte> data, long offset)
		{
			int value = data[offset + 3];
			value = (value << 8) + data[offset + 2];
			value = (value << 8) + data[offset + 1];
			value = (value << 8) + data[offset];
			return value;
		}
		public static uint ToUInt32(IAccessor<byte> data, long offset)
		{
			uint value = data[offset + 3];
			value = (value << 8) + data[offset + 2];
			value = (value << 8) + data[offset + 1];
			value = (value << 8) + data[offset];
			return value;
		}

		public static long ToInt64(IAccessor<byte> data, long offset)
		{
			return (((long)ToUInt32(data, offset + 4)) << 32) + ToUInt32(data, offset);
		}
		public static ulong ToUInt64(IAccessor<byte> data, long offset)
		{
			return (((ulong)ToUInt32(data, offset + 4)) << 32) + ToUInt32(data, offset);
		}

		/*
		 * TODO: Single, Double
		 */


		/// <summary>
		/// Converts data passed in into a string representation based on type given
		/// </summary>
		/// <param name="type">Type to use for conversion</param>
		/// <param name="data">Data used to form value</param>
		/// <param name="offset">Optional offset into data</param>
		/// <returns>String representation</returns>
		public static string ToString(Type type, IAccessor<byte> data, long offset = 0)
		{
			if (offset < 0 || offset >= data.LongCount)
				throw new IndexOutOfRangeException();

			if (type == null)
				return "";

			if (data == null)
				return "<NULL>";

			//TODO: Replace with more dynamic reflection
			try
			{
				object obj;
				switch (type.Name)
				{
					case "Boolean":		return ToBoolean(data, offset).ToString();
					case "Char":		return ToChar(data, offset).ToString();
					case "SByte":		return ToSByte(data, offset).ToString("G");
					case "Byte":		return ToByte(data, offset).ToString("X2") + "h";
					case "Int16":		if (offset + 2 <= data.LongCount) return ToInt16(data, offset).ToString("G"); else break;
					case "UInt16":		if (offset + 2 <= data.LongCount) return ToUInt16(data, offset).ToString("X4") + "h"; else break;
					case "Int32":		if (offset + 4 <= data.LongCount) return ToInt32(data, offset).ToString("G"); else break;
					case "UInt32":		if (offset + 4 <= data.LongCount) return ToUInt32(data, offset).ToString("X8") + "h"; else break;
					case "Int64":		if (offset + 8 <= data.LongCount) return ToInt64(data, offset).ToString("G"); else break;
					case "UInt64":		if (offset + 8 <= data.LongCount) return ToUInt64(data, offset).ToString("X16") + "h"; else break;
				//	case "Single":		if (offset + 4 <= data.LongCount) return ToSingle(data, offset).ToString(); else break;
				//	case "Double":		if (offset + 8 <= data.LongCount) return ToDouble(data, offset).ToString(); else break;

					case "AsciiChar":	obj = AsciiChar.FromData(data, offset); if (obj != null) return obj.ToString(); else break;
					case "AsciiString": obj = AsciiString.FromData(data, offset); if (obj != null) return obj.ToString(); else break;
					case "WideString":	obj = WideString.FromData(data, offset); if (obj != null) return obj.ToString(); else break;
					case "VarString":	obj = VarString.FromData(data, offset); if (obj != null) return obj.ToString(); else break;
				}

				if (type.GetInterface("IDatatype") != null)
				{
					try
					{
						Plugin.IDatatype datatype = Activator.CreateInstance(type) as Plugin.IDatatype;
						if (datatype != null)
							datatype = datatype.FromData(data, offset);
						if (datatype != null)
							return datatype.ToString();
					}
					catch (Exception)
					{
						//string msg = string.Format("Unable to create instance of type {0}!", 
						//	type.FullName);
						//Debug.Fail(msg);
					}
				}
			}
			catch (Exception)
			{ }

			return "";
			//throw new ArgumentException(string.Format("Unknown type {0}", type));
		}

		/// <summary>
		/// Find length of required data based on type given
		/// </summary>
		/// <param name="type">Type to use</param>
		/// <param name="data">Data (in case type is of varying length like {Ansi|Wide|Var}String)</param>
		/// <param name="offset">Offset into data (in case type is of varying length like VarString)</param>
		/// <returns>Number of bytes for given type, or -1 if none found (or error occured)</returns>
		//public static int LengthOf(Type type, byte[] data, long offset)
		public static long LengthOf(Type type, IAccessor<byte> data, long offset)
		{
			if (data == null || data.LongCount == 0 || 
				offset < 0 || offset >= data.LongCount)
				return -1;

			if (type == null)
				return data.LongCount - offset;

			//TODO: Replace with more dynamic reflection
			switch(type.Name)
			{
				case "Boolean":		return 1;

				case "AsciiChar":	return 1;
				case "Char":		return 2;

				case "SByte":		return 1;
				case "Byte":		return 1;
				case "Int16":		return 2;
				case "UInt16":		return 2;
				case "Int32":		return 4;
				case "UInt32":		return 4;
				case "Int64":		return 8;
				case "UInt64":		return 8;
			//	case "Single":		return 4;
			//	case "Double":		return 8;

				case "AsciiString":	return AsciiString.LengthOf(data, offset);
				case "WideString":	return WideString.LengthOf(data, offset);
				case "VarString":	return VarString.LengthOf(data, offset);
			}

			if (type.GetInterface("IDatatype") != null)
			{
				try
				{
					Plugin.IDatatype datatype = Activator.CreateInstance(type) as Plugin.IDatatype;
					return (int)datatype.LengthOf(data, offset);
				}
				catch (Exception)
				{
					string msg = string.Format("Unable to create instance of type {0}!", 
						type.FullName);
					Debug.Fail(msg);
				}
			}

			throw new ArgumentException(string.Format("Unknown type {0}", type));
		}
	}
}
