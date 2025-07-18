using System;
using Chetch.Arduino;
using Chetch.Arduino.Devices;
using Chetch.Arduino.Devices.Buzzers;

namespace Chetch.AlarmsService;

public class AlarmsBoard : ArduinoBoard
{
    #region Constants
    public const String DEFAULT_BOARD_NAME = "alarms-board"; //for identification purposes only
    public const String GENSET_ALARM_SID = "gs";
    public const String INVERTER_ALARM_SID = "iv";
    public const String HIGHWATER_ALARM_SID = "hw";
    #endregion

    #region Properties
    public SwitchGroup LocalAlarms { get; } = new SwitchGroup("Local Alarms");
    public SwitchGroup ControlSwitches { get; } = new SwitchGroup("Control Switches");
    //If this master is off then any alarm hardwired to the arduino board will go directly to the buzzer rather than via the board
    //if the master is on then it will be disconnected from the buzzer. Without this the alarm could not be silenced as the silecning
    //is done by software
    public SwitchDevice Master { get; } = new SwitchDevice("master");
    public Buzzer Buzzer { get; } = new Buzzer("buzzer");
    public SwitchDevice Pilot { get; } = new SwitchDevice("pilot");
    #endregion

    public AlarmsBoard(String sid = DEFAULT_BOARD_NAME) : base(sid)
    {
        //add indicator and control stuff
        ControlSwitches.Add(Master);
        ControlSwitches.Add(Buzzer);
        ControlSwitches.Add(Pilot);

        //add local alarms to an array for convenience
        LocalAlarms.Add(new SwitchDevice(GENSET_ALARM_SID, "Gensets"));
        LocalAlarms.Add(new SwitchDevice(INVERTER_ALARM_SID, "Inverter"));
        LocalAlarms.Add(new SwitchDevice(HIGHWATER_ALARM_SID, "High Water"));

        AddDevices(ControlSwitches);
        AddDevices(LocalAlarms);
    }

    public override void End()
    {
        ControlSwitches.TurnOff();
        base.End();
    }
}
