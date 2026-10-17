using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Windows.Devices.Geolocation;

namespace Jarvis.Desktop;

internal sealed record DeviceLocation(double Latitude, double Longitude, string PlaceName);

internal sealed record WeatherSnapshot(
    double TemperatureCelsius,
    int WeatherCode,
    double HighCelsius,
    double LowCelsius,
    int? PrecipitationChance,
    DateTime RetrievedAt);

internal sealed class LocationAccessException : Exception
{
    public LocationAccessException(string status, bool permissionDenied, Exception? innerException = null)
        : base(status, innerException)
    {
        Status = status;
        PermissionDenied = permissionDenied;
    }

    public string Status { get; }
    public bool PermissionDenied { get; }
}

internal static class EnvironmentData
{
    private static readonly HttpClient WeatherClient = new() { Timeout = TimeSpan.FromSeconds(12) };

    public static async Task<DeviceLocation> RequestDeviceLocationAsync()
    {
        GeolocationAccessStatus access = await Geolocator.RequestAccessAsync();
        if (access != GeolocationAccessStatus.Allowed)
            throw new LocationAccessException($"RequestAccessAsync: {access}", access == GeolocationAccessStatus.Denied);

        var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
        Geoposition position;
        try
        {
            position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(20));
        }
        catch (Exception ex)
        {
            throw new LocationAccessException(
                $"PositionStatus: {locator.LocationStatus}; {ex.GetType().Name}; HRESULT 0x{ex.HResult:X8}",
                ex is UnauthorizedAccessException || ex.HResult == unchecked((int)0x80070005),
                ex);
        }
        BasicGeoposition coordinates = position.Coordinate.Point.Position;
        CivicAddress? address = position.CivicAddress;
        string placeName = address?.City?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(placeName))
            placeName = $"{coordinates.Latitude.ToString("F2", CultureInfo.InvariantCulture)}°, {coordinates.Longitude.ToString("F2", CultureInfo.InvariantCulture)}°";

        return new DeviceLocation(coordinates.Latitude, coordinates.Longitude, placeName);
    }

    public static async Task<WeatherSnapshot> GetWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        string lat = latitude.ToString("F5", CultureInfo.InvariantCulture);
        string lon = longitude.ToString("F5", CultureInfo.InvariantCulture);
        string url = "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code" +
            "&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
            "&forecast_days=1&timezone=auto&temperature_unit=celsius";

        using HttpResponseMessage response = await WeatherClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        JsonElement root = document.RootElement;
        JsonElement current = root.GetProperty("current");
        JsonElement daily = root.GetProperty("daily");

        return new WeatherSnapshot(
            current.GetProperty("temperature_2m").GetDouble(),
            current.GetProperty("weather_code").GetInt32(),
            daily.GetProperty("temperature_2m_max")[0].GetDouble(),
            daily.GetProperty("temperature_2m_min")[0].GetDouble(),
            daily.GetProperty("precipitation_probability_max")[0].ValueKind == JsonValueKind.Number
                ? daily.GetProperty("precipitation_probability_max")[0].GetInt32()
                : null,
            DateTime.Now);
    }

    public static (string Key, string Glyph, string Color) DescribeWeather(int code) => code switch
    {
        0 => ("clear", "☀", "#E0C77C"),
        1 => ("mostlyClear", "◐", "#E0C77C"),
        2 => ("partlyCloudy", "◒", "#80B9FF"),
        3 => ("overcast", "☁", "#AEB8D0"),
        45 or 48 => ("fog", "≋", "#AEB8D0"),
        51 or 53 or 55 or 56 or 57 => ("drizzle", "☂", "#78AEFF"),
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => ("rain", "☂", "#78AEFF"),
        71 or 73 or 75 or 77 or 85 or 86 => ("snow", "❄", "#D7E6FF"),
        95 or 96 or 99 => ("thunderstorm", "ϟ", "#B79EFF"),
        _ => ("variable", "◌", "#AEB8D0")
    };
}
