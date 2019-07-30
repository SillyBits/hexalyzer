using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;


using Hexalyzer.Plugin;
using Attr = Hexalyzer.Plugin.Attributes;
using T = Hexalyzer.Datatypes;
using H = Hexalyzer.Helper;


/*
 * TODO:
 * 
 * 
 */


namespace UnrealEngine
{

	[Attr.Name("Unreal FString"), Attr.IconRef("Datatype.FString.png")]
	public class FString : T.DatatypeBase<FString, string>
	{

		public FString() 
			: base() 
		{
			_Source = null;
		}

		public FString(T.AsciiString str) 
			: base(str?.Value as string)
		{
			_Source = Source.Ascii;
		}

		public FString(T.WideString str) 
			: base(str?.Value as string)
		{
			_Source = Source.UCS2;
		}


		public override string ToString()
		{
			if (_Value == null)
				return "<NULL>";
			return "'" + _Value + "'";
		}


		// IDatatype
		//

		public override long Length
		{
			get
			{
				long len = 0;
				if (_Value != null)
					len = _Value.Length + 1 + 4;
				return len;
			}
		}

		public override bool IsValid(Hexalyzer.IAccessor<byte> data, long offset)
		{
			return IsValid_(data, offset);
		}

		public static bool IsValid_(Hexalyzer.IAccessor<byte> data, long offset)
		{
			if (offset + 4 > data.Count)
				return false;

			long len = T.Helpers.ToInt32(data, offset);
			long ofs = offset + 4;

			if (len < 0)
			{
				len = -len;
				if (ofs + (len * 2) <= data.Count)
					return H.Analyzers.IsWideString(data, ofs, len);
			}
			else if (len > 0)
			{
				if (ofs + len <= data.Count)
					return H.Analyzers.IsAsciiString(data, ofs, len);
			}

			return false;
		}

		public override long LengthOf(Hexalyzer.IAccessor<byte> data, long offset)
		{
			return LengthOf_(data, offset);
		}

		public static long LengthOf_(Hexalyzer.IAccessor<byte> data, long offset)
		{
			long len = T.Helpers.ToInt32(data, offset);

			if (len == 0)
				return -1;

			if (len < 0)
				len = (-len * 2) + 4;
			else
				len += 4;

			if (offset + len > data.Count)
				len = -1;

			return len;
		}

		public override IDatatype FromData(Hexalyzer.IAccessor<byte> data, long offset)
		{
			return FromData_(data, offset);
		}

		public static IDatatype FromData_(Hexalyzer.IAccessor<byte> data, long offset)
		{
			if (offset + 4 > data.Count)
				return null;

			int len = T.Helpers.ToInt32(data, offset);
			long ofs = offset + 4;

			if (len < 0)
			{
				len = -len;
				T.WideString wstr = T.WideString.FromData_(data, ofs, len);
				if (wstr != null)
					return new FString(wstr);
			}
			else if (len > 0)
			{
				T.AsciiString astr = T.AsciiString.FromData_(data, ofs, len);
				if (astr != null)
					return new FString(astr);
			}

			return null;
		}

		internal enum Source { Ascii, UCS2 };
		internal Source? _Source;

	}


	[Attr.Name("Unreal FString with hash"), Attr.IconRef("Datatype.FStringWithHash.png")]
	public class FStringWithHash : T.DatatypeBase<FStringWithHash, FStringWithHash.Data>
	{
		public struct Data
		{
			public FString String { get; internal set; }
			public uint Hash { get; internal set; }
		}


		public FStringWithHash() : base() { }

		public FStringWithHash(FString str, uint hash)
			: base(new Data() { String = str, Hash = hash, })
		{ }


		public override string ToString()
		{
			return _Value.String.ToString() + "|" + _Value.Hash.ToString("X8") + "h";
		}


		// IDatatype
		//

		public override long Length
		{
			get
			{
				long len = 0;
				if (_Value.String != null)
					len = _Value.String.Length + 1;
				if (len > 0)
					len += 4; //uint:Hash
				return len;
			}
		}


		public override bool IsValid(Hexalyzer.IAccessor<byte> data, long offset)
		{
			return IsValid_(data, offset);
		}

		public static bool IsValid_(Hexalyzer.IAccessor<byte> data, long offset)
		{
			if (!FString.IsValid_(data, offset))
				return false;
			long len = LengthOf_(data, offset);
			return (len > 0);
		}

		public override long LengthOf(Hexalyzer.IAccessor<byte> data, long offset)
		{
			return LengthOf_(data, offset);
		}

		public static long LengthOf_(Hexalyzer.IAccessor<byte> data, long offset)
		{
			long len = FString.LengthOf_(data, offset);
			if (len > 0)
				len += 4;// uint:Hash
			if (offset + len > data.Count)
				len = -1;
			return len;
		}

		public override IDatatype FromData(Hexalyzer.IAccessor<byte> data, long offset)
		{
			return FromData_(data, offset);
		}

		public static IDatatype FromData_(Hexalyzer.IAccessor<byte> data, long offset)
		{
			FString str = FString.FromData_(data, offset) as FString;
			if (str == null)
				return null;

			offset += FString.LengthOf_(data, offset);
			if (offset + 4 > data.Count)
				return null;

			uint hash = T.Helpers.ToUInt32(data, offset);
			return new FStringWithHash(str, hash);
		}

	}

}
