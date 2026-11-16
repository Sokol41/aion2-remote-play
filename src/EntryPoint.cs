using System.Reflection;
using System.Security.Principal;

internal static class EntryPoint
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            string sdk=Path.Combine(AppContext.BaseDirectory,"HIDMaestro.Core.dll");
            if(!File.Exists(sdk))throw new FileNotFoundException("HIDMaestro.Core.dll отсутствует. Распакуйте архив целиком. / Extract the entire ZIP; HIDMaestro.Core.dll is missing.");
            if(AssemblyName.GetAssemblyName(sdk).Version?.ToString()!="2.1.0.0")
                throw new InvalidOperationException("Нужен HIDMaestro SDK 2.1.0. / HIDMaestro SDK 2.1.0 is required.");
            if(!args.Contains("--self-test") && !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                throw new UnauthorizedAccessException("Запустите от администратора. / Run as administrator.");
            Program.Run(args);
        }
        catch(Exception e)
        {
            Environment.ExitCode=1;
            if(args.Contains("--self-test"))File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"selftest.txt"),e.ToString());
            else MessageBox.Show("Не удалось запустить приложение. / Unable to start.\n\n"+e.Message+"\n\nТребуются / Required: Windows x64, .NET Desktop Runtime 10, installed HIDMaestro 2.1.0.","aion2-remote-play v6",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
}
