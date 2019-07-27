using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;


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
					Type[] interfaces = type.FindInterfaces((t,o) => t == typeof(IPlugin), null);
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
							Plugins.Add(type.Name, plugin);
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


		internal static void AddToUI(MainWindow wnd)
		{
			_Wnd = wnd;

			bool resource_separator = true, view_separator = true, tool_separator = true;
			MenuItem menu, submenu;
			ToolBar toolbar;
			ContentControl tb_button;

			foreach(KeyValuePair<string,IPlugin> pair in Plugins)
			{
				string name = pair.Key;//plugin.GetType().Name;
				IPlugin plugin = pair.Value;

				Type[] datatypes = plugin.GetSupportedDatatypes();
				if (datatypes != null && datatypes.Length > 0)
				{
					menu = wnd.AddCustomMenuItem(wnd.Resource_Menu, name + "_Datatype", plugin.Name, plugin.Icon, 
						false, null, resource_separator);
					menu.Tag = new Info(typeof(IPlugin), plugin);
					resource_separator = false;
					toolbar = null;

					foreach (Type datatype in datatypes)
					{
						string dt_name = name + "_" + datatype.Name;

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

							submenu = wnd.AddCustomMenuItem(menu, dt_name, dt_inst.Name, dt_inst.Icon, false, _OnMenuItem_Click, false);
							submenu.Tag = info;

							if (dt_inst.Icon != null)
							{
								if (toolbar == null)
									toolbar = wnd.AddCustomToolbar(name + "_TB");
								tb_button = wnd.AddCustomToolbarButton(toolbar, dt_name, dt_inst.Name, dt_inst.Icon, false, _OnToolbar_Click, false);
								tb_button.Tag = info;
							}

							_Datatypes.Add(datatype.FullName, datatype);
						}
					}
				}

				Type[] panels = plugin.GetSupportedPanels();
				if (panels != null && panels.Length > 0)
				{
					menu = wnd.AddCustomMenuItem(wnd.View_Menu, name + "_View", plugin.Name, plugin.Icon, 
						false, null, view_separator);
					menu.Tag = plugin;
					view_separator = false;
					toolbar = null;

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
						//	wnd.AddCustomMenuItem(menu, datatype.Name, dt_inst.Name, dt_inst.Icon, false, _OnMenuItem_Click, false);
						//	submenu.Tag = info;
						//
						//	if (dt_inst.Icon != null)
						//	{
						//		if (toolbar == null)
						//			toolbar = wnd.AddCustomToolbar(name + "_TB");
						//		tb_button = wnd.AddCustomToolbarButton(toolbar, dt_name, dt_inst.Name, dt_inst.Icon, false, _OnToolbar_Click, false);
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
					menu = wnd.AddCustomMenuItem(wnd.Tools_Menu, name + "_Tool", plugin.Name, plugin.Icon, 
						false, null, tool_separator);
					menu.Tag = plugin;
					tool_separator = false;
					toolbar = null;

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
						//	wnd.AddCustomMenuItem(menu, datatype.Name, dt_inst.Name, dt_inst.Icon, false, _OnMenuItem_Click, false);
						//	submenu.Tag = info;
						//
						//	if (dt_inst.Icon != null)
						//	{
						//		if (toolbar == null)
						//			toolbar = wnd.AddCustomToolbar(name + "_TB");
						//		tb_button = wnd.AddCustomToolbarButton(toolbar, dt_name, dt_inst.Name, dt_inst.Icon, false, _OnToolbar_Click, false);
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

		internal static Type GetDatatype(string datatype)
		{
			Type type = null;
			if (!_Datatypes.TryGetValue(datatype, out type))
				type = null;
			return type;
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

			_Datatypes = new Dictionary<string, Type>();
			_Panels    = new Dictionary<string, Type>();
			_Views     = new Dictionary<string, Type>();
			_Analyzers = new Dictionary<string, Type>();
		}


		private class Info
		{
			internal Type Type;
			internal object Instance;
			internal object Data;

			internal Info(Type obj, object inst)
			{
				Type = obj;
				Instance = inst;
			}
		}


		internal static Dictionary<string, IPlugin> Plugins;
		internal static MainWindow _Wnd;
		internal static Dictionary<string, Type> _Datatypes;
		internal static Dictionary<string, Type> _Panels;
		internal static Dictionary<string, Type> _Views;
		internal static Dictionary<string, Type> _Analyzers;

	}

}
