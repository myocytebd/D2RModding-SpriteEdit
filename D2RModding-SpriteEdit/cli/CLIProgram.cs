using System;

namespace D2RModding_SpriteEdit
{
    static class CLIProgram
    {
        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                
                Console.WriteLine($"Usage: {Environment.GetCommandLineArgs()[0]} [FILES]...");
                return;
            }
            CLI.RunBatch(args);
        }
    }
}
