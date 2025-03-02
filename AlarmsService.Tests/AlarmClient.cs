using System;
using System.Diagnostics;
using System.Security;
using System.Threading.Tasks;
using Chetch.ChetchXMPP;
using Chetch.Messaging;
using Chetch.Alarms;

namespace AlarmsService.Tests;

[TestClass]
public class AlarmClient : AlarmTestBase
{
    public const String USERNAME = "bbalarms.client@openfire.bb.lan";
    const String PASSWORD = "bbalarms";

    CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    public AlarmClient() : base(USERNAME, PASSWORD)
    {
        
        cnn.MessageReceived += (sender, eargs) => {
            switch(eargs.Message.Type)
            {
                case MessageType.ALERT:
                    AlarmManager.Alarm alarm = eargs.Message.Get<AlarmManager.Alarm>(AlarmManager.MESSAGE_FIELD_ALARM);
                    Debug.Print("Alarm {0} sends message {1}", alarm.ID, alarm.Message);
                    cancellationTokenSource.Cancel();
                    break;

                case MessageType.COMMAND_RESPONSE:
                    if(eargs.Message.HasValue(AlarmManager.MESSAGE_FIELD_ALARMS_LIST))
                    {
                        var alarmsList = eargs.Message.GetList<AlarmManager.Alarm>(AlarmManager.MESSAGE_FIELD_ALARMS_LIST);
                        foreach(var a in alarmsList)
                        {
                            Debug.Print("{0} {1}", a.ID, a.Name);
                        }
                    }
                    break;

                case MessageType.SUBSCRIBE_RESPONSE:
                    Debug.Print("Subscribed!");
                    break;

                case MessageType.ERROR:
                    String errorMessage = eargs.Message.HasValue("ErrorMessage") ?  eargs.Message.GetString("ErrorMessage") :  eargs.Message.GetString("Message");
                    Debug.Print("Error!: {0}", errorMessage);
                    break;

                case MessageType.NOTIFICATION:
                    cancellationTokenSource.Cancel();
                    break;
            }
        };
    }

    protected override async Task ConnectClient()
    {
        try
        {
            Task t = base.ConnectClient();
            await t;
            
            Debug.WriteLine("Subscribing...");
            var msg = ChetchXMPPMessaging.CreateSubscribeMessage(ALARMS_SERVICE);
            await SendMessage(msg);
        }
        catch (Exception e)
        {
            Debug.WriteLine("Error: {0}", e.Message);
        }
    }

    [TestMethod]
    public async Task Listen()
    {
        await ConnectClient();
        await Task.Run(()=>{
            do{
                Thread.Sleep(1000);
            } while (!cancellationTokenSource.Token.IsCancellationRequested);
            
            Debug.WriteLine("Listening ended as cancellation request has been made");
        }, cancellationTokenSource.Token);
    }

    [TestMethod]
    public async Task GetAlarmsList()
    {
        await ConnectClient();
        Debug.Print("Requesting alarms list...");
        var msg = ChetchXMPPMessaging.CreateCommandMessage(AlarmManager.COMMAND_LIST_ALARMS);
        await SendMessage(msg);
        await Task.Delay(1000);
    }
}
