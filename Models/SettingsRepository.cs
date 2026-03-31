using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Settings;
using PerformanceTrayMonitor.Common;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace PerformanceTrayMonitor.Models
{
	public sealed class SettingsRepository
	{
		private readonly string _path;

		public SettingsRepository(string path)
		{
			_path = path;
		}

		public async Task<SettingsDto?> LoadAsync()
		{
			if (!File.Exists(_path))
				return null;

			var json = await File.ReadAllTextAsync(_path);
			return JsonSerializer.Deserialize<SettingsDto>(json);
		}

		public async Task SaveAsync(SettingsDto dto)
		{
			var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
			{
				WriteIndented = true
			});

			var temp = _path + ".tmp";
			await File.WriteAllTextAsync(temp, json);

			File.Copy(temp, _path, overwrite: true);
			File.Delete(temp);
		}

		private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
		{
			WriteIndented = true
		};

		public static void SaveAtomic(SettingsDto dto)
		{
			try
			{
				var json = JsonSerializer.Serialize(dto, JsonOptions);

				var tempFile = Paths.SettingsFile + ".tmp";
				File.WriteAllText(tempFile, json);

				File.Copy(tempFile, Paths.SettingsFile, overwrite: true);
				File.Delete(tempFile);
			}
			catch (Exception ex)
			{
				Log.Error($"{ex} Failed to save settings file atomically.");
			}
		}

		public static SettingsDto Load()
		{
			try
			{
				if (!File.Exists(Paths.SettingsFile))
					return null;

				var json = File.ReadAllText(Paths.SettingsFile);
				return JsonSerializer.Deserialize<SettingsDto>(json);
			}
			catch (Exception ex)
			{
				Log.Error($"{ex} Failed to load settings file.");
				return null;
			}
		}
	}
}
