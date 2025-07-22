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
    public SwitchGroup LocalAlarms { get; }
    public SwitchGroup ControlSwitches { get; }

    public PassiveSwitch GensetAlarm { get; } = new PassiveSwitch(GENSET_ALARM_SID, "Gensets");
    public PassiveSwitch InverterAlarm { get; } = new PassiveSwitch(INVERTER_ALARM_SID, "Inverter");
    public PassiveSwitch HighwaterAlarm { get; } = new PassiveSwitch(HIGHWATER_ALARM_SID, "High Water");

    //If this master is off then any alarm hardwired to the arduino board will go directly to the buzzer rather than via the board
    //if the master is on then it will be disconnected from the buzzer. Without this the alarm could not be silenced as the silecning
    //is done by software
    public ActiveSwitch Master { get; } = new ActiveSwitch("master");
    public Buzzer Buzzer { get; } = new Buzzer("buzzer");
    public ActiveSwitch Pilot { get; } = new ActiveSwitch("pilot");
    #endregion

    public AlarmsBoard(String sid = DEFAULT_BOARD_NAME) : base(sid)
    {
        //add indicator and control stuff
        ControlSwitches = new SwitchGroup("Control Switches")
        {
            Master,
            Buzzer,
            Pilot
        };

        //add local alarms to an array for convenience
        LocalAlarms = new SwitchGroup("Local Alarms")
        {
            GensetAlarm,
            InverterAlarm,
            HighwaterAlarm
        };

        AddDevices(ControlSwitches);
        AddDevices(LocalAlarms);
    }

    public override void End()
    {
        try
        {
            ControlSwitches.TurnOff();
        }
        catch (Exception){}
        base.End();
    }
}
