using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;


using Hexalyzer.Plugin;


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


		internal static Uri GetResourceUri(string name)
		{
			return new Uri("pack://application:,,,/UnrealEngine.Plugin;component/Resources/" + name);
		}

		internal static ImageSource GetImageResource(string name)
		{
			ImageSource img = new BitmapImage(GetResourceUri(name));
			if (img != null)
				img.Freeze();
			return img;
		}


		private static Type[] _Datatypes = new Type[] { typeof(FString), typeof(FStringWithHash), };
		private static Type[] _Panels    = null;//new Type[] { };
		private static Type[] _Tools     = null;//new Type[] { };
		private static Type[] _Analyzers = null;//new Type[] { };

	}

}
