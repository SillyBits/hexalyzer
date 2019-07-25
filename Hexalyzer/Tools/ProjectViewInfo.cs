using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Hexalyzer.Tools
{

	public class ProjectViewInfo : ToolsWindow
	{

		public ProjectView View
		{
			get { return _View; }
			set { _View = value; _Update(); }
		}


		public ProjectViewInfo()
		{
			InitializeComponent();

			Icon = new BitmapImage(new Uri("pack://application:,,,/Resources/Toolbar.View.ProjectViewInfo.png"));
			Title = "Project view info";


			_Info = new TextBlock() {
				Width = double.NaN,
				MinWidth = 100,
				Height = double.NaN,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				Margin = new Thickness(4, 0, 4, 0),
				Text = "n/a",
			};

			Child = _Info;
		}


		// Non-public implementation following
		//

		~ProjectViewInfo()
		{
			_Stop();
		}


		private void _Update()
		{
			_Stop();

			if (_View == null)
			{
				_Info.Text = "n/a";
			}
			else
			{
				_Timer = new Timer(_UpdateInfo, null, 0, 1000);
			}
		}

		private void _Stop()
		{
			if (_Timer != null)
				_Timer.Dispose();
			_Timer = null;
		}

		private void _UpdateInfo(object obj = null)
		{
			if (Dispatcher.IsInvokeRequired())
			{
				if (_Timer == null)
					return;
				Dispatcher.Invoke(() => _UpdateInfo(obj));
			}
			else
			{
				if (_View == null || _View.Info == null)
					_Info.Text = "n/a";
				else
					_Info.Text = /*DateTime.Now.ToString() + "\n" +*/
						string.Join("\n", _View.Info);
			}
		}


		private ProjectView _View;
		private TextBlock _Info;
		private Timer _Timer;
	}

}
