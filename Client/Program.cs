using System;

Console.Write("Podaj IP Serwera (enter dla localhost): ");
string ip = Console.ReadLine();
if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

using var game = new Client.Game1(ip);
game.Run();