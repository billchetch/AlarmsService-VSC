using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Chetch.Alarms;
using Chetch.ChetchXMPP;
using Chetch.Messaging;

namespace AlarmsService.Tests;

public class AlarmTestBase
{
    public const String ALARMS_SERVICE = "bbalarms.service@openfire.bb.lan";

    protected ChetchXMPPConnection cnn;

    public AlarmManager AlarmManager { get; set; } = new AlarmManager();


    public AlarmTestBase(String un, String pw)
    {
        cnn = new ChetchXMPPConnection(un, pw);
        //cnn.MessageReceived += ()
    }

    protected virtual Task ConnectClient()
    {
        Debug.WriteLine("Connecting {0} ...", cnn.Username);
        Task t = cnn.ConnectAsync();
        Debug.WriteLine("{0} connected!", cnn.Username);
        return t;
    }

    protected virtual Task DisconnectClient()
    {
        Debug.WriteLine("Disconnecting {0} ...", cnn.Username);
        Task t = cnn.DisconnectAsync();
        Debug.WriteLine("{0} disconnected!", cnn.Username);
        return t;
    }

    protected Task SendMessage(Message msg)
    {
        if(String.IsNullOrEmpty(msg.Target))
        {
            msg.Target = ALARMS_SERVICE;
        }
        msg.Sender = cnn.Username;
        return cnn.SendMessageAsync(msg);
    }

    protected void NotifyTestEnd(String targetTonotify)
    {
        var msg = ChetchXMPPMessaging.CreateNotificationMessage(1);
        msg.Target = targetTonotify;
        SendMessage(msg);
    }
}
