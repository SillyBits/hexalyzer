using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;


using Hexalyzer.Plugin;
using Attr = Hexalyzer.Plugin.Attributes;


namespace Hexalyzer.Datatypes
{
	/// <summary>
	/// Basic implementation for data types using attributes
	/// </summary>
	/// <typeparam name="_Derived">Derived class</typeparam>
	/// <typeparam name="_Type">Internal data storage type</typeparam>
	public abstract class DatatypeBase<_Derived, _Type> : IDatatype
		where _Derived : IDatatype, new()
	{

		public DatatypeBase() { _Value = default(_Type); }
		public DatatypeBase(_Type value) { _Value = value; }

		// IDatatype
		//

		public static string      Name  = Attr.Name.Get(typeof(_Derived));
		public static ImageSource Icon  = Attr.IconRef.Get(typeof(_Derived));
		public static IDatatype   Empty = new _Derived();

		public virtual object Value { get { return _Value; } }
		public abstract long Length { get; }

		// Sadfully, no static methods allowed, so one has to use a fake instance to access those :(
		public virtual bool IsValid(IAccessor<byte> data, long offset) { return (0 <= offset && offset + LengthOf(data, offset) <= data.LongCount); }
		public virtual long LengthOf(IAccessor<byte> data, long offset) { return (_Value != null) ? Length : 0; }
		public abstract IDatatype FromData(IAccessor<byte> data, long offset);

		protected _Type _Value;

	}
	

	public static class SystemType
	{
		#region Raw conversion
		// As return value isn't part of the "signature equation", we're implementing the converters using an additional
		// "(dynamic)_Type" parameter to both get our typed signature and prevent generic to be choosen at compile-time.
		// Another generic is used to pass this.
		public static _Type FromData<_Type>(IAccessor<byte> data, long offset)
		{
			return Convert(data, offset, (dynamic)default(_Type));
		}

		public static _Type Convert<_Type>(IAccessor<byte> data, long offset, _Type _)
		{
			throw new NotImplementedException(string.Format("Convert: No specialisation for type '{0}' found!", typeof(_Type)));
		}

		public static bool Convert(IAccessor<byte> data, long offset, bool _) { return (data[offset] != 0); }

		public static sbyte Convert(IAccessor<byte> data, long offset, sbyte _) { return (sbyte)data[offset]; }
		public static byte Convert(IAccessor<byte> data, long offset, byte _) { return data[offset]; }

		public static char Convert(IAccessor<byte> data, long offset, char _) { return (char)Convert(data, offset, default(ushort)); }

		public static short Convert(IAccessor<byte> data, long offset, short _) { return (short)((data[offset + 1] << 8) + data[offset]); }
		public static ushort Convert(IAccessor<byte> data, long offset, ushort _) { return (ushort)((data[offset + 1] << 8) + data[offset]); }

		public static int Convert(IAccessor<byte> data, long offset, int _)
		{
			int val = data[offset + 3];
			val = (val << 8) + data[offset + 2];
			val = (val << 8) + data[offset + 1];
			val = (val << 8) + data[offset];
			return val;
		}
		public static uint Convert(IAccessor<byte> data, long offset, uint _)
		{
			uint val = data[offset + 3];
			val = (val << 8) + data[offset + 2];
			val = (val << 8) + data[offset + 1];
			val = (val << 8) + data[offset];
			return val;
		}

		public static long Convert(IAccessor<byte> data, long offset, long _)
		{
			long val = data[offset + 7];
			val = (val << 8) + data[offset + 6];
			val = (val << 8) + data[offset + 5];
			val = (val << 8) + data[offset + 4];
			val = (val << 8) + data[offset + 3];
			val = (val << 8) + data[offset + 2];
			val = (val << 8) + data[offset + 1];
			val = (val << 8) + data[offset];
			return val;
		}
		public static ulong Convert(IAccessor<byte> data, long offset, ulong _)
		{
			ulong val = data[offset + 7];
			val = (val << 8) + data[offset + 6];
			val = (val << 8) + data[offset + 5];
			val = (val << 8) + data[offset + 4];
			val = (val << 8) + data[offset + 3];
			val = (val << 8) + data[offset + 2];
			val = (val << 8) + data[offset + 1];
			val = (val << 8) + data[offset];
			return val;
		}

		//TODO: Add own converter?
		public static float Convert(IAccessor<byte> data, long offset, float _)
		{
			IAccessor<byte> shifted = data[offset, data.LongCount - offset];
			return BitConverter.ToSingle(shifted.ToArray(), 0);
		}
		public static double Convert(IAccessor<byte> data, long offset, double _)
		{
			IAccessor<byte> shifted = data[offset, data.LongCount - offset];
			return BitConverter.ToDouble(shifted.ToArray(), 0);
		}

