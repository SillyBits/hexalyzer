using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Hexalyzer.Tools
{

	public class DataBufferInfo : ToolsWindow
	{

		public ProjectFile Project
		{
			get { return _Project; }
			set { _Project = value; _Update(); }
		}


		public DataBufferInfo()
		{
			InitializeComponent();

			Icon = new BitmapImage(new Uri("pack://application:,,,/Resources/Toolbar.View.DataBufferInfo.png"));
			Title = "Data buffer info";


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

		~DataBufferInfo()
		{
			_Stop();
		}


		private void _Update()
		{
			_Stop();

			if (_Project == null)
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
				if (_Project == null || _Project.BufferInfo == null)
					_Info.Text = "n/a";
				else
					_Info.Text = /*DateTime.Now.ToString() + "\n" +*/
						string.Join("\n", _Project.BufferInfo);
			}
		}


		private ProjectFile _Project;
		private TextBlock _Info;
		private Timer _Timer;
	}

}
