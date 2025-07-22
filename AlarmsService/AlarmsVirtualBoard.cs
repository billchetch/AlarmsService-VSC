using System;
using Chetch.Arduino;
using Chetch.Messaging;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;

namespace Chetch.AlarmsService;

public class AlarmsVirtualBoard : ArduinoVirtualBoard
{

    public AlarmsVirtualBoard(AlarmsBoard board) : base(board)
    {
        var regime = new ArduinoVirtualBoard.Regime("local-alarms");
        regime.RepeatCount = 1;
        regime.AddMessage(board.GensetAlarm, MessageType.DATA, "PinState", 1);
        regime.AddDelay(1000);
        regime.AddMessage(board.InverterAlarm, MessageType.DATA, "PinState", 1);
        regime.AddDelay(1000);
        regime.AddMessage(board.HighwaterAlarm, MessageType.DATA, "PinState", 1);
        regime.AddDelay(1000);
        regime.AddMessage(board.GensetAlarm, MessageType.DATA, "PinState", 0);
        regime.AddDelay(1000);
        regime.AddMessage(board.InverterAlarm, MessageType.DATA, "PinState", 0);
        regime.AddDelay(1000);
        regime.AddMessage(board.HighwaterAlarm, MessageType.DATA, "PinState", 0);
        AddRegime(regime);

        foreach (var la in board.LocalAlarms)
        {
            regime = new ArduinoVirtualBoard.Regime(la.SID + "-alarm");
            regime.RepeatCount = 3;
            regime.AddMessage(la, MessageType.DATA, "PinState", 1);
            regime.AddDelay(1000);
            regime.AddMessage(la, MessageType.DATA, "PinState", 0);
            regime.AddDelay(1000);
            AddRegime(regime);
        }

        regime = new ArduinoVirtualBoard.Regime(board.GensetAlarm.SID + "-alarm-20s");
        regime.RepeatCount = 2;
        regime.AddMessage(board.GensetAlarm, MessageType.DATA, "PinState", 1);
        regime.AddDelay(20000);
        regime.AddMessage(board.GensetAlarm, MessageType.DATA, "PinState", 0);
        regime.AddDelay(2000);
        AddRegime(regime);

        //other stuff
        board.RequestStatusTimerInterval = 5;

    }

    public AlarmsVirtualBoard() : this(new AlarmsBoard()){}
}
