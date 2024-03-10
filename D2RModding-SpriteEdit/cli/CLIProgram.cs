using System;

namespace D2RModding_SpriteEdit
{
    static class CLIProgram
    {
        static void Main(string[] args)
        {
            Console.WriteLine(String.Join(" ", args));
            if (!CLI.RunCLI(args)) {
                Console.WriteLine($@"Usage:
{Environment.GetCommandLineArgs()[0]} <sprite2png|img2sprite> [FILES]...
{Environment.GetCommandLineArgs()[0]} export-frames [SPRITE-FILE] [FRAME-FILES-DIR]
{Environment.GetCommandLineArgs()[0]} import-frames [SPRITE-FILE] [FRAME-FILES]...
");
                return;
            }
        }
    }
}
