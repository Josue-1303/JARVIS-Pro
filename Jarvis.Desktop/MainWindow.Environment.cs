using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Jarvis.Desktop;

public partial class MainWindow
{
    private readonly DispatcherTimer localClockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer weatherRefreshTimer = new() { Interval = TimeSpan.FromMinutes(30) };
    private readonly DispatcherTimer newsRefreshTimer = new() { Interval = TimeSpan.FromMinutes(30) };
    private CancellationTokenSource internetRequests = new();
    private DeviceLocation? currentDeviceLocation;
    private WeatherSnapshot? currentWeather;
    private string weatherStatusKey = "locationOff";
    private bool locationPermissionDenied;
    private string newsStatusKey = "newsLoading";
    private DateTime? newsRetrievedAt;
    private IReadOnlyList<NewsHeadline> latestNews = Array.Empty<NewsHeadline>();
    private bool internetAccessEnabled;
    private bool loadingInternetAccess;

    private void InitializeInternetAccess()
    {
        string path = Path.Combine(store.DataPath, "internet-access.txt");
        try
        {
            internetAccessEnabled = File.Exists(path) &&
                File.ReadAllText(path).Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            internetAccessEnabled = false;
        }

        loadingInternetAccess = true;
        InternetAccessToggle.IsChecked = internetAccessEnabled;
        loadingInternetAccess = false;
        ApplyInternetAccessState(refresh: false);
    }

    private void InternetAccessToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (loadingInternetAccess) return;
        bool enabled = InternetAccessToggle.IsChecked == true;
        if (enabled == internetAccessEnabled) return;

        string path = Path.Combine(store.DataPath, "internet-access.txt");
        try { File.WriteAllText(path, enabled ? "on" : "off"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            loadingInternetAccess = true;
            InternetAccessToggle.IsChecked = internetAccessEnabled;
            loadingInternetAccess = false;
            SetStatus("internetSettingsError");
            return;
        }

        internetAccessEnabled = enabled;
        ApplyInternetAccessState(refresh: true);
        SetStatus(enabled ? "internetEnabledStatus" : "internetDisabledStatus");
    }

