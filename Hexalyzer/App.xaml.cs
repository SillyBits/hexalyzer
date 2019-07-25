#define DEVENV


using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;


/*
 * TODO:
 * 
 */


namespace Hexalyzer
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{

		protected ConfigFile _config;


		protected override void OnStartup(StartupEventArgs e)
		{
			_config = new ConfigFile(Settings.APP_PATH, Settings.APP_NAME);

			if (!Config.Root.core.HasItem("defaultpath") || Config.Root.core.defaultpath == "")
			{
#if DEVENV
				Config.Root.core.defaultpath = Path.GetFullPath(Path.Combine(Settings.APP_PATH, ".."));
#else
				Config.Root.core.defaultpath = Settings.APP_PATH;
#endif
			}

			Settings.Renew();

			base.OnStartup(e);
		} 

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);

			_config.Shutdown();
			_config = null;
		}

	}


	public static class Settings
	{
		#region Core
		public const string APP_NAME = "Hexalyzer";

		public static string APP_VERSION
		{
			get
			{
				if (_AppVersion == null)
				{
					Assembly ass = Assembly.GetExecutingAssembly();
					var versions = ass.GetCustomAttributes<AssemblyInformationalVersionAttribute>() 
								   as AssemblyInformationalVersionAttribute[];
					AssemblyInformationalVersionAttribute attr = versions.FirstOrDefault();
					if (attr != null)
					{
						_AppVersion = attr.InformationalVersion;
					}
					else
					{
						var vers = FileVersionInfo.GetVersionInfo(ass.Location);
						_AppVersion = vers.ProductVersion;
					}
				}

				return _AppVersion;
			}
		}

		public static string APP_PATH
		{
			get
			{
				if (_AppPath == null)
				{
					Assembly ass = Assembly.GetExecutingAssembly();
					Module module = ass.ManifestModule;
					_AppPath = Path.GetDirectoryName(module.FullyQualifiedName);
#if DEVENV
					_AppPath = Path.Combine(_AppPath, ".."); // <- "bin"
#  if DEBUG
					_AppPath = Path.Combine(_AppPath, ".."); // <- "Debug"
#  else
					_AppPath = Path.Combine(_AppPath, ".."); // <- "Release"
#  endif
#endif
					_AppPath = Path.GetFullPath(_AppPath);
				}

				return _AppPath;
			}
		}

		public const string RESOURCES = "Resources";
		public static string RESOURCE_PATH
		{
			get
			{
				if (_ResourcePath == null)
					_ResourcePath = Path.Combine(APP_PATH, RESOURCES);

				return _ResourcePath;
			}
		}


#if DEVENV
		public static string DEFAULT_FILE;
#endif
		#endregion

		#region Layout
		/*
		 * Example layout for 16 bytes per row:
		 * 
		 *		         11111111112222222222333333333344444444445
		 *		12345678901234567890123456789012345678901234567890
		 *		--------------------------------------------------
		 *		00 00 00 00  00 00 00 00  38 03 00 00  05 00 00 00
		 *		|---11----|..|
		 *		|-----------------------50-----------------------|
		 */

		/// <summary>
		/// Format string used for offset column
		/// </summary>
		public static string OFFSET_FORMAT = "X8";

		/// <summary>
		/// Number of bytes per row
		/// </summary>
		public static int BYTES_PER_ROW = 16;

		/// <summary>
		/// Number of bytes per column group
		/// (used with hex column)
		/// </summary>
		public static int BYTES_PER_COL = 4;

		/// <summary>
		/// Number of blanks for column spacing
		/// </summary>
		public static int COL_SEP_CHARS = 2;

		/// <summary>
		/// Number of newline chars needed for reaching next line
		/// (depends on actual control used internally)
		/// </summary>
		//[Obsolete]
		public static int NEWLINE_CHARS = 1;

		/// <summary>
		/// FOr isolating offset aligned to no. of bytes per row
		/// (shortcut for heavily used "offset - (offset % BYTES_PER_ROW)")
		/// </summary>
		public static long OFFSET_MASK = ~(BYTES_PER_ROW - 1);

		/// <summary>
		/// Columns per row
		/// (used with hex column)
		/// </summary>
		public static int COLS_PER_ROW;

		/// <summary>
		/// Characters per column 
		/// (used with hex column)
		/// </summary>
		public static int CHARS_PER_COL;

		/// <summary>
		/// Characters per row
		/// (used with hex column)
		/// </summary>
		public static int CHARS_PER_ROW;

		/// <summary>
		/// Column spacer string
		/// (used with hex column)
		/// </summary>
		public static string COL_SPACER;
		#endregion

		#region Font
		/// <summary>
		/// Font used for display
		/// </summary>
		public static string FONT_FAMILY { get; internal set; }

		/// <summary>
		/// Font size for display
		/// </summary>
		public static int FONT_SIZE { get; internal set; }

		/// <summary>
		/// Cached Typeface instance for selected font
		/// (using normal style, weight and stretch)
		/// </summary>
		public static Typeface FONT_TYPEFACE;
		#endregion


		/// <summary>
		/// Renew settings by reading from actual config file
		/// </summary>
		internal static void Renew()
		{
#if DEVENV
			DEFAULT_FILE = Path.GetFullPath(Path.Combine(APP_PATH, "..", "__internal", "AssemblerMk1_256.uasset.hexaproj"));
#endif

			OFFSET_FORMAT = Config.Root.view.offset_format;
			BYTES_PER_ROW = Config.Root.view.bytes_per_row;
			BYTES_PER_COL = Config.Root.view.bytes_per_col;
			COL_SEP_CHARS = Config.Root.view.col_separator;
			OFFSET_MASK   = ~(BYTES_PER_ROW - 1);

			COLS_PER_ROW  = BYTES_PER_ROW / BYTES_PER_COL;
			CHARS_PER_COL = (BYTES_PER_COL * 3) - 1;
			CHARS_PER_ROW = (CHARS_PER_COL * COLS_PER_ROW) + (COL_SEP_CHARS * (COLS_PER_ROW - 1));
			COL_SPACER    = (new StringBuilder(COL_SEP_CHARS)).Append(' ', COL_SEP_CHARS).ToString();

			FONT_FAMILY   = Config.Root.view.font.family;
			FONT_SIZE     = Config.Root.view.font.size;
			FONT_TYPEFACE = new Typeface(new FontFamily(FONT_FAMILY), FontStyles.Normal, 
										 FontWeights.Normal, FontStretches.Normal);
		}


		private static string _AppVersion;
		private static string _AppPath;
		private static string _ResourcePath;

	}

}
