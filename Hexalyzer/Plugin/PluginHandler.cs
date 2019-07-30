using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace Hexalyzer.Plugin
{

	internal static class PluginHandler
	{

		internal static void Load()
		{
			string dir = Config.Root.core.pluginpath;
			Debug.WriteLine("Enumerating plugins in '{0}'", dir);

			foreach (string file in Directory.EnumerateFiles(dir, "*.dll"))
			{
				Assembly ass = Assembly.LoadFile(file);
				Debug.WriteLine("- " + ass.FullName);

				Type[] types = ass.GetTypes();
				foreach (Type type in types)
				{
					Type[] interfaces = type.FindInterfaces((t, o) => t == typeof(IPlugin), null);
					if (interfaces.Length > 0)
					{
						Debug.WriteLine("  => IPlugin found, registering");
						//foreach (Type t in interfaces)
						//	Debug.WriteLine("    - " + t.FullName);
						try
						{
							IPlugin plugin = Activator.CreateInstance(type) as IPlugin;
							if (plugin == null)
							{
								string msg = string.Format("Unable to create instance for {0}|{1}!",
											Path.GetFileName(file), type.FullName);
								Debug.Fail(msg);
								break;
							}
							Register(type.Name, plugin);
						}
						catch (Exception exc)
						{
							string msg = string.Format("Failed to register plugin {0}|{1}:\n",
											Path.GetFileName(file), type.FullName)
										+ exc.Message.ToString() + "\n"
										+ exc.Source.ToString() + "\n"
										+ exc.StackTrace.ToString() + "\n"
										;
							Debug.Fail(msg);
							break;
						}
					}
				}
			}

			Debug.WriteLine("Done enumerating, loaded a total of {0} plugins", Plugins.Count);
		}

		private static void Register(string name, IPlugin plugin)
		{
			Plugins.Add(name, plugin);

			// Register all datatypes
			Type[] datatypes = plugin.GetSupportedDatatypes();
			foreach (Type datatype in datatypes)
			{
				Datatypes.Registry.Add(datatype.FullName, datatype);
			}
		}

		internal static void AddToUI(MainWindow wnd)
		{
			_Wnd = wnd;

			bool resource_separator = true, view_separator = true, tool_separator = true;
			MenuItem menu, submenu;
			ToolBar toolbar;
			ContentControl tb_button;

			foreach (KeyValuePair<string, IPlugin> pair in Plugins)
			{
				string plugin_name = pair.Key.Replace('.', '_');
				IPlugin plugin = pair.Value;
				toolbar = null;

				Type[] datatypes = plugin.GetSupportedDatatypes();
				if (datatypes != null && datatypes.Length > 0)
				{
					menu = wnd.AddCustomMenuItem(wnd.Resource_Menu, plugin_name + "_Datatype", plugin.Name, plugin.Icon,
						false, null, resource_separator);
					menu.Tag = new Info(typeof(IPlugin), plugin);
					resource_separator = false;

					foreach (Type datatype in datatypes)
					{
						string dt_name = plugin_name + "_" + datatype.Name;
						string name = Attributes.Name.Get(datatype);
						ImageSource icon = Attributes.IconRef.Get(datatype);

						IDatatype dt_inst = Activator.CreateInstance(datatype) as IDatatype;
						if (dt_inst == null)
						{
							string msg = string.Format("Unable to create instance for {0}|{1}!",
										plugin.Name, datatype.FullName);
							Debug.Fail(msg);
						}
						else
						{
							Info info = new Info(typeof(IDatatype), datatype);

							submenu = wnd.AddCustomMenuItem(menu, dt_name, name/*dt_inst.Name*/, icon/*dt_inst.Icon*/, 
								false, _OnMenuItem_Click, false);
							submenu.Tag = info;

							if (icon/*dt_inst.Icon*/ != null)
							{
								if (toolbar == null)
									toolbar = wnd.AddCustomToolbar(plugin_name + "_TB");
								tb_button = wnd.AddCustomToolbarButton(toolbar, dt_name, name/*dt_inst.Name*/, icon/*dt_inst.Icon*/, 
									false, _OnToolbar_Click, false);
								tb_button.Tag = info;
							}

						}
					}
				}

				Type[] panels = plugin.GetSupportedPanels();
				if (panels != null && panels.Length > 0)
				{
					menu = wnd.AddCustomMenuItem(wnd.View_Menu, plugin_name + "_View", plugin.Name, plugin.Icon,
						false, null, view_separator);
					menu.Tag = plugin;
					view_separator = false;

					foreach (Type panel in panels)
					{
						//IDatatype dt_inst = Activator.CreateInstance(panel) as IDatatype;
						//if (dt_inst == null)
						//{
						//	string msg = string.Format("Unable to create instance for {0}|{1}!", 
						//				plugin.Name, datatype.FullName);
						//	Debug.Fail(msg);
						//}
						//else
						//{
						//	Info info = new Info(typeof(IPanel), panel);
						//
						//	wnd.AddCustomMenuItem(menu, datatype.Name, name/*dt_inst.Name*/, icon/*dt_inst.Icon*/, 
						//		false, _OnMenuItem_Click, false);
						//	submenu.Tag = info;
						//
						//	if (icon/*dt_inst.Icon*/ != null)
						//	{
						//		if (toolbar == null)
						//			toolbar = wnd.AddCustomToolbar(name + "_TB");
						//		tb_button = wnd.AddCustomToolbarButton(toolbar, dt_name, name/*dt_inst.Name*/, icon/*dt_inst.Icon*/, 
						//			false, _OnToolbar_Click, false);
						//		tb_button.Tag = info;
						//	}
						//
						//	_Panels.Add(panel.FullName, panel);
						//}
					}
				}

				Type[] tools = plugin.GetSupportedTools();
				if (tools != null && tools.Length > 0)
				{
					menu = wnd.AddCustomMenuItem(wnd.Tools_Menu, plugin_name + "_Tool", plugin.Name, plugin.Icon,
						false, null, tool_separator);
					menu.Tag = plugin;
					tool_separator = false;

					foreach (Type tool in tools)
					{
						//IDatatype dt_inst = Activator.CreateInstance(panel) as IDatatype;
						//if (dt_inst == null)
						//{
						//	string msg = string.Format("Unable to create instance for {0}|{1}!", 
						//				plugin.Name, datatype.FullName);
						//	Debug.Fail(msg);
						//}
						//else
						//{
						//	Info info = new Info(typeof(ITool), tool);
						//
						//	wnd.AddCustomMenuItem(menu, datatype.Name, name/*dt_inst.Name*/, icon/*dt_inst.Icon*/, 
						//		false, _OnMenuItem_Click, false);
						//	submenu.Tag = info;
						//
						//	if (icon/*dt_inst.Icon*/ != null)
						//	{
						//		if (toolbar == null)
						//			toolbar = wnd.AddCustomToolbar(name + "_TB");
						//		tb_button = wnd.AddCustomToolbarButton(toolbar, dt_name, name/*dt_inst.Name*/, icon/*dt_inst.Icon*/, 
						//			false, _OnToolbar_Click, false);
						//		tb_button.Tag = info;
						//	}
						//
						//	_Tools.Add(tool.FullName, tool);
						//}
					}
				}

				//Type[] analyzers = plugin.GetSupportedAnalyzers();
				//...
			}
		}


		internal static bool HasPlugin(string plugin)
		{
			return Plugins.ContainsKey(plugin);
		}

		internal static IPlugin GetPlugin(string plugin)
		{
			IPlugin instance = null;
			if (!Plugins.TryGetValue(plugin, out instance))
				instance = null;
			return instance;
		}


		private static void _OnMenuItem_Click(object sender, RoutedEventArgs e)
		{
			Info info = (sender as MenuItem).Tag as Info;
			_Handle(info);
		}

		private static void _OnToolbar_Click(object sender, RoutedEventArgs e)
		{
			Info info = (sender as ContentControl).Tag as Info;
			_Handle(info);
		}

		private static void _Handle(Info info)
		{
			if (info.Type == typeof(IPlugin))
			{

			}
			else if (info.Type == typeof(IDatatype))
			{
				_Wnd.PrjView.AddResource(info.Instance as Type);
			}
			else if (info.Type == typeof(IPanel))
			{
				//TODO: Show/hide panel
			}
			else if (info.Type == typeof(ITool))
			{
				//TODO: Start/stop tool
			}
			//else
			//{
			//	//ERROR
			//}
		}


		static PluginHandler()
		{
			Plugins = new Dictionary<string, IPlugin>();

			_Panels    = new Dictionary<string, Type>();
			_Views     = new Dictionary<string, Type>();
			_Analyzers = new Dictionary<string, Type>();
		}


		private class Info
		{
			internal Type Type;
			internal object Instance;
			internal object Data;

			internal Info(Type obj, object inst, object data = null)
			{
				Type = obj;
				Instance = inst;
				Data = data;
			}
		}


		internal static Dictionary<string, IPlugin> Plugins;
		internal static MainWindow _Wnd;
		internal static Dictionary<string, Type> _Panels;
		internal static Dictionary<string, Type> _Views;
		internal static Dictionary<string, Type> _Analyzers;

	}

}


