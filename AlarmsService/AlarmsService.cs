using Chetch.Arduino;
using Chetch.Arduino.Devices;
using Chetch.Alarms;
using Chetch.Database;
using Chetch.Messaging;
using XmppDotNet.Xmpp.XHtmlIM;
using XmppDotNet.Xmpp.Muc;
using System.Reflection.Metadata.Ecma335;

namespace Chetch.AlarmsService;

public class AlarmsService : ArduinoService<AlarmsService>, AlarmManager.IAlarmRaiser
{
    #region Constants
    public const String COMMAND_TEST_BUZZER = "test-buzzer";
    public const String COMMAND_TEST_PILOT = "test-pilot";


    public const String ARDUINO_BOARD_NAME = "alarms-board"; //for identification purposes only
    public const int BAUD_RATE = 9600;

    public const byte MASTER_SWITCH_ID = 10;
    public const byte BUZZER_ID = 11;

    public const byte PILOT_ID = 12;
    
    #endregion
    
    #region Classes and Enums
    public enum Test
    {
        NOT_TESTING,
        ALARM,
        BUZZER,
        PILOT
    }
    #endregion

    #region Properties
    public AlarmManager AlarmManager { get; set; } = new AlarmManager();

    public bool IsTesting => currentTest != Test.NOT_TESTING;
    #endregion

    #region Fields
    List<String> alarmSources = new List<String>();

    //If this master is off then any alarm hardwired to the arduino board will go directly to the buzzer rather than via the board
    //if the master is on then it will be disconnected from the buzzer. Without this the alarm could not be silenced as the silecning
    //is done by software
    SwitchDevice master = new SwitchDevice(MASTER_SWITCH_ID, "master");
    SwitchDevice buzzer = new SwitchDevice(BUZZER_ID, "buzzer");
    SwitchDevice pilot = new SwitchDevice(PILOT_ID, "pilot");

    Test currentTest = Test.NOT_TESTING;
    System.Timers.Timer testTimer = new System.Timers.Timer();
    #endregion

    public AlarmsService(ILogger<AlarmsService> Logger) : base(Logger)
    {
        ChetchDbContext.Config = Config;
    }

    
    public void RegisterAlarms()
    {
        //Console.WriteLine("Registering alarms");
        try{
            using(var context = new AlarmsDBContext())
            {
                var activeAlarms = context.Alarms.Where(x => x.Active);
                foreach(var alarm in activeAlarms)
                {
                    //Console.WriteLine("Registering alarm: {0} - {1} source: {2}", alarm.UID, alarm.Name, alarm.Source);
                    AlarmManager.RegisterAlarm(this, alarm.UID, alarm.Name);
                    if(alarm.Source != null && !alarmSources.Contains(alarm.Source))
                    {
                        alarmSources.Add(alarm.Source);
                    }
                }
                
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, e.Message);
        }
    }

    #region Service Lifecycle
    protected override Task Execute(CancellationToken stoppingToken)
    {
        //little bit convuluted this but it will call the register alrams method which will create alarms from DB
        //this is to make consistent the useage of AlarmManager which is designed for easy use in other services
        //This service is an exceptional case
        AlarmManager.AddRaiser(this);
        AlarmManager.AlarmChanged += (mgr, alarm) => {
            Console.WriteLine("Alarm {0} has changed to state {1}", alarm.ID, alarm.State);
        };
        
        //Create an arduino board and add devices
        ArduinoBoard board = new ArduinoBoard(ARDUINO_BOARD_NAME, 0x7523, BAUD_RATE); //, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        board.Ready += (sender, ready) => {
            Console.WriteLine("Board is ready: {0}", ready);
            if(!ready)
            {
                master.TurnOff();
            }
        };
        board.AddDevice(master);
        board.AddDevice(buzzer);
        board.AddDevice(pilot);

        AddBoard(board);

        //configure the test timer stuff
        testTimer.AutoReset = false;
        testTimer.Elapsed += (sender, eargs) => {
            endTest();
        };


        return base.Execute(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if(IsTesting)
        {
            endTest();
        }

        //TODO: do this properly ... placed here to remember we need to ensure that the master is turned off for sure
        /*
        try
        {
            master.TurnOff();
        }
        catch (Exception e)
        {
            Logger.LogError(e, e.Message);
        }*/
        

        return base.StopAsync(cancellationToken);
    }
    #endregion


    #region Command handling

    protected override void AddCommands()
    {
        AddCommand(AlarmManager.COMMAND_LIST_ALARMS, "List currently active alarms and their state");
        AddCommand(AlarmManager.COMMAND_TEST_ALARM, "Test <alarm>");
        AddCommand(COMMAND_TEST_BUZZER, "Test the buzzer");
        AddCommand(COMMAND_TEST_PILOT, "Test the pilot light");

        base.AddCommands();
    }

    protected override bool HandleCommandReceived(ServiceCommand command, List<object> arguments, Message response)
    {
        switch (command.Command)
        {
            case AlarmManager.COMMAND_LIST_ALARMS:
                var alarmsList = AlarmManager.Alarms.Select(x => String.Format("{0} - {1} ({2})",x.ID, x.Name, x.State)).ToList();
                response.AddValue(AlarmManager.MESSAGE_FIELD_ALARMS_LIST, alarmsList);
                return true;

            case AlarmManager.COMMAND_TEST_ALARM:
                if(arguments.Count < 1)
                {
                    throw new ArgumentException("Please specify an alarm to test");
                }
                var alarmID = arguments[0].ToString();
                var alarmState = AlarmManager.AlarmState.MODERATE;
                AlarmManager.RunTest(alarmID, alarmState, "Testing some shiii", 3000);
                return true;

            case COMMAND_TEST_BUZZER:
                runTest(Test.BUZZER, 3000);
                return true;

            case COMMAND_TEST_PILOT:
                return true;

            default:
                return base.HandleCommandReceived(command, arguments, response);
        }
    }
    #endregion

    #region Testing
    void runTest(Test testToRun, int runFor, String alarmID = null, AlarmManager.AlarmState alarmState = AlarmManager.AlarmState.CRITICAL)
    {
        if(testToRun == Test.NOT_TESTING)
        {
            throw new ArgumentException("Cannot run the test: NOT TESTING");
        }

        if(IsTesting)
        {
            throw new Exception(String.Format("Cannot run test {0} as test {1} is already running", testToRun, currentTest));
        }

        switch(testToRun){
            case Test.ALARM:
                AlarmManager.RunTest(alarmID, alarmState, "Testing cuz", runFor);
                break;

            case Test.BUZZER:
                buzzer.TurnOn();
                break;

            case Test.PILOT:
                pilot.TurnOn();
                break;

        }

        currentTest = testToRun;
        testTimer.Interval = runFor;
        testTimer.Start();
    }

    void endTest()
    {
        if(!IsTesting)return; //fail quietly

        switch(currentTest)
        {
            case Test.ALARM:
                AlarmManager.EndTest();
                break;

            case Test.BUZZER:
                buzzer.TurnOff();
                break;

            case Test.PILOT:
                pilot.TurnOff();
                break;
        }

        currentTest = Test.NOT_TESTING;
    }
    #endregion
}