		#endregion

		#region Length
		// Retrieving lengths for system types. 
		// Note that one has to pass an additional type parameter which MUST be casted to (dynamic) to prevent
		// compiler from choosing generic version over specialisation.

		public static long Length<_Type>(_Type val)
		{
			throw new NotImplementedException(string.Format("Length: No specialisation for type '{0}' found!", typeof(_Type)));
		}

		public static long Length(bool val) { return 1; }

		public static long Length(sbyte val) { return 1; }
		public static long Length(byte val) { return 1; }

		public static long Length(char val) { return 2; }

		public static long Length(short val) { return 2; }
		public static long Length(ushort val) { return 2; }

		public static long Length(int val) { return 4; }
		public static long Length(uint val) { return 4; }

		public static long Length(long val) { return 8; }
		public static long Length(ulong val) { return 8; }

		public static long Length(float val) { return 4; }
		public static long Length(double val) { return 8; }

		#endregion

		#region To string conversion
		// Conversion into displayable string

		public static string ToStr<_Type>(_Type val)
		{
			throw new NotImplementedException();
		}

		public static string ToStr(bool val) { return val.ToString(); }

		public static string ToStr(sbyte val) { return val.ToString("G"); }
		public static string ToStr(byte val) { return val.ToString("X2") + "h"; }

		public static string ToStr(char val) { return "[" + val + "]"; }

		public static string ToStr(short val) { return val.ToString("G"); }
		public static string ToStr(ushort val) { return val.ToString("X4") + "h"; }

		public static string ToStr(int val) { return val.ToString("G"); }
		public static string ToStr(uint val) { return val.ToString("X8") + "h"; }

		public static string ToStr(long val) { return val.ToString("G"); }
		public static string ToStr(ulong val) { return val.ToString("X16") + "h"; }

		//TODO: Find suitable format strings
		public static string ToStr(float val) { return val.ToString(); }
		public static string ToStr(double val) { return val.ToString(); }

		#endregion

		[Attr.Name(nameof(_Type)), Attr.IconRef("Datatype.System." + nameof(_Type) + ".png")]
		public class Wrapper<_Type> : DatatypeBase<Wrapper<_Type>, _Type>
		{

			public Wrapper() : base() { }
			public Wrapper(_Type val) : base(val) { }

			public override string ToString() { return ToStr((dynamic)_Value); }

			public override long Length { get { return _Length; } }

			public override bool IsValid(IAccessor<byte> data, long offset) { return IsValid_(data, offset); }

			public static bool IsValid_(IAccessor<byte> data, long offset) { return (0 <= offset && offset + _Length <= data.LongCount); }

			public override long LengthOf(IAccessor<byte> data, long offset) { return LengthOf_(data, offset); }

			public static long LengthOf_(IAccessor<byte> data, long offset) { return IsValid_(data, offset) ? _Length : 0; }

			public override IDatatype FromData(IAccessor<byte> data, long offset) { return FromData_(data, offset); }

			public static IDatatype FromData_(IAccessor<byte> data, long offset)
			{
				return new Wrapper<_Type>(FromData<_Type>(data, offset));
			}

			private static long _Length = Length((dynamic)default(_Type));
		}

	}


	[Attr.Name("Ascii char"), Attr.IconRef("Datatype.AsciiChar.png")]
	public class AsciiChar : DatatypeBase<AsciiChar, char>
	{
		public AsciiChar() : base() { }
		public AsciiChar(char val) : base(val) { }

		public override string ToString()
		{
			return "[" + _Value + "]";
		}

		public override long Length { get { return 1; } }

		public override IDatatype FromData(IAccessor<byte> data, long offset)
		{
			return FromData_(data, offset);
		}

		public static AsciiChar FromData_(IAccessor<byte> data, long offset)
		{
			if (offset < 0 || offset + 1 > data.LongCount)
				throw new IndexOutOfRangeException();

			return new AsciiChar((char)data[offset]);
		}

	}

	// Simple ascii string, terminated by null char
	[Attr.Name("Ascii string"), Attr.IconRef("Datatype.AsciiString.png")]
	public class AsciiString : DatatypeBase<AsciiString, string>
	{
		public AsciiString() : base() { }
		public AsciiString(string val) : base(val) { }

		public override string ToString()
		{
			if (_Value == null)
				return "<NULL>";
			if (_Value[0] == 0)
				return "<empty>";
			return "'" + _Value + "'";
		}

		public override long Length
		{
			get
			{
				long len = 0;
				if (_Value != null)
					len = _Value.Length + 1;
				return len;
			}
		}

