using System.Collections.ObjectModel;

namespace MauiApp_basic;

public partial class MainPage : ContentPage
{
	int _counter = 0;
	ObservableCollection<String> _lines = ["", ""];
	private IFeatureFlagsContext? _ffService;
	private readonly IDispatcherTimer _timer;

	public MainPage()
	{
		InitializeComponent();
		_ffService = ServiceHelper.Services.GetService<IFeatureFlagsContext>();

		_timer = Application.Current.Dispatcher.CreateTimer();
		_timer.Interval = TimeSpan.FromSeconds(1);
		_timer.Tick += (s, e) => OnTick2();
		_timer.Start();

	}

	private void OnTick2()
	{
		if (_ffService == null)
		{
			PrintLine("IFeatureFlagsContext is null!");
			return;
		}
		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				_counter++;
				var flagResult = _ffService.IsTestFlagEnabled();
				PrintLine(_counter + " - "+ FeatureFlagsContext.TestFlagIdentifier + " is " + flagResult);
			}
			catch (Exception ex)
			{
				PrintLine("EXCEPTION CAUGHT:\n\n" + ex.ToString());
			}
		});
	}

	private void OnResetClicked(object sender, EventArgs e)
	{
		_lines.Clear();
		_counter = 0;
		Lines.ItemsSource = _lines;
	}

	private void PrintLine(string line)
	{
		_lines.Add(line);
		Lines.ItemsSource = _lines;
	}

}

