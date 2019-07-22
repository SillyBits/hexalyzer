using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;


namespace Hexalyzer.Dialogs
{
	/// <summary>
	/// Interaction logic for CreateProjectDialog.xaml
	/// </summary>
	public partial class CreateProjectDialog : Window
	{
		public CreateProjectDialog()
		{
			Loaded += _Loaded;

			InitializeComponent();
		}


		public static ProjectFile CreateNewProject(Window owner = null)
		{
			if (owner == null)
				owner = Application.Current.MainWindow;

			var dlg = new CreateProjectDialog();
			bool res = dlg.ShowDialog().GetValueOrDefault(false);
			if (res)
				return ProjectFile.Create(dlg.Filename.Text, dlg.Project.Text, dlg.Source.Text);

			return null;
		}


		private void _Loaded(object sender, RoutedEventArgs e)
		{
			Loaded -= _Loaded;

			Filename.TextChanged += Filename_TextChanged;
			Source.TextChanged += Source_TextChanged;
		}

		private void _UpdateButton()
		{
			create.IsEnabled = FilenameValid
				&& (Project.Text.Length > 0)
				&& SourceValid;
		}

		private void Filename_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
		{
			Uri uri;
			if (Uri.TryCreate(Filename.Text, UriKind.RelativeOrAbsolute, out uri))
				FilenameValid = (uri != null);
			else
				FilenameValid = false;
			_UpdateButton();
		}

		private void Source_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
		{
			SourceValid = File.Exists(Source.Text);
			_UpdateButton();
		}

		private void filenameBrowse_Click(object sender, RoutedEventArgs e)
		{
			//Assembly ass = Assembly.GetExecutingAssembly();
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Title = "Select new filename";
			dlg.InitialDirectory = Config.Root.core.defaultpath;
			dlg.Filter = "Hexalyzer projects (*.hexaproj)|*.hexaproj|All files (*.*)|*.*";//Translate._("MainWindow.LoadGamefile.Filter");
			if (dlg.ShowDialog().GetValueOrDefault(false) == true)
			{
				Filename.Text = dlg.FileName;
			}
		}

		private void sourceFileBrowse_Click(object sender, RoutedEventArgs e)
		{
			//Assembly ass = Assembly.GetExecutingAssembly();
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.Title = "Select source file";
			dlg.InitialDirectory = Config.Root.core.defaultpath;
			dlg.Filter = "All files (*.*)|*.*";//Translate._("MainWindow.LoadGamefile.Filter");
			if (dlg.ShowDialog().GetValueOrDefault(false) == true)
			{
				Source.Text = dlg.FileName;
			}
		}

		private void create_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		private void abort_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		internal bool FilenameValid = false;
		internal bool SourceValid = false;

	}
}
