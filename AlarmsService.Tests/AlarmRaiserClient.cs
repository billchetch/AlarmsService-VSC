using System.Diagnostics;
using System.Threading.Tasks;
using Chetch.ChetchXMPP;

namespace AlarmsService.Tests;

[TestClass]
public sealed class AlarmRaiserClient
{
    const String USERNAME = "mactest@openfire.bb.lan";
    const String PASSWORD = "mactest";


    Task connectClient()
    {
        var cnn = new ChetchXMPPConnection(USERNAME, PASSWORD);
        return cnn.ConnectAsync();
    }

    [TestMethod]
    public async Task TestMethod1()
    {
        Debug.Print("Connecting client...");
        await connectClient();
        Debug.Print("Client connected");

    }
}