		public override long LengthOf(IAccessor<byte> data, long offset)
		{
			return LengthOf_(data, offset);
		}

		public static long LengthOf_(IAccessor<byte> data, long offset)
		{
			int length = 0;

			// Scan up until null-terminator found, or max. allowed length exceeded
			while (length <= MAX_LENGTH)
			{
				if (offset + length > data.LongCount)
				{
					length = -1;
					break;
				}

				byte b = data[offset + length];

				if (!Helper.Analyzers.IsAsciiChar(b))
					break;

				length++;

				if (b == 0)
					break;
			}

			return length;
		}

		public override IDatatype FromData(IAccessor<byte> data, long offset)
		{
			return FromData_(data, offset, -1);
		}

		public static AsciiString FromData_(IAccessor<byte> data, long offset, long length)
		{
			if (offset < 0 || offset + length > data.LongCount)
				throw new IndexOutOfRangeException();

			if (length <= 0)
			{
				length = LengthOf_(data, offset);
			}
			if (length > 0 && length <= MAX_LENGTH && offset + length <= data.LongCount)
			{
				if (data[offset + length - 1] == 0)
				{
					IAccessor<byte> shifted = data[offset, length];
					return new AsciiString(Encoding.ASCII.GetString(shifted.ToArray(), 0, (int)(length - 1)));
				}
			}

			return null;
		}

		private const int MAX_LENGTH = 4096;

	}

	// Simple unicode string, terminated by null char
	[Attr.Name("Wide string"), Attr.IconRef("Datatype.WideString.png")]
	public class WideString : DatatypeBase<WideString, string>
	{
		public WideString() : base() { }
		public WideString(string val) : base(val) { }

		public override string ToString()
		{
			if (_Value == null)
				return "<NULL>";
			if (_Value[0] == 0)
				return "<empty>";
			return "'" + _Value + "'";
		}

		public override long Length
		{
			get
			{
				long len = 0;
				if (_Value != null)
					len = _Value.Length + 1;
				return len;
			}
		}

		public override long LengthOf(IAccessor<byte> data, long offset)
		{
			return LengthOf_(data, offset);
		}

		public static int LengthOf_(IAccessor<byte> data, long offset)
		{
			int length = 0;

			// Scan up until null-terminator found, or max. allowed length exceeded
			while (length <= MAX_LENGTH)
			{
				if (offset + length > data.LongCount)
				{
					length = -1;
					break;
				}

				byte b1 = data[offset + length];
				byte b2 = data[offset + length + 1];

				if (!Helper.Analyzers.IsWideChar(b1, b2))
					break;

				length += 2;

				if (b1 == 0 && b2 == 0)
					break;
			}

			return length;
		}

		public override IDatatype FromData(IAccessor<byte> data, long offset)
		{
			return FromData_(data, offset, -1);
		}

		public static WideString FromData_(IAccessor<byte> data, long offset, long length)
		{
			if (offset < 0 || offset + length > data.LongCount)
				throw new IndexOutOfRangeException();

			if (length <= 0)
			{
				length = LengthOf_(data, offset);
				if (length > 0) // To comply with scale passed in: the no. of chars
					length >>= 1;
			}
			if (length > 0)
			{
				length <<= 1;
				if (length <= MAX_LENGTH && offset + length <= data.LongCount)
				{
					if (data[offset + length - 1] == 0 && data[offset + length - 2] == 0)
					{
						IAccessor<byte> shifted = data[offset, length];
						return new WideString(Encoding.Unicode.GetString(shifted.ToArray(), 0, (int)(length - 2)));
					}
				}
			}

			return null;
		}

		private const int MAX_LENGTH = 4096;

	}
	

	public static class Helpers
	{

		/// <summary>
		/// Converts data passed in into a string representation based on type given
		/// </summary>
		/// <param name="type">Type to use for conversion</param>
		/// <param name="data">Data used to form value</param>
		/// <param name="offset">Optional offset into data</param>
		/// <returns>String representation</returns>
		public static string ToString(Type type, IAccessor<byte> data, long offset = 0)
		{
			if (0 > offset || offset >= data.LongCount)
				throw new IndexOutOfRangeException();

			if (type == null)
				return "";

			if (data == null)
				return "<NULL>";

			try
			{
				IDatatype instance = Registry.Instance(type, data, offset);
				if (instance != null)
					return instance.ToString();
			}
			catch { }

			throw new ArgumentException(string.Format("Unknown type {0}", type));
		}

