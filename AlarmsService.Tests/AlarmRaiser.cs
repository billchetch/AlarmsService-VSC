using System.Diagnostics;
using System.Threading.Tasks;
using Chetch.ChetchXMPP;
using Chetch.Alarms;

namespace AlarmsService.Tests;

[TestClass]
public sealed class AlarmRaiser : AlarmTestBase, AlarmManager.IAlarmRaiser
{
    public const String USERNAME = "bbalarms.raiser@openfire.bb.lan";
    const String PASSWORD = "8ulan8aru";

    public AlarmRaiser() : base(USERNAME, PASSWORD)
    {
        AlarmManager.AddRaiser(this);
        AlarmManager.AlarmChanged += (sender, alarm) => {
            Debug.Print("Alarm changed event");

            try
            {
                var msg = AlarmManager.CreateAlertMessage(alarm);
                SendMessage(msg);
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
        AlarmManager.RegisterAlarm(this, "test", "Test alarm kak Test");
    }

    [TestMethod]
    public async Task RaiseAndLowerTestAlarm()
    {
        await ConnectClient();

        NotifyServiceEvent(Chetch.AlarmsService.AlarmsService.ServiceEvent.Connected);
        Thread.Sleep(3000);

        var rand = new Random();
        for (int i = 0; i < 2; i++)
        {
            var msg = String.Format("Test alarm raised {0}", i + 1);
            AlarmManager.Raise("test", AlarmManager.AlarmState.MODERATE, msg);
            await Task.Delay(rand.Next(1000, 2000));
            AlarmManager.Lower("test", "Lowered bro");
            await Task.Delay(rand.Next(1000, 2000));
        }

        //NotifyTestEnd(AlarmClient.USERNAME);
        NotifyServiceEvent(Chetch.AlarmsService.AlarmsService.ServiceEvent.Disconnecting);
        Thread.Sleep(1000);
        await DisconnectClient();
        Thread.Sleep(1000);
    }
}