    private void ConfigureWebSearch_Click(object sender, RoutedEventArgs e)
    {
        bool hasKey = WebSearchCredentialStore.HasKey(store.DataPath);
        var dialog = new WebSearchKeyDialog(
            T("webSearchDialogTitle"), T("webSearchDialogDescription"), T("webSearchKeyLabel"),
            T("webSearchSaveKey"), T("webSearchRemoveKey"), T("webSearchCancel"), T("webSearchOpenKeys"),
            T("webSearchKeyReady"), T("webSearchKeyMissing"), hasKey) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            if (dialog.RemoveRequested)
            {
                WebSearchCredentialStore.Remove(store.DataPath);
                SetStatus("webSearchKeyRemoved");
            }
            else
            {
                WebSearchCredentialStore.Save(store.DataPath, dialog.ApiKey);
                SetStatus("webSearchKeySaved");
            }
            WebSearchKeyStatusText.Text = T(WebSearchCredentialStore.HasKey(store.DataPath) ? "webSearchKeyReady" : "webSearchKeyMissing");
            WebSearchKeyStatusText.Foreground = WebSearchCredentialStore.HasKey(store.DataPath)
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(89, 214, 206))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(133, 143, 169));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception or System.Security.Cryptography.CryptographicException)
        {
            MessageBox.Show(this, ex.Message, T("webSearchDialogTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyInternetAccessState(bool refresh)
    {
        InternetAccessStateText.Text = T(internetAccessEnabled ? "internetOn" : "internetOff");
        LocationButton.ToolTip = !internetAccessEnabled
            ? T("internetOffLocationTip")
            : currentDeviceLocation is null ? T("locationTip") : T("locationPrivacyTip");
        if (internetAccessEnabled)
        {
            if (internetRequests.IsCancellationRequested)
            {
                internetRequests.Dispose();
                internetRequests = new CancellationTokenSource();
            }
            if (!refresh) return;
            weatherRefreshTimer.Start();
            newsRefreshTimer.Start();
            _ = RefreshNewsAsync();
            if (currentDeviceLocation is not null) _ = RefreshWeatherAsync();
            return;
        }

        internetRequests.Cancel();
        weatherRefreshTimer.Stop();
        newsRefreshTimer.Stop();
        currentWeather = null;
        weatherStatusKey = "internetOffHint";
        latestNews = Array.Empty<NewsHeadline>();
        newsRetrievedAt = null;
        newsStatusKey = "internetOffHint";
        NewsList.ItemsSource = latestNews;
        RenderNewsStatus();
        RenderWeatherHud();
    }

    private void StartEnvironmentHud()
    {
        UpdateLocalClock();
        localClockTimer.Tick += LocalClockTimer_Tick;
        localClockTimer.Start();
        weatherRefreshTimer.Tick += WeatherRefreshTimer_Tick;
        NewsList.ItemsSource = latestNews.Select(CreateNewsView).ToArray();
        newsRefreshTimer.Tick += NewsRefreshTimer_Tick;
        if (internetAccessEnabled)
        {
            weatherRefreshTimer.Start();
            newsRefreshTimer.Start();
            _ = RefreshNewsAsync();
            if (currentDeviceLocation is not null) _ = RefreshWeatherAsync();
        }
        else ApplyInternetAccessState(refresh: false);
    }

    private void StopEnvironmentHud()
    {
        localClockTimer.Stop();
        localClockTimer.Tick -= LocalClockTimer_Tick;
        weatherRefreshTimer.Stop();
        weatherRefreshTimer.Tick -= WeatherRefreshTimer_Tick;
        newsRefreshTimer.Stop();
        newsRefreshTimer.Tick -= NewsRefreshTimer_Tick;
        internetRequests.Cancel();
        internetRequests.Dispose();
    }

    private void LocalClockTimer_Tick(object? sender, EventArgs e) => UpdateLocalClock();

    private void UpdateLocalClock()
    {
        DateTime now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        DateText.Text = now.ToString("ddd, d MMM yyyy", CultureInfo.GetCultureInfo(interfaceLanguage == "en" ? "en-GB" : "es-ES"));
    }

    private void SetLocationStatus(string key)
    {
        locationStatusKey = key;
        LocationStatusText.Text = T(key);
    }

    private async void LocationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!internetAccessEnabled)
        {
            SetLocationStatus("internetOffHint");
            LocationButton.ToolTip = T("internetOffLocationTip");
            return;
        }

        if (locationPermissionDenied)
        {
            locationPermissionDenied = false;
            SetLocationStatus("locationPermission");
            LocationText.Text = T("enableLocation");
            LocationButton.ToolTip = T("locationTip");
            try { Process.Start(new ProcessStartInfo("ms-settings:privacy-location") { UseShellExecute = true }); }
            catch (Exception)
            {
                locationPermissionDenied = true;
                SetLocationStatus("locationDenied");
                LocationButton.ToolTip = T("locationDeniedTip");
            }
            return;
        }

        LocationButton.IsEnabled = false;
        SetLocationStatus("locationRequesting");
        try
        {
            currentDeviceLocation = await EnvironmentData.RequestDeviceLocationAsync();
            locationPermissionDenied = false;
            LocationText.Text = currentDeviceLocation.PlaceName;
            LocationButton.ToolTip = T("locationPrivacyTip");
            SetLocationStatus("locationActive");
            await RefreshWeatherAsync();
        }
        catch (Exception ex)
        {
            locationPermissionDenied = ex is LocationAccessException locationError
                ? locationError.PermissionDenied
                : ex is UnauthorizedAccessException || ex.HResult == unchecked((int)0x80070005);
            SetLocationStatus(locationPermissionDenied ? "locationDenied" : "locationUnavailable");
            string diagnostic = ex is LocationAccessException locationFailure
                ? locationFailure.Status
                : $"{ex.GetType().FullName}; HRESULT 0x{ex.HResult:X8}; {ex.Message}";
            if (ex.InnerException is Exception inner)
                diagnostic += $"\nInner: {inner.GetType().FullName}; HRESULT 0x{inner.HResult:X8}; {inner.Message}";
            LocationStatusText.Text = T("locationDiagnostic");
            LocationButton.ToolTip = $"{T(locationPermissionDenied ? "locationDeniedTip" : "locationUnavailableTip")}\n\n{diagnostic}";
            if (currentDeviceLocation is null) LocationText.Text = T("allowLocation");
        }
        finally
        {
            LocationButton.IsEnabled = true;
        }
    }

    private async void WeatherRefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (currentDeviceLocation is not null) await RefreshWeatherAsync();
    }

    private async void NewsRefreshTimer_Tick(object? sender, EventArgs e) => await RefreshNewsAsync();

    private async Task RefreshNewsAsync()
    {
        if (!internetAccessEnabled)
        {
            newsStatusKey = "internetOffHint";
            latestNews = Array.Empty<NewsHeadline>();
            NewsList.ItemsSource = latestNews;
            RenderNewsStatus();
            return;
        }
        newsStatusKey = "newsLoading";
        NewsStatusText.Text = T(newsStatusKey);
        CancellationToken requestToken = internetRequests.Token;
        try
        {
            latestNews = await NewsFeedData.GetHeadlinesAsync(requestToken);
            if (!internetAccessEnabled || requestToken.IsCancellationRequested) return;
            newsStatusKey = latestNews.Count == 0 ? "newsOffline" : "newsUpdated";
            newsRetrievedAt = DateTime.Now;
            NewsList.ItemsSource = latestNews.Select(CreateNewsView).ToArray();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (!internetAccessEnabled) return;
            newsStatusKey = "newsOffline";
        }
        RenderNewsStatus();
    }

    private void RenderNewsStatus() => NewsStatusText.Text = newsStatusKey == "newsUpdated" && newsRetrievedAt is DateTime retrieved
        ? string.Format(T(newsStatusKey), retrieved.ToString("HH:mm", CultureInfo.InvariantCulture))
        : T(newsStatusKey);

    private NewsHeadlineView CreateNewsView(NewsHeadline headline)
    {
        string published = headline.PublishedAt == DateTimeOffset.MinValue
            ? T("newsUnknownTime")
            : headline.PublishedAt.ToLocalTime().ToString("d MMM · HH:mm", CultureInfo.GetCultureInfo(interfaceLanguage == "en" ? "en-GB" : "es-ES"));
        return new NewsHeadlineView(headline.Title, headline.Url, published);
    }

    private sealed record NewsHeadlineView(string Title, string Url, string PublishedLabel);

    private async Task RefreshWeatherAsync()
    {
        if (!internetAccessEnabled || currentDeviceLocation is null) return;
        weatherStatusKey = "weatherUpdating";
        RenderWeatherHud();
        CancellationToken requestToken = internetRequests.Token;
        try
        {
            currentWeather = await EnvironmentData.GetWeatherAsync(
                currentDeviceLocation.Latitude, currentDeviceLocation.Longitude, requestToken);
            if (!internetAccessEnabled || requestToken.IsCancellationRequested) return;
            weatherStatusKey = "weatherUpdated";
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (!internetAccessEnabled) return;
            weatherStatusKey = "weatherOffline";
        }
        RenderWeatherHud();
    }

    private void RenderWeatherHud()
    {
        if (currentWeather is WeatherSnapshot weather)
        {
            var condition = EnvironmentData.DescribeWeather(weather.WeatherCode);
            WeatherGlyph.Text = condition.Glyph;
            WeatherGlyph.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(condition.Color)!;
            WeatherTemperatureText.Text = $"{Math.Round(weather.TemperatureCelsius):0}°";
            WeatherConditionText.Text = T(condition.Key);
            WeatherRangeText.Text = string.Format(T("temperatureRange"), Math.Round(weather.HighCelsius), Math.Round(weather.LowCelsius));
            WeatherPrecipitationText.Text = weather.PrecipitationChance is int chance
                ? string.Format(T("precipitationChance"), chance)
                : T("todayForecast");
            WeatherSourceText.Text = weatherStatusKey switch
            {
                "weatherUpdated" => string.Format(T("weatherUpdated"), weather.RetrievedAt.ToString("HH:mm", CultureInfo.InvariantCulture)),
                _ => T(weatherStatusKey)
            };
        }
        else
        {
            WeatherConditionText.Text = T(!internetAccessEnabled ? "internetOffHint" : currentDeviceLocation is null ? "locationOff" : "weatherNoForecast");
            WeatherTemperatureText.Text = "--°";
            WeatherRangeText.Text = string.Format(T("temperatureRange"), "--", "--");
            WeatherPrecipitationText.Text = string.Format(T("precipitationChance"), "--");
            WeatherSourceText.Text = !internetAccessEnabled ? T("internetOffHint") : currentDeviceLocation is null ? "OPEN-METEO" : T(weatherStatusKey);
        }
    }
}
