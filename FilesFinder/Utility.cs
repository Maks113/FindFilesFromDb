using System;
using System.Diagnostics;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace FilesFinder
{
	internal static class Utility
	{
		public static Task<Tuple<int, string>> InvokeProcessAsync(string fileName, string arguments, CancellationToken ct, string workingDirectory = "", string verb = "", string domain = null, string user = null, string password = null)
		{
			var psi = new ProcessStartInfo(fileName, arguments);
			if (Environment.OSVersion.Platform != PlatformID.Win32NT && fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
			{
				psi = new ProcessStartInfo("wine", fileName + " " + arguments);
			}

			psi.UseShellExecute = false;
			psi.WindowStyle = ProcessWindowStyle.Hidden;
			psi.ErrorDialog = false;
			psi.CreateNoWindow = true;
			psi.Verb = verb;
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;
			psi.WorkingDirectory = workingDirectory;
			if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(user) || password == null)
				return InvokeProcessAsync(psi, ct);
			psi.Domain = domain;
			psi.UserName = user;
			var secure = new SecureString();
			foreach (var c in password)
			{
				secure.AppendChar(c);
			}
			psi.Password = secure;
			psi.LoadUserProfile = false;

			return InvokeProcessAsync(psi, ct);
		}
		
		public static async Task<Tuple<int, string>> InvokeProcessAsync(ProcessStartInfo psi, CancellationToken ct)
		{
			var pi = Process.Start(psi);
			await Task.Run(() =>
			{
				while (!ct.IsCancellationRequested)
				{
					if (pi != null && pi.WaitForExit(2000)) return;
				}

				if (!ct.IsCancellationRequested) return;
				
				pi?.Kill();
				ct.ThrowIfCancellationRequested();
			}, ct);

			var textResult = await pi.StandardOutput.ReadToEndAsync();
			if (!string.IsNullOrWhiteSpace(textResult) && pi.ExitCode == 0)
				return Tuple.Create(pi.ExitCode, textResult.Trim());
			textResult = (textResult ?? "") + "\n" + await pi.StandardError.ReadToEndAsync();

			if (string.IsNullOrWhiteSpace(textResult))
			{
				textResult = string.Empty;
			}

			return Tuple.Create(pi.ExitCode, textResult.Trim());
		}
	}
}