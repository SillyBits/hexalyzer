﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;


namespace Hexalyzer
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{

		public MainWindow()
		{
			Loaded += _Loaded;

			InitializeComponent();

			_Init();
		}


		protected void _Init()
		{
			// - Position window
			if (Config.Root.HasSection("window"))
			{
				if (Config.Root.window.HasItem("state") 
					&& Config.Root.window.state == WindowState.Maximized.ToString())
				{
					WindowState = WindowState.Maximized;
				}
				else
				{
					if (Config.Root.window.HasItem("pos_x") && Config.Root.window.pos_x != -1)
					{
						Left   = Config.Root.window.pos_x;
						Top    = Config.Root.window.pos_y;
						Width  = Config.Root.window.size_x;
						Height = Config.Root.window.size_y;
					}
					else
					{
						WindowStartupLocation = WindowStartupLocation.CenterScreen;
					}
				}
			}

			_SetupMRU();

			Plugin.PluginHandler.Load();
			Plugin.PluginHandler.AddToUI(this);

			UpdateUI();

			PrjView.OffsetChanged += PrjView_OffsetChanged;
			PrjView.SelectionChanged += PrjView_SelectionChanged;
		}

		public void UpdateUI()
		{
			bool has_prj   = (CurrentProject != null);
			bool is_mod    = has_prj && CurrentProject.IsModified;
			bool has_input = has_prj && (PrjView.Selection >= 0);

			string title = "Hexalyzer";
			if (has_prj)
			{
				title += string.Format(" - {0} ({1})", CurrentProject.Project, Path.GetFileName(CurrentProject.Filename));
				if (is_mod)
					title += " [modified]";
			}
			Title = title;

			Action<object,bool> enable = (obj,state) => {
				if (obj is MenuItem)
				{
					(obj as MenuItem).IsEnabled = state;
				}
				else if (obj is Button)
				{
					Button btn = obj as Button;
					btn.IsEnabled = state;
					btn.Opacity = state ? 1 : 0.5;
				}
				// Other types like separators are just ignored
			};

			enable(File_Save, is_mod);
			enable(File_Save_TB, is_mod);
			enable(File_SaveAs, is_mod);
			enable(File_Close, has_prj);
			enable(File_Close_TB, has_prj);

			enable(Resource_Menu, has_input);
			foreach (var item in Resource_ToolBar.Items)
				enable(item, has_input);

			//enable(View_Menu, has_prj);
			//foreach (var item in View_ToolBar.Items)
			//	enable(item, has_prj);

			enable(Tools_Menu, has_input);
			foreach (var item in Tools_ToolBar.Items)
				enable(item, has_input);
		}


		#region UI customizers
		public void AddCustomDatatype(string name, string title, string icon, RoutedEventHandler handler, bool add_separator = false)
		{
			AddCustomEntry(Resource_Menu, Resource_ToolBar, name, title, icon, false, handler, add_separator);
		}

		public void AddCustomView(string name, string title, string icon, RoutedEventHandler handler, bool add_separator = false)
		{
			AddCustomEntry(View_Menu, View_ToolBar, name, title, icon, true, handler, add_separator);
		}

		public void AddCustomTool(string name, string title, string icon, RoutedEventHandler handler, bool add_separator = false)
		{
			AddCustomEntry(Tools_Menu, Tools_ToolBar, name, title, icon, false/*???*/, handler, add_separator);
		}

		public void AddCustomEntry(MenuItem menu, ToolBar toolbar, string name, string title, string icon, bool checkable, 
			RoutedEventHandler handler, bool add_separator)
		{
			BitmapImage img = null;
			if (!string.IsNullOrEmpty(icon))
			{
				img = new BitmapImage(new Uri("pack://application:,,,/Resources/" + icon));
				img.Freeze();
			}

			AddCustomEntry(menu, toolbar, name, title, img, checkable, handler, add_separator);
		}

		public void AddCustomEntry(MenuItem menu, ToolBar toolbar, string name, string title, ImageSource icon, bool checkable,
			RoutedEventHandler handler, bool add_separator)
		{
			if (menu != null)
				AddCustomMenuItem(menu, name, title, icon, checkable, handler, add_separator);

			if (toolbar != null)
				AddCustomToolbarButton(toolbar, name, title, icon, checkable, handler, add_separator);
		}

		public MenuItem AddCustomMenuItem(MenuItem menu, string name, string title, ImageSource icon, bool checkable,
			RoutedEventHandler handler, bool add_separator)
		{
			if (add_separator)
				menu.Items.Add(new Separator());

			MenuItem menuitem = new MenuItem() {
				Name = name + "_Menu",
				Header = title,
				IsCheckable = checkable,
			};

			if (icon != null)
				menuitem.Icon = new Image() { Source = icon };

			if (handler != null)
				menuitem.Click += handler;

			menu.Items.Add(menuitem);
			
			return menuitem;
		}

		
		public ToolBar AddCustomToolbar(string name)
		{
			int index = 0;
			var band_2 = toolbars
				.ToolBars
				.Where(tb => tb.Band==2)
				;
			if (band_2.Count() > 0)
				index = band_2
					.Select(tb => tb.BandIndex)
					.Max()
					;

			ToolBar toolbar = new ToolBar() {
				Band = 2,
				BandIndex = index + 1,
			};

			toolbars.ToolBars.Add(toolbar);

			return toolbar;
		}

		public ContentControl AddCustomToolbarButton(ToolBar toolbar, string name, string title, ImageSource icon, bool checkable,
			RoutedEventHandler handler, bool add_separator)
		{
			if (add_separator)
				toolbar.Items.Add(new Separator());

			ButtonBase button;
			if (checkable)
			{
				button = new ToggleButton() {
					Name = name + "_TB",
					ToolTip = title,
					IsThreeState = false,
					IsEnabled = true,
				};
			}
			else
			{
				button = new Button() {
					Name = name + "_TB",
					ToolTip = title,
				};
			}

			if (icon != null)
				button.Content = new Image() { Source = icon };

			if (handler != null)
				button.Click += handler;

			toolbar.Items.Add(button);

			return button;
		}
		#endregion


		// Non-public members following
		//

		private void _Loaded(object sender, EventArgs e)
		{
#if DEVENV
			AddCustomView("View_DataBufferInfo", "Data buffer info", "Toolbar.View.DataBufferInfo.png", View_DataBufferInfo);
			AddCustomView("View_ProjectViewInfo", "Project view info", "Toolbar.View.ProjectViewInfo.png", View_ProjectViewInfo);
#endif

		}

		protected override void OnClosing(CancelEventArgs e)
		{
			_CloseProject();

			// Save window state
			Config.Root.window.state  = WindowState.ToString();

			Config.Root.window.pos_x  = (int) Left  ;
			Config.Root.window.pos_y  = (int) Top   ;
			Config.Root.window.size_x = (int) Width ;
			Config.Root.window.size_y = (int) Height;

			base.OnClosing(e);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			//TODO: Let project view handle the rest
			if (CurrentProject != null)
				PrjView.DoKeyDown(e);
		}

		protected override void OnKeyUp(KeyEventArgs e)
		{
			// Lazy old-style keyboard shortcuts 
			// ... but way shorter than this Key-/CommandBinding shananigans -.-
			base.OnKeyUp(e);
			if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
			{
				switch (e.Key)
				{
					case Key.N: File_New_Click(e.OriginalSource, null); break;

#if DEBUG
					case Key.O: _LoadProject(Settings.DEFAULT_FILE); break;
#else
					case Key.O: File_Open_Click(e.OriginalSource, null); break;
#endif

					case Key.S: File_Save_Click(e.OriginalSource, null); break;
					case Key.W: File_Close_Click(e.OriginalSource, null); break;
				}
			}
			else
			{
				//TODO: Let project view handle the rest
				if (CurrentProject != null)
					PrjView.DoKeyUp(e);
			}
		}


		private void File_New_Click(object sender, RoutedEventArgs e)
		{
			var prj = Dialogs.CreateProjectDialog.CreateNewProject();
			if (prj != null)
			{
				_CloseProject();

				CurrentProject = prj;
				//TODO: Monitor IsModified

				PrjView.Project = CurrentProject;

				if (_ValuePreview != null)
					_ValuePreview.Project = CurrentProject;
				if (_DataBufferInfo != null)
					_DataBufferInfo.Project = CurrentProject;

				_AddToMRU(CurrentProject.Filename);

				UpdateUI();
			}
		}

		private void File_Open_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.Title = "Select project to load";//Translate._("MainWindow.LoadGamefile.Title");
			dlg.InitialDirectory = Config.Root.core.defaultpath;
			dlg.Filter = "Hexalyzer projects (*.hexaproj)|*.hexaproj|All files (*.*)|*.*";//Translate._("MainWindow.LoadGamefile.Filter");
			if (dlg.ShowDialog().GetValueOrDefault(false) == true && File.Exists(dlg.FileName))
			{
				string filename = dlg.FileName;
				_AddToMRU(filename);
				_LoadProject(filename);
			}
		}

		private void File_Save_Click(object sender, RoutedEventArgs e)
		{
			_SaveProject(null);
		}

		private void File_SaveAs_Click(object sender, RoutedEventArgs e)
		{
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Title = "Select new filename";
			dlg.InitialDirectory = Path.GetDirectoryName(CurrentProject.Filename);
			dlg.Filter = "Hexalyzer projects (*.hexaproj)|*.hexaproj|All files (*.*)|*.*";//Translate._("MainWindow.LoadGamefile.Filter");
			if (dlg.ShowDialog().GetValueOrDefault(false) == true)
			{
				string filename = dlg.FileName;
				_SaveProject(filename);
			}
		}

		private void File_Close_Click(object sender, RoutedEventArgs e)
		{
			_CloseProject();
		}

		private void File_MRU_Click(object sender, RoutedEventArgs e)
		{
			_HandleMRU(sender, e);
		}

		private void File_Exit_Click(object sender, RoutedEventArgs e)
		{
			_CloseProject();

			Close();
		}


		private void Resource_Add(object sender, RoutedEventArgs e)
		{
			string type_s = null;
			if (sender is Button)
				type_s = (sender as Button).Tag as string;
			else if (sender is MenuItem)
				type_s = (sender as MenuItem).Tag as string;
			if (type_s == null)
				return;

			Type type = Datatypes.Registry.Get(type_s);

			PrjView.AddResource(type);
		}

		private void Resource_Remove(object sender, RoutedEventArgs e)
		{
			PrjView.RemoveResource();
		}


		private void View_ValuePreview(object sender, RoutedEventArgs e)
		{
			if (_IsCheckedToggling)
				return;

			bool active;
			if (sender is MenuItem)
			{
				_IsCheckedToggling = true;
				View_ValuePreview_TB.IsChecked = active = (sender as MenuItem).IsChecked;
				_IsCheckedToggling = false;
			}
			else if (sender is ToggleButton)
			{
				_IsCheckedToggling = true;
				View_ValuePreview_Menu.IsChecked = active = (sender as ToggleButton).IsChecked.GetValueOrDefault(false);
				_IsCheckedToggling = false;
			}
			else
				return;

			if (!active) // <- Logic flipped as we're triggered after check was updated!
			{
				// Remove value preview panel
				if (_ValuePreview == null)
					throw new Exception("Invalid internal state! (value preview == null)");
				Tools.Remove(_ValuePreview);
				_ValuePreview = null;
			}
			else
			{
				// Add value preview panel
				if (_ValuePreview != null)
					throw new Exception("Invalid internal state! (value preview != null)");
				_ValuePreview = new Tools.ValuePreview(PrjView);
				_ValuePreview.Project = CurrentProject;
				Tools.Add(_ValuePreview);
			}
		}

