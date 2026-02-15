using System;

Console.Write("Podaj adres serwera (IP:PORT) (enter dla localhost): ");
string input = Console.ReadLine();

string ip = "127.0.0.1";
int port = 12345;

if (!string.IsNullOrWhiteSpace(input))
{
    if (input.Contains(':'))
    {
        var parts = input.Split(':');
        ip = parts[0];
        if (int.TryParse(parts[1], out int p)) port = p;
    }
    else
    {
        ip = input;
    }
}

using var game = new Client.Game1(ip, port);
game.Run();