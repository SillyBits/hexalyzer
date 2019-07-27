using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;


using Hexalyzer.Plugin;
using Hexalyzer.Datatypes;
using Hexalyzer.Helper;


namespace UnrealEngine
{

	public class Plugin : IPlugin
	{

		public string Name
		{
			get { return "Unreal engine"; }
		}

		public ImageSource Icon
		{
			get { return null; }
		}


		public Type[] GetSupportedDatatypes()
		{
			return _Datatypes;
		}

		public Type[] GetSupportedPanels()
		{
			return _Panels;
		}

		public Type[] GetSupportedTools()
		{
			return _Tools;
		}

		public Type[] GetSupportedAnalyzers()
		{
			return _Analyzers;
		}


		private static Type[] _Datatypes = new Type[] { typeof(UEStrWithHash), };
		private static Type[] _Panels    = null;//new Type[] { };
		private static Type[] _Tools     = null;//new Type[] { };
		private static Type[] _Analyzers = null;//new Type[] { };

	}


	public class UEStrWithHash : IDatatype
	{

		public string String { get; private set; }
		public uint Hash { get; private set; }


		public UEStrWithHash()
		{
		}

		public UEStrWithHash(AsciiString str, uint hash)
		{
			String = str.Value;
			Hash = hash;
		}

		public UEStrWithHash(WideString str, uint hash)
		{
			String = str.Value;
			Hash = hash;
		}

		public UEStrWithHash(VarString str, uint hash)
		{
			String = str.Value;
			Hash = hash;
		}


		public override string ToString()
		{
			return "'" + String + "'|" + Hash.ToString("X8") + "h";
		}


		// IDatatype
		//

		public string Name
		{
			get { return "Unreal string with 32bit hash"; }
		}

		public ImageSource Icon
		{
			get
			{
				if (_Icon == null)
				{
					Uri uri = new Uri("pack://application:,,,/UnrealEngine.Plugin;component/Resources/Datatype.UEStrWithHash.png");
					_Icon = new BitmapImage(uri);
					if (_Icon != null)
						_Icon.Freeze();
				}
				return _Icon;
			}
		}

		public IDatatype Empty
		{
			get { return _INSTANCE; }
		}


		public bool IsValid(IReadOnlyList<byte> data, long offset)
		{
			if (offset + 4 > data.Count)
				return false;

			int len = Helpers.ToInt32(data, (int)offset);
			if (len == 0)
				return false;

			long ofs = offset + 4;

			if (len < 0)
			{
				len = -len;
				if (Analyzers.IsWideString(data, ofs, len))
				{
					if (ofs + (len * 2) + 4 <= data.Count)
						return true;
				}
			}
			if (Analyzers.IsAsciiString(data, ofs, len))
			{
				if (ofs + len + 4 <= data.Count)
					return true;
			}

			if (Analyzers.IsWideString(data, ofs, len))
			{
				if (ofs + (len * 2) + 4 <= data.Count)
					return true;
			}

			return false;
		}

		public long LengthOf(IReadOnlyList<byte> data, long offset)
		{
			int len = Helpers.ToInt32(data, offset);
			if (len < 0)
				len = -len;
			len += 4 + 4;// int:Length + uint:Hash
			if (offset + len > data.Count)
				len = -1;
			return len;
		}

		public IDatatype FromData(IReadOnlyList<byte> data, long offset)
		{
			if (offset + 4 > data.Count)
				return null;

			int len = Helpers.ToInt32(data, (int)offset);
			if (len == 0)
				return null;

			if (len < 0)
			{
				WideString wstr = WideString.FromData(data, offset);
				if (wstr == null)
					return null;
				offset += 4;
				offset += (wstr.Value.Length + 1) * 2;
				uint hash = Helpers.ToUInt32(data, offset);
				return new UEStrWithHash(wstr, hash);
			}

			AsciiString astr = AsciiString.FromData(data, offset);
			if (astr != null)
			{
				offset += 4;
				offset += astr.Value.Length + 1;
				uint hash = Helpers.ToUInt32(data, offset);
				return new UEStrWithHash(astr, hash);
			}

			VarString vstr = VarString.FromData(data, offset);
			if (vstr != null)
			{
				offset += 4;
				offset += (vstr.Value.Length + 1) * 2;
				uint hash = Helpers.ToUInt32(data, offset);
				return new UEStrWithHash(vstr, hash);
			}

			return null;
		}


		private static ImageSource _Icon;
		private static UEStrWithHash _INSTANCE = new UEStrWithHash();

	}

}