#if DEVENV
		private void View_DataBufferInfo(object sender, RoutedEventArgs e)
		{
			if (_IsCheckedToggling)
				return;

			bool active;
			if (sender is MenuItem)
			{
				active = (sender as MenuItem).IsChecked;
				ToggleButton btn = View_ToolBar.Items.Named("View_DataBufferInfo_TB") as ToggleButton;
				if (btn != null)
				{
					_IsCheckedToggling = true;
					btn.IsChecked = active;
					_IsCheckedToggling = false;
				}
			}
			else if (sender is ToggleButton)
			{
				active = (sender as ToggleButton).IsChecked.GetValueOrDefault(false);
				MenuItem menu = View_Menu.Items.Named("View_DataBufferInfo_Menu") as MenuItem;
				if (menu != null)
				{
					_IsCheckedToggling = true;
					menu.IsChecked = active;
					_IsCheckedToggling = false;
				}
			}
			else
				return;

			if (!active) // <- Logic flipped as we're triggered after check was updated!
			{
				// Remove data buffer info panel
				if (_DataBufferInfo == null)
					throw new Exception("Invalid internal state! (data buffer view == null)");
				Tools.Remove(_DataBufferInfo);
				_DataBufferInfo = null;
			}
			else
			{
				// Add data buffer info panel
				if (_DataBufferInfo != null)
					throw new Exception("Invalid internal state! (data buffer view != null)");
				_DataBufferInfo = new Tools.DataBufferInfo();
				_DataBufferInfo.Project = CurrentProject;
				Tools.Add(_DataBufferInfo);
			}
		}

		private void View_ProjectViewInfo(object sender, RoutedEventArgs e)
		{
			if (_IsCheckedToggling)
				return;

			bool active;
			if (sender is MenuItem)
			{
				active = (sender as MenuItem).IsChecked;
				ToggleButton btn = View_ToolBar.Items.Named("View_ProjectViewInfo_TB") as ToggleButton;
				if (btn != null)
				{
					_IsCheckedToggling = true;
					btn.IsChecked = active;
					_IsCheckedToggling = false;
				}
			}
			else if (sender is ToggleButton)
			{
				active = (sender as ToggleButton).IsChecked.GetValueOrDefault(false);
				MenuItem menu = View_Menu.Items.Named("View_ProjectViewInfo_Menu") as MenuItem;
				if (menu != null)
				{
					_IsCheckedToggling = true;
					menu.IsChecked = active;
					_IsCheckedToggling = false;
				}
			}
			else
				return;

			if (!active) // <- Logic flipped as we're triggered after check was updated!
			{
				// Remove project view info panel
				if (_ProjectViewInfo == null)
					throw new Exception("Invalid internal state! (project view info == null)");
				Tools.Remove(_ProjectViewInfo);
				_ProjectViewInfo = null;
			}
			else
			{
				// Add project view info panel
				if (_ProjectViewInfo != null)
					throw new Exception("Invalid internal state! (project view info != null)");
				_ProjectViewInfo = new Tools.ProjectViewInfo();
				_ProjectViewInfo.View = PrjView;
				Tools.Add(_ProjectViewInfo);
			}
		}
