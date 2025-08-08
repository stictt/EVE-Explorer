using System;
using System.IO;
using System.Text.Json;

public static class SettingsService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "EVEExplorer");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
            if (!File.Exists(FilePath)) return new AppSettings();

            var json = File.ReadAllText(FilePath);
            var s = JsonSerializer.Deserialize<AppSettings>(json);
            return s ?? new AppSettings();
        }
        catch
        {
            // если файл битый — стартуем с дефолтов
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

        var opts = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(settings, opts);

        // safe-write: во временный файл, затем Replace
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(FilePath))
            File.Replace(tmp, FilePath, FilePath + ".bak");
        else
            File.Move(tmp, FilePath);
    }
}
