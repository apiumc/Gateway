using System;
namespace UMC.Proxy
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    /// <summary>
    /// A executer for `cavif` which is a Encoder/converter for AVIF images.
    /// Please have the native `cavif` libraries next to your application.
    /// Grab prebuilt `cavif` from here: https://github.com/kornelski/cavif-rs/releases
    /// </summary>
    public static class AvifConverter
    {

        /// <summary>
        /// Converts PNG/JPEG to AVIF format.
        /// </summary>
        public static Task<AvifConvertResult> EncodeImage(string imageFile, string outputImageFile = null, AvifConverterOptions options = null)
        {
            string converterPath = GetConverterPath();
            string arguments = GenerateArgument(imageFile, outputImageFile, options);
            return RunEncodeProcessAsync(converterPath, arguments);
        }


        private static string GenerateArgument(string imageFile, string outputImage = null, AvifConverterOptions options = null)
        {
            List<string> list = new List<string>(8);
            if (options != null)
            {
                if (!options!.EmitMesage)
                {
                    list.Add("--quiet");
                }
                if (options!.Overwrite)
                {
                    list.Add("--overwrite");
                }
                if (options!.Speed.HasValue)
                {
                    list.Add("--speed " + options!.Speed.Value);
                }
                if (options!.Quality.HasValue)
                {
                    list.Add("--quality " + options!.Quality.Value);
                }
                if (options!.ColorRgb == true)
                {
                    list.Add("--color rgb");
                }
                if (options!.DirtyAlpha == true)
                {
                    list.Add("--dirty-alpha");
                }
            }
            else
            {
                list.Add("--quiet");
                list.Add("--overwrite");
            }
            if (outputImage != null)
            {
                list.Add("-o \"" + outputImage + "\"");
            }
            list.Add("\"" + imageFile + "\"");
            return string.Join(" ", list);
        }

        private static string GetConverterPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ".\\native\\cavif.exe";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "./native/cavif";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "./native/cavif";
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        private static Task<AvifConvertResult> RunEncodeProcessAsync(string aviflibExe, string arguments)
        {
            TaskCompletionSource<AvifConvertResult> tcs = new TaskCompletionSource<AvifConvertResult>();
            try
            {
                Process process = new Process
                {
                    StartInfo = {
                    FileName = aviflibExe,
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                },
                    EnableRaisingEvents = true
                };
                process.Exited += delegate
                {
                    string text = "";
                    while (!process.StandardOutput.EndOfStream)
                    {
                        text = text + process.StandardOutput.ReadLine() + Environment.NewLine;
                    }
                    bool success = process.ExitCode == 0;
                    tcs.SetResult(new AvifConvertResult(success, text));
                    process.Dispose();
                };
                process.Start();
            }
            catch (Exception ex)
            {
                return Task.FromResult(new AvifConvertResult(success: false, ex.Message));
            }
            return tcs.Task;
        }
    }
    public class AvifConvertResult
    {
        public bool Success { get; }

        public string Message { get; } = string.Empty;


        public AvifConvertResult()
        {
        }

        public AvifConvertResult(bool success)
        {
            Success = success;
        }

        public AvifConvertResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }
    /// <summary>
     /// Read more about options here:
     /// https://github.com/kornelski/cavif-rs
     /// </summary>
    public class AvifConverterOptions
    {
        /// <summary>
        /// Quality from 1 (worst) to 100 (best), the default value is 80. The numbers have different meaning than JPEG's quality scale. Beware when comparing codecs. There is no lossless compression support.
        /// </summary>
        public int? Quality { get; set; }

        /// <summary>
        /// Encoding speed between 1 (best, but slowest) and 10 (fastest, but a blurry mess), the default value is 4. Speeds 1 and 2 are unbelievably slow, but make files ~3-5% smaller. Speeds 7 and above degrade compression significantly, and are not recommended.
        /// </summary>
        public int? Speed { get; set; }

        /// <summary>
        /// Replace files if there's .avif already. By default the existing files are overwritten.
        /// </summary>
        public bool Overwrite { get; set; } = true;


        /// <summary>
        /// Preserve RGB values of fully transparent pixels (not recommended). By default irrelevant color of transparent pixels is cleared to avoid wasting space.
        /// </summary>
        public bool? DirtyAlpha { get; set; }

        /// <summary>
        /// Encode using RGB instead of YCbCr color space. Makes colors closer to lossless, but makes files larger. Use only if you need to avoid even smallest color shifts.
        /// </summary>
        public bool? ColorRgb { get; set; }

        /// <summary>
        /// Generate output message
        /// </summary>
        public bool EmitMesage { get; set; }
    }


}

