namespace ArmaReforger.Cli;

internal static class ConsoleInput
{
    public static string Read(string prompt)
    {
        Console.Write(prompt);

        return Console.ReadLine()?.Trim()
               ?? throw new InvalidOperationException("Input stream closed");
    }

    /// <summary>Ввод без эха на экран — для пароля.</summary>
    public static string ReadSecret(string prompt)
    {
        // Без терминала (pipe, отладчик) ReadKey недоступен — читаем строкой.
        if (Console.IsInputRedirected)
        {
            return Read(prompt);
        }

        Console.Write(prompt);

        var buffer = new System.Text.StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();

                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }
    }
}
