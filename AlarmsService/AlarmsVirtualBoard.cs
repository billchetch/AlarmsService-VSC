using System;
using Chetch.Arduino;
using Chetch.Messaging;

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
    }

    public AlarmsVirtualBoard() : this(new AlarmsBoard()){}
}
