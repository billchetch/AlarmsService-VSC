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

    protected  Task SendMessage(Message msg)
    {
        msg.Target = ALARMS_SERVICE;
        msg.Sender = cnn.Username;
        return cnn.SendMessageAsync(msg);
    }
}
