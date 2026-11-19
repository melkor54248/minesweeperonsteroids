namespace MinesweeperOnSteroids;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args is ["--self-test", var reportPath])
        {
            using var report = File.CreateText(reportPath);
            Console.SetOut(report);
            Console.SetError(report);
            return SelfTests.Run();
        }
        if (args is ["--ui-test", var outputDirectory])
            return DesktopTests.Run(outputDirectory);
        if (args is ["--self-test"])
            return SelfTests.Run();
        if (args.Length != 0)
        {
            MessageBox.Show("Run without arguments to play.\n\n" +
                "Developer checks:\n--self-test <report-file>\n--ui-test <output-directory>",
                "minesweeperonsteroids", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 2;
        }
        Application.Run(new MainForm());
        return 0;
    }
}