		/// <summary>
		/// Find length of required data based on type given
		/// </summary>
		/// <param name="type">Type to use</param>
		/// <param name="data">Data (in case type is of varying length like {Ansi|Wide|Var}String)</param>
		/// <param name="offset">Offset into data (in case type is of varying length like VarString)</param>
		/// <returns>Number of bytes for given type, or -1 if none found (or error occured)</returns>
		public static long LengthOf(Type type, IAccessor<byte> data, long offset)
		{
			if (data == null || data.LongCount == 0 || 
				offset < 0 || offset >= data.LongCount)
				return -1;

			if (type == null)
				return data.LongCount - offset;

			try
			{
				IDatatype instance = Registry.Instance(type, data, offset);
				if (instance != null)
					return instance.LengthOf(data, offset);
			}
			catch { }

			throw new ArgumentException(string.Format("Unknown type {0}", type));
		}
	}


	/// <summary>
	/// Global datatype registry
	/// </summary>
	internal static class Registry
	{

		/// <summary>
		/// Register new datatype
		/// </summary>
		/// <param name="name">Name of type</param>
		/// <param name="type">Type</param>
		internal static void Add(string name, Type type)
		{
			_Types.Add(name, type);
		}

		/// <summary>
		/// Checks if datatype is registered or not
		/// </summary>
		/// <param name="name">Name of type</param>
		/// <returns>Outcome</returns>
		internal static bool Has(string name)
		{
			if (name == null)
				return false;
			return _Types.ContainsKey(name);
		}

		/// <summary>
		/// Reverse name lookup by type
		/// </summary>
		/// <param name="type">Type to lookup</param>
		/// <returns>Registered name, or null if not registered</returns>
		internal static string Name(Type type)
		{
			if (_Types.ContainsValue(type))
				return _Types.First(pair => pair.Value == type).Key;
			return null;
		}

		/// <summary>
		/// Get registered datatype
		/// </summary>
		/// <param name="name">Name of type</param>
		/// <returns>Type, or null if not found</returns>
		internal static Type Get(string name)
		{
			Type type = null;
			if (name != null && !_Types.TryGetValue(name, out type))
				type = null;
			return type;
		}

		internal static IDatatype Instance(string name)
		{
			Type type = Get(name);
			return Activator.CreateInstance(type) as IDatatype;
		}

		internal static IDatatype Instance(Type type, IAccessor<byte> data, long offset, long length = 0)
		{
			if (type.GetInterface("IDatatype") == null)
			{
				// A base type was passed in, convert to wrapper
				Type org = type;
				type = Get(org.FullName);
				if (type == null)
					type = Get(org.Name);
			}

			IDatatype instance = null;

			try
			{
				// Favour static "FromData_" method
				MethodInfo method = type.GetMethod("FromData_", new Type[] { typeof(IAccessor<byte>), typeof(long) } );//, BindingFlags.Static);
				if (method != null)
				{
					if (method.GetParameters().Length == 2)
						instance = method.Invoke(null, new object[] { data, offset } ) as IDatatype;
					else
						instance = method.Invoke(null, new object[] { data, offset, length } ) as IDatatype;
				}
				else
				{
					// Favour static "Empty" field before creating temporary instance
					FieldInfo field = type.GetField("Empty");//, BindingFlags.Static);
					if (field != null)
						instance = field.GetValue(null) as IDatatype;
					else
						instance = Activator.CreateInstance(type) as IDatatype;

					if (instance != null)
						instance = instance.FromData(data, offset);
				}
			}
			catch
			{
				instance = null;
			}

			return instance;
		}


		static Registry()
		{
			Add("Boolean"		, typeof(SystemType.Wrapper<bool>));

			Add("AsciiChar"		, typeof(AsciiChar));
			Add("Char"			, typeof(SystemType.Wrapper<char>));

			Add("SByte"			, typeof(SystemType.Wrapper<sbyte>));
			Add("Byte"			, typeof(SystemType.Wrapper<byte>));

			Add("Int16"			, typeof(SystemType.Wrapper<short>));
			Add("UInt16"		, typeof(SystemType.Wrapper<ushort>));

			Add("Int32"			, typeof(SystemType.Wrapper<int>));
			Add("UInt32"		, typeof(SystemType.Wrapper<uint>));

			Add("Int64"			, typeof(SystemType.Wrapper<long>));
			Add("UInt64"		, typeof(SystemType.Wrapper<ulong>));

			Add("Single"		, typeof(SystemType.Wrapper<float>));
			Add("Double"		, typeof(SystemType.Wrapper<double>));

			Add("AsciiString"	, typeof(AsciiString));
			Add("WideString"	, typeof(WideString));
		}


		private static Dictionary<string, Type> _Types = new Dictionary<string, Type>();

	}

}
