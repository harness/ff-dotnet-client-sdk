using System.Collections.ObjectModel;
using System.Text;
using Microsoft.Extensions.Primitives;
using Exception = System.Exception;
using String = System.String;

namespace MauiApp_basic;

public partial class MainPage : ContentPage
{
	private readonly ObservableCollection<String> _lines = ["Output"];
	private readonly IFeatureFlagsContext _ffService;
	private readonly IDispatcherTimer _timer;
	private int _counter = 0;

	public MainPage()
	{
		InitializeComponent();
		_ffService = ServiceHelper.Services.GetRequiredService<IFeatureFlagsContext>();

		_timer = Application.Current.Dispatcher.CreateTimer();
		_timer.Interval = TimeSpan.FromSeconds(1);
		_timer.Tick += (s, e) => OnTick2();
		_timer.Start();
	}

	private void OnTick2()
	{
		if (_ffService.InitFailed(out var exception))
		{
			PrintLine(GetInnerExceptionNames(exception));
			_timer.Stop();
			return;
		}

		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				_counter++;
				var flagResult = _ffService.IsFlagEnabled();
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

	private static string GetInnerExceptionNames(Exception exception)
	{
		Exception? ex = exception;
		var builder = new StringBuilder();
		while (ex != null)
		{
			var builder2 = new StringBuilder();
			builder2.Append(ex.GetType().Name);
			builder2.Append('(');
			builder2.Append(ex.Message);
			builder2.Append(')');

			builder.Insert(0, " > ").Insert(0, builder2.ToString());
			ex = ex.InnerException;
		}

		builder.Length -= 3;
		return builder.ToString();
	}

}