// Attributes
//
namespace Hexalyzer.Plugin.Attributes
{ 

	[AttributeUsage(AttributeTargets.Class)]
	public class AttrBase : Attribute
	{
		public object Value { get; protected set; }

		internal static bool Has<_Attr>(Type type)
			where _Attr : AttrBase
		{
			return (type.GetCustomAttribute<_Attr>(false) != null);
		}

		internal static _Type Get<_Attr,_Type>(Type type)
			where _Attr : AttrBase
		{
			_Attr attr = type.GetCustomAttribute<_Attr>(false);
			return (_Type)(attr?.Value);
		}
	}

	public class StringAttr : AttrBase
	{
		internal static string Get<_Attr>(Type type)
			where _Attr : StringAttr
		{
			return Get<_Attr, string>(type);
		}
	}

	public class LongAttr : AttrBase
	{
		internal static long Get<_Attr>(Type type)
			where _Attr : LongAttr
		{
			return Get<_Attr, long>(type);
		}
	}


	public class Name : StringAttr
	{
		public Name(string name)
		{
			Value = name;
		}

		internal static string Get(Type type)
		{
			return Get<Name>(type);
		}
	}

	public class IconRef : AttrBase
	{
		public IconRef(string icon)
		{
			Value = icon;
		}

		internal static ImageSource Get(Type type)
		{
			ImageSource icon = null;

			try
			{
				string name = Get<IconRef, string>(type);
				if (name != null)
				{
					Assembly ass = Assembly.GetAssembly(type);
					if (ass != null)
					{
						string path = string.Format("pack://application:,,,/{0};component/Resources/{1}",
							ass.GetName(), name);
						Uri uri = new Uri(path);
						icon = new BitmapImage(uri);
					}
				}
			}
			catch
			{
				icon = null;
			}

			return icon;
		}
	}

}
