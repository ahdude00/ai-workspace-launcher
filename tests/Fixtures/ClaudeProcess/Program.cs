// Stay alive until the integration test closes stdin or stops this process.
Console.WriteLine("ready");
string? line = Console.ReadLine();
if (args is ["--pty-test", var label])
{
    Console.WriteLine($"{label}:{line}:{Environment.GetEnvironmentVariable("AWL_PTY_TEST")}");
}
