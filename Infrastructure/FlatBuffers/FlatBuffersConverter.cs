using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.FlatBuffers
{
    public class FlatBuffersConverter
    {
        private readonly ILogger _logger;

        public FlatBuffersConverter(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Finds the full path to the flatc executable by checking PATH environment variable,
        /// application base directory, and common installation paths.
        /// </summary>
        public string FindFlatcExecutablePath()
        {
            // 1. Check in application base directory
            string localAppDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flatc.exe");
            if (File.Exists(localAppDir))
            {
                _logger.LogInformation("[flatc] Found flatc.exe in App Base Directory: {Path}", localAppDir);
                return localAppDir;
            }

            // 2. Check PATH environment variables (Process, User, Machine)
            var targets = new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine };
            foreach (var target in targets)
            {
                string? pathEnv = Environment.GetEnvironmentVariable("PATH", target);
                if (string.IsNullOrEmpty(pathEnv)) continue;

                var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in paths)
                {
                    try
                    {
                        string candidate = Path.Combine(p.Trim('"', ' '), "flatc.exe");
                        if (File.Exists(candidate))
                        {
                            _logger.LogInformation("[flatc] Found flatc.exe in PATH ({Target}): {Path}", target, candidate);
                            return candidate;
                        }
                    }
                    catch { }
                }
            }

            // 3. Check common installation directories
            string[] commonPaths = new[]
            {
                @"D:\flatbuffers\flatc.exe",
                @"C:\flatbuffers\flatc.exe",
                @"C:\Program Files\flatbuffers\flatc.exe",
                @"C:\Tools\flatc.exe"
            };

            foreach (var cp in commonPaths)
            {
                if (File.Exists(cp))
                {
                    _logger.LogInformation("[flatc] Found flatc.exe in common path: {Path}", cp);
                    return cp;
                }
            }

            _logger.LogWarning("[flatc] Could not resolve full path for flatc.exe. Defaulting to 'flatc'.");
            return "flatc";
        }

        /// <summary>
        /// Ensures data has 128-byte RSA header removed if present.
        /// FlatBuffers binary files from Arknights EN raw repository have a 128-byte RSA signature header.
        /// </summary>
        public byte[] EnsureHeaderStripped(byte[] data)
        {
            if (data == null || data.Length <= 128)
                return data ?? Array.Empty<byte>();

            // Check if buffer starts with a valid FlatBuffers root table offset
            // Offset 0 in FlatBuffers binary is uint (4 bytes) pointing to root table.
            // If offset 0 value is unreasonably large (> file size), it indicates a 128-byte header is present.
            int firstInt = BitConverter.ToInt32(data, 0);
            if (firstInt < 0 || firstInt >= data.Length)
            {
                _logger.LogInformation("Detected 128-byte header in binary data. Stripping header (Original size: {OrigSize} bytes).", data.Length);
                byte[] stripped = new byte[data.Length - 128];
                Buffer.BlockCopy(data, 128, stripped, 0, stripped.Length);
                return stripped;
            }

            return data;
        }

        /// <summary>
        /// Converts binary FlatBuffers data to JSON using flatc process.
        /// </summary>
        public async Task<string?> ConvertToJsonAsync(byte[] binaryData, string schemaPath)
        {
            var strippedData = EnsureHeaderStripped(binaryData);

            string tempDir = Path.Combine(Path.GetTempPath(), "A9Tool_FBS_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            string tempInputFile = Path.Combine(tempDir, "data.bytes");
            string expectedJsonFile = Path.Combine(tempDir, "data.json");

            try
            {
                await File.WriteAllBytesAsync(tempInputFile, strippedData);

                string flatcExe = FindFlatcExecutablePath();

                var startInfo = new ProcessStartInfo
                {
                    FileName = flatcExe,
                    Arguments = $"--json --raw-binary \"{schemaPath}\" -- \"{tempInputFile}\" --strict-json --natural-utf8",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = tempDir
                };

                _logger.LogInformation("Executing flatc: \"{FileName}\" {Arguments}", startInfo.FileName, startInfo.Arguments);

                using var process = new Process { StartInfo = startInfo };
                var sbError = new StringBuilder();
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) sbError.AppendLine(e.Data); };

                process.Start();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError("flatc failed with exit code {ExitCode}. Errors: {Error}", process.ExitCode, sbError.ToString());
                    throw new InvalidOperationException($"flatc conversion failed: {sbError}");
                }

                if (File.Exists(expectedJsonFile))
                {
                    _logger.LogInformation("Successfully converted FlatBuffers to JSON using flatc.");
                    return await File.ReadAllTextAsync(expectedJsonFile, Encoding.UTF8);
                }

                _logger.LogWarning("flatc process completed with exit code 0 but {JsonFile} was not found.", expectedJsonFile);
                return null;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to cleanup temporary directory {TempDir}", tempDir);
                }
            }
        }
    }
}
