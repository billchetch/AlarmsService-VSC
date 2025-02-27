using System.Diagnostics;
using System.Threading.Tasks;
using Chetch.ChetchXMPP;
using Chetch.Alarms;

namespace AlarmsService.Tests;

[TestClass]
public sealed class AlarmRaiserClient : AlarmManager.IAlarmRaiser
{
    const String USERNAME = "mactest@openfire.bb.lan";
    const String PASSWORD = "mactest";

    const String ALARMS_SERVICE = "bbalarms.service@openfire.bb.lan";

    ChetchXMPPConnection cnn;

    public AlarmManager AlarmManager { get; set; } = new AlarmManager();

    public AlarmRaiserClient()
    {
        cnn = new ChetchXMPPConnection(USERNAME, PASSWORD);
        //cnn.MessageReceived += ()

        AlarmManager.AddRaiser(this);
        AlarmManager.AlarmChanged += (sender, alarm) => {
            Debug.Print("Alarm changed event");

            try
            {
                var msg = AlarmManager.CreateAlertMessage(alarm, ALARMS_SERVICE);
                cnn.SendMessageAsync(msg);
                Debug.Print("Sent alarm alert for {0} to alarms service {1}", alarm.ID, ALARMS_SERVICE);
            }
            catch (Exception e)
            {
                Debug.Print("Error: {0}", e.Message);
            }
            
        };
    }

    public void RegisterAlarms()
    {
        AlarmManager.RegisterAlarm(this, "gps", "GPS Alarm Test");
    }

    Task connectClient()
    {
        return cnn.ConnectAsync();
    }

    [TestMethod]
    public async Task TestMethod1()
    {
        Debug.Print("Connecting client...");
        await connectClient();
        Debug.Print("Client connected");
        AlarmManager.Raise("gps", AlarmManager.AlarmState.MODERATE, "Fuck its working");
        await Task.Delay(2000);
    }
}
