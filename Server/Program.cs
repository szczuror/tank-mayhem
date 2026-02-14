using System.Net;
using System.Net.Sockets;
using Shared;

var server = new UdpClient(12345);
Console.WriteLine("Serwer ruszył na porcie 12345...");

var connectedClients = new Dictionary<IPEndPoint, byte>();
var worldState = new Dictionary<byte, TankState>();

while (true)
{
    try
    {
        var result = await server.ReceiveAsync();
        byte[] data = result.Buffer;

        if (data.Length == 0) continue;

        byte packetType = data[0]; 

        if (packetType == 1)
        {
            HandlePosition(data, result.RemoteEndPoint);
        }
        else if (packetType == 2 || packetType == 3)
        {
            Broadcast(data, result.RemoteEndPoint);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Błąd: {ex.Message}");
    }
}
void HandlePosition(byte[] data, IPEndPoint senderEndPoint)
{
    var receivedTank = TankState.FromBytes(data);
    
    if (!connectedClients.ContainsKey(senderEndPoint))
    {
        RegisterClient(senderEndPoint, receivedTank.Id);
    }

    worldState[receivedTank.Id] = receivedTank;

    Broadcast(data, senderEndPoint);
}

void RegisterClient(IPEndPoint endPoint, byte newPlayerId)
{
    connectedClients.Add(endPoint, newPlayerId);
    Console.WriteLine($"[JOIN] Gracz {newPlayerId} dołączył z {endPoint}");

    foreach (var existingTank in worldState.Values)
    {
        if (existingTank.Id == newPlayerId) continue;
        byte[] syncData = existingTank.ToBytes();
        server.SendAsync(syncData, syncData.Length, endPoint);
    }
}

void Broadcast(byte[] data, IPEndPoint excludeEndPoint)
{
    foreach (var clientEP in connectedClients.Keys)
    {
        if (clientEP.Equals(excludeEndPoint)) continue;
        server.SendAsync(data, data.Length, clientEP);
    }
}