#endif


		private void Tools_Analyze(object sender, RoutedEventArgs e)
		{
			if (_IsCheckedToggling)
				return;

			bool active;
			if (sender is MenuItem)
			{
				_IsCheckedToggling = true;
				Tools_Analyze_TB.IsChecked = active = (sender as MenuItem).IsChecked;
				_IsCheckedToggling = false;
			}
			else if (sender is ToggleButton)
			{
				_IsCheckedToggling = true;
				Tools_Analyze_Menu.IsChecked = active = (sender as ToggleButton).IsChecked.GetValueOrDefault(false);
				_IsCheckedToggling = false;
			}
			else
				return;

			if (!active) // <- Logic flipped as we're triggered after check was updated!
			{
				// Stop background analyzer
				PrjView.IsAnalyzerActive = false;
			}
			else
			{
				// Start background analyzer
				PrjView.IsAnalyzerActive = true;
			}
		}


		private void Help_Changelog_Click(object sender, RoutedEventArgs e)
		{
			string filename = Path.Combine(Settings.RESOURCE_PATH, "Changelog.res");
			string content = File.ReadAllText(filename);
			Dialogs.ShowHtmlResDialog.Show("Changelog ...", content);
		}

		private void Help_About_Click(object sender, RoutedEventArgs e)
		{
			string filename = Path.Combine(Settings.RESOURCE_PATH, "About.res");
			string content = File.ReadAllText(filename);
			Dialogs.ShowHtmlResDialog.Show("About ...", content);
		}


		// Events from project file
		private void CurrentProject_IsModifiedChanged(object sender, bool state)
		{
			UpdateUI();
		}

		// Events from project view
		private void PrjView_OffsetChanged(object sender, long from, long to)
		{
			UpdateUI();

			string str = "Offset: ";
			if (from >= 0)
				str += from.ToOffsetString() + " : " + to.ToOffsetString();
			else
				str += "-";
			sb_Offset.Text = str;
		}

		private void PrjView_SelectionChanged(object sender, long from, long to)
		{
			UpdateUI();

			string str = "Selected: ";
			if (from >= 0)
			{
				str += from.ToOffsetString();
				if (to >= from)
				{
					str += string.Format(" : {0} ({1})", to.ToOffsetString(), (to - from + 1));
				}
			}
			else
				str += "-";
			sb_Selection.Text = str;
		}


		private ProjectFile CurrentProject;

		// Tools shown in tools panel
		private Tools.ValuePreview _ValuePreview;
		private Tools.DataBufferInfo _DataBufferInfo;
		private Tools.ProjectViewInfo _ProjectViewInfo;

		private bool _IsCheckedToggling = false;
	

		#region Project file handling
		private void _LoadProject(string filename)
		{
			_CloseProject();

			if (!File.Exists(filename))
			{
				Debug.Fail("Tried to load a file which didn't exist anymore!");
				return;
			}

			CurrentProject = ProjectFile.Load(filename);
			CurrentProject.IsModifiedChanged += CurrentProject_IsModifiedChanged;

			PrjView.Project = CurrentProject;

			if (_ValuePreview != null)
				_ValuePreview.Project = CurrentProject;
			if (_DataBufferInfo != null)
				_DataBufferInfo.Project = CurrentProject;

			UpdateUI();

			sb_Filesize.Text = string.Format("Filesize: {0:#,#0} bytes ({1})", 
				CurrentProject.Filesize, CurrentProject.Filesize.ToOffsetString());
		}

		private void _SaveProject(string filename)
		{
			if (CurrentProject == null)
				return;

			if (filename == null)
				CurrentProject.Save();
			else
				CurrentProject.SaveAs(filename);

			UpdateUI();
		}

		private void _CloseProject()
		{
			if (CurrentProject != null)
			{
				CurrentProject.IsModifiedChanged -= CurrentProject_IsModifiedChanged;

				if (CurrentProject.IsModified)
				{
					var res = MessageBox.Show("Your project contains unsaved changes!\n\nDo you want to save the file before closing?", 
						"Hexalyzer", MessageBoxButton.YesNo, MessageBoxImage.Warning);
					if (res == MessageBoxResult.Yes)
						_SaveProject(CurrentProject.Filename);
				}

				PrjView.Project = null;

				if (_ValuePreview != null)
					_ValuePreview.Project = null;
				if (_DataBufferInfo != null)
					_DataBufferInfo.Project = null;

				CurrentProject.Close();
				CurrentProject = null;

				sb_Offset.Text = sb_Selection.Text = sb_Filesize.Text = "";
			}

			UpdateUI();
		}
		#endregion

		#region MRU handling
		protected void _SetupMRU()
		{
			if (!Config.HasSection("mru"))
			{
				File_MRU.IsEnabled = false;
				return;
			}

			_UpdateMRU();
			File_MRU.IsEnabled = true;
		}

		protected void _UpdateMRU()
		{
			MenuItem item;

			// Remove any old item beforehand
			while (File_MRU.Items.Count > 2)
			{
				item = File_MRU.Items[File_MRU.Items.Count - 1] as MenuItem;
				item.Click -= File_MRU_Click;
				File_MRU.Items.Remove(item);
			}

			// Add elements available
			foreach(string file in Config.Root.mru.files)
			{
				int index = File_MRU.Items.Count - 2;

				item = new MenuItem();
				item.Header = string.Format("_{0}. {1}", index, file);
				item.Tag = index;
				item.Click += File_MRU_Click;

				File_MRU.Items.Add(item);
			}

			File_MRU.UpdateLayout();
		}

		protected void _HandleMRU(object sender, RoutedEventArgs e)
		{
			MenuItem item = sender as MenuItem;
			int index = (int) item.Tag;

			if (index == -1)
			{
				// Clear all
				Config.Root.mru.files.Clear();
				_UpdateMRU();
			}
			else
			{
				string filename = Config.Root.mru.files[index];
				if (!File.Exists(filename))
				{
					string msg = "The specified file\n\n" + filename + "\n\ndoes not exist anymore!\n\nShould this file be removed from the list?";
					MessageBoxResult res = MessageBox.Show(msg, "File not found", MessageBoxButton.YesNo, MessageBoxImage.Question);
					if (res == MessageBoxResult.Yes)
					{
						Config.Root.mru.files.RemoveAt(index);
						_UpdateMRU();
					}
					return;
				}

				_LoadProject(filename);
			}
		}

		protected void _AddToMRU(string filename)
		{
			foreach(string file in Config.Root.mru.files)
			{
				if (file == filename)
					return;
			}
			Config.Root.mru.files.Add(filename);
			_UpdateMRU();
		}
		#endregion

	}
}
