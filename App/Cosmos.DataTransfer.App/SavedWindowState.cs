
namespace Cosmos.DataTransfer.App;

public class SavedWindowState
{
    public string WindowType { get; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    public SavedWindowState(string windowType, Window window)
    {
        WindowType = windowType;

        X = window.X;
        Y = window.Y;
        Width = window.Width;
        Height = window.Height;
    }

    public SavedWindowState(string windowType)
    {
        WindowType = windowType;
        X = GetSavedValue("WindowLocationX");
        Y = GetSavedValue("WindowLocationY");
        Width = GetSavedValue("WindowSizeWidth");
        Height = GetSavedValue("WindowSizeHeight");
    }

    private double? GetSavedValue(string valueKey)
    {
        var xValue = Preferences.Get($"{WindowType}{valueKey}", double.NaN);
        if (!double.IsNaN(xValue))
        {
            return xValue;
        }

        return null;
    }

    public void Save()
    {
        SaveValue("WindowLocationX", X);
        SaveValue("WindowLocationY", Y);
        SaveValue("WindowSizeWidth", Width);
        SaveValue("WindowSizeHeight", Height);
    }

    private void SaveValue(string valueKey, double? value)
    {
        if (value != null)
            Preferences.Set($"{WindowType}{valueKey}", value.Value);
        else
            Preferences.Remove($"{WindowType}{valueKey}");
    }
}
