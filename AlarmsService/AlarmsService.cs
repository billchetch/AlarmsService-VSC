using Chetch.Arduino;
using Chetch.Arduino.Devices;
using Chetch.Arduino.Devices.Buzzers;
using Chetch.Alarms;
using Chetch.Database;
using Chetch.Messaging;
using Chetch.Utilities;

namespace Chetch.AlarmsService;

public class AlarmsService : ArduinoService<AlarmsService>, AlarmManager.IAlarmRaiser
{
    #region Constants
    public const String COMMAND_TEST_BUZZER = "test-buzzer";
    public const String COMMAND_TEST_PILOT = "test-pilot";
    public const String COMMAND_TEST_MASTER = "test-master";
    public const String COMMAND_SILENCE_BUZZER = "silence";
    public const String COMMAND_UNSILENCE_BUZZER = "unsilence";

    public const String ARDUINO_BOARD_NAME = "alarms-board"; //for identification purposes only
    public const int DEFAULT_TEST_DURATION = 5; //in seconds
    public const int GET_REMOTE_ALARMS_INTERVAL = 30; //in seconds

    public const byte MASTER_SWITCH_ID = 10;
    public const byte BUZZER_ID = 11;
    public const byte PILOT_ID = 12;
    public const byte GENSET_ALARM_ID = 13;
    public const String GENSET_ALARM_SID = "gs";
    public const byte INVERTER_ALARM_ID = 14;
    public const String INVERTER_ALARM_SID = "iv";
    public const byte HIGHWATER_ALARM_ID = 15;
    public const String HIGHWATER_ALARM_SID = "hw";

    public const String LOCAL_SOURCE_NAME = "local";

    #endregion
    
    #region Classes and Enums
    public enum Test
    {
        NOT_TESTING,
        ALARM,
        BUZZER,
        PILOT,
        MASTER
    }
    #endregion

    #region Properties
    public AlarmManager AlarmManager { get; set; } = new AlarmManager();

    public bool IsTesting => currentTest != Test.NOT_TESTING;
    #endregion

    #region Fields
    ArduinoBoard board;

    //If this master is off then any alarm hardwired to the arduino board will go directly to the buzzer rather than via the board
    //if the master is on then it will be disconnected from the buzzer. Without this the alarm could not be silenced as the silecning
    //is done by software
    SwitchDevice master = new SwitchDevice(MASTER_SWITCH_ID, "master");
    Buzzer buzzer = new Buzzer(BUZZER_ID, "buzzer");
    SwitchDevice pilot = new SwitchDevice(PILOT_ID, "pilot");

    SwitchGroup localAlarms = new SwitchGroup("Local Alarms");
    SwitchGroup controlSwitches = new SwitchGroup("Control Switches");
    List<AlarmsDBContext.Alarm> remoteAlarms = new List<AlarmsDBContext.Alarm>();
    Dictionary<String, AlarmsDBContext.Alarm> activeAlarms = new Dictionary<String, AlarmsDBContext.Alarm>();
    List<String> remoteSources = new List<String>();
    
    Test currentTest = Test.NOT_TESTING;
    System.Timers.Timer testTimer = new System.Timers.Timer();
    System.Timers.Timer getRemoteAlarmsTimer = new System.Timers.Timer();
    #endregion

    #region Constructors
    public AlarmsService(ILogger<AlarmsService> Logger) : base(Logger)
    {
        ChetchDbContext.Config = Config;

        //add local alarms to an array for convenience
        localAlarms.Add(new SwitchDevice(GENSET_ALARM_ID, GENSET_ALARM_SID, "Gensets"));
        localAlarms.Add(new SwitchDevice(INVERTER_ALARM_ID, INVERTER_ALARM_SID, "Inverter"));
        localAlarms.Add(new SwitchDevice(HIGHWATER_ALARM_ID, HIGHWATER_ALARM_SID, "High Water"));
        localAlarms.Switched += (sender, eargs) => {
                if(eargs.Switch == null)return;
                Console.WriteLine("Local Alarm {0} Switched, PinState={1}", eargs.Switch.SID, eargs.PinState);
                if(eargs.Switch.IsOn)
                {
                    AlarmManager.Raise(eargs.Switch.SID,
                            AlarmManager.AlarmState.CRITICAL,
                            "Local alarm raised"
                        );
                }
                else //assume is off
                {
                    AlarmManager.Lower(eargs.Switch.SID,
                            "Local alarm lowered"
                        );
                }
            };
        localAlarms.Ready += (sender, ready) => {
            Console.WriteLine("Local alarms ready: {0}", ready);
            if(ready)
            {
                AlarmManager.Connect(LOCAL_SOURCE_NAME);
            }
            else
            {
                AlarmManager.Disconnect(LOCAL_SOURCE_NAME);
                AlarmManager.Flush();
            }
        };

        //Retrieve alarms data from db
        using(var context = new AlarmsDBContext())
        {
            remoteAlarms = context.Alarms.Where(x => x.Active && x.Source != LOCAL_SOURCE_NAME).ToList();
            remoteSources = context.Alarms.GroupBy(x => x.Source).Select(x => x.First().Source).Where(x => x != "local").ToList();
            
            //keep a record of this from the db for convenience
            activeAlarms = context.Alarms.Where(x => x.Active).ToDictionary(x => x.SID, x => x);
        }

        //add indicator and control stuff
        controlSwitches.Add(pilot);
        controlSwitches.Add(buzzer);
        controlSwitches.Add(master);
        controlSwitches.Switched += (sender, eargs)=>{
            if(ServiceConnected && sender != null)
            {
                var message = CreateMessageForDevice(eargs.Switch, MessageType.DATA);
                message.AddValue("On", eargs.Switch.IsOn);
                Broadcast(message);
            }
        };
        controlSwitches.Ready += (sender, ready) => {
            Console.WriteLine("Control switches ready: {0}", ready);
            foreach(SwitchDevice sw in controlSwitches)
            {
                var message = CreateMessageForDevice(sw, MessageType.DATA);
                message.AddValue("On", ready ? sw.IsOn : false);
                Broadcast(message);
            }
        };

        //Set up timer for getting remote alarms
        getRemoteAlarmsTimer.Interval = GET_REMOTE_ALARMS_INTERVAL * 1000;
        getRemoteAlarmsTimer.AutoReset = true;
        getRemoteAlarmsTimer.Elapsed += (sender, eargs) =>{
            if(ServiceConnected)
            {
                foreach(var remoteSource in remoteSources)
                {
                    var msg = AlarmManager.CreateListAlarmsMessage(remoteSource);
                    SendMessage(msg);
                }
            }
        };
    }
    #endregion
    
    #region Alarm Registtration
    public void RegisterAlarms()
    {
        //Register local alarms
        foreach(var la in localAlarms)
        {
            var alm = AlarmManager.RegisterAlarm(this, la.SID, la.Name);
            if(activeAlarms.ContainsKey(la.SID))
            {
                activeAlarms[la.SID].AssignTo(alm);
            }
        }
        
        //register remote alarms (don't connect as this is done via a connection with remote source)
        //NOTE: we use the SID property of the alarm here
        foreach(var alarm in remoteAlarms)
        {
            var alm = AlarmManager.RegisterAlarm(this, alarm.SID, alarm.Name);
            alarm.AssignTo(alm);
        }
    }
    #endregion

    #region Service Lifecycle
    protected override Task Execute(CancellationToken stoppingToken)
    {
        //Respond to key alarm manager evernts
        AlarmManager.AlarmChanged += (mgr, alarm) => {
            //if the alarm has been raised and it's for real then end any current tests
            if(IsTesting && alarm.IsRaised && !alarm.IsTesting)
            {
                endTest();
            }
        }; //end of alarm changed even handler
        AlarmManager.AlarmDequeued += (sender, alarm) => {
            //if any alarm is raised then the master switch is turned on
            /*try
            {
                if(alarm.IsRaised)
                {
                    master.TurnOn();
                    pilot.TurnOn();

                    //here we determine teh conditions under which we turn the buzzer on...
                    if(alarm.State == AlarmManager.AlarmState.CRITICAL)
                    {
                        buzzer.TurnOn();
                    }
                }
                
                //if all alarms are now off then turn everything off
                if(!AlarmManager.IsAlarmRaised)
                {
                    master.TurnOff();
                    pilot.TurnOff();
                    buzzer.TurnOff();
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, e.Message);
            } */

            //now record stuff in the db if this isn't a test
            if(!alarm.IsTesting)
            {
                try
                {
                    using(var context = new AlarmsDBContext())
                    {
                        //get the db object corresponding to this alarm
                        var dbAlarm = activeAlarms[alarm.ID];

                        //update alarms table
                        dbAlarm.AssignFrom(alarm);
                        context.Update(dbAlarm);

                        //create an entry in the alarms_log table
                        context.Add(new AlarmsDBContext.LogEntry(dbAlarm.ID, alarm));
                        context.SaveChanges();
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e, e.Message);
                }    
            }

            //Finally we send this out to any subscribers on the network
            if(ServiceConnected)
            {
                try
                {
                    var alertMsg = AlarmManager.CreateAlertMessage(alarm);
                    Broadcast(alertMsg);
                    Logger.LogInformation("Broadcasting alert message for alarm {0}: {1}, code: {2}, testing: {3}", alarm.ID, alarm.State, alarm.Code, alarm.IsTesting);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, e.Message);
                }
            }
        };
        
        //Add the service as a raiser, this will call RegisterAlarms (see above)
        AlarmManager.AddRaiser(this);

        //Fire up the alarm manager
        //AlarmManager.Run(() => board != null && board.IsReady && controlSwitches.IsReady, stoppingToken);
        AlarmManager.Run(stoppingToken);
        Logger.LogInformation("Alarm Manager set up and running...");

        //Create an arduino board and add devices
        board = new ArduinoBoard(ARDUINO_BOARD_NAME);
        board.AddDevices(controlSwitches);
        board.AddDevices(localAlarms);
        
        //Now add the board to the service (this takes care of connecting it etc)
        AddBoard(board);
        Logger.LogInformation("Arduino board set up and added to the service...");

        //configure the test timer stuff ... no auto reset as it starts on start test and fires on end test
        testTimer.AutoReset = false;
        testTimer.Elapsed += (sender, eargs) => {
            endTest();
        };

        //Once connected to the following
        ServiceChanged += (sender, serviceEvent) => {
            if(serviceEvent == ServiceEvent.Connected)
            {
                //fire up the timer to check remote alarms status
                getRemoteAlarmsTimer.Start();

                //ensure subscribed to all remote sources
                foreach(var remoteSource in remoteSources)
                {
                    Subscribe(remoteSource);
                    requestAlarmsList(remoteSource);
                }

                try
                {
                    SysLogDBContext.Log(AlarmsDBContext.DEFAULT_DATABASE_NAME, 
                                            SysLogDBContext.LogEntryType.INFO,
                                            "Alarms Service connected");
                                        
                } catch {}
            }
        };
        
        return base.Execute(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if(IsTesting)
        {
            endTest();
        }

        try
        {
            controlSwitches.TurnOff();
        }
        catch (Exception e)
        {
            Logger.LogError(e, e.Message);
        }
        

        return base.StopAsync(cancellationToken);
    }
    #endregion

    #region Command and general message handling
    protected override void AddCommands()
    {
        AddCommand(AlarmManager.COMMAND_LIST_ALARMS, "List currently active alarms and their state");
        AddCommand(AlarmManager.COMMAND_TEST_ALARM, "Test <alarm>");
        AddCommand(COMMAND_TEST_BUZZER, "Test the buzzer");
        AddCommand(COMMAND_TEST_PILOT, "Test the pilot light");
        AddCommand(COMMAND_TEST_MASTER, "Test the master switch");
        AddCommand(COMMAND_SILENCE_BUZZER, "Silence buzzer for <duration> seconds");
        AddCommand(COMMAND_UNSILENCE_BUZZER, "Unsilence the buzzer");

        base.AddCommands();
    }

    protected override bool HandleCommandReceived(ServiceCommand command, List<object> arguments, Message response)
    {
        switch (command.Command)
        {
            case AlarmManager.COMMAND_LIST_ALARMS:
                AlarmManager.AddAlarmsListToMessage(response);
                return true;

            case AlarmManager.COMMAND_TEST_ALARM:
                if(arguments.Count == 0)
                {
                    throw new ArgumentException("Please specify an alarm to test");
                }
                var alarmID = arguments[0].ToString();
                var alarmState = AlarmManager.AlarmState.CRITICAL;
                int testDuration = DEFAULT_TEST_DURATION;
                if(arguments.Count > 1)
                {
                    alarmState = (AlarmManager.AlarmState)System.Convert.ToInt16(arguments[1].ToString());
                    if(alarmState == AlarmManager.AlarmState.LOWERED) //assume this is random
                    {
                        alarmState = AlarmManager.GetRandomRaisedState();
                    }
                }
                if(arguments.Count > 2)
                {
                    testDuration = System.Convert.ToInt16(arguments[2].ToString());
                }
                runTest(Test.ALARM, testDuration, alarmID, alarmState);
                return true;

            case COMMAND_TEST_BUZZER:
                runTest(Test.BUZZER, DEFAULT_TEST_DURATION);
                return true;

            case COMMAND_TEST_PILOT:
                runTest(Test.PILOT, DEFAULT_TEST_DURATION);
                return true;

            case COMMAND_TEST_MASTER:
                runTest(Test.MASTER, DEFAULT_TEST_DURATION);  
                return true;

            case COMMAND_SILENCE_BUZZER:
                if(arguments.Count < 1)
                {
                    throw new ArgumentException("Please specify a silence duration");
                }
                int silenceDuration = System.Convert.ToInt16(arguments[0].ToString());
                buzzer.Silence(silenceDuration);
                return true;

            case COMMAND_UNSILENCE_BUZZER:
                buzzer.Unsilence();
                return true;

            default:
                return base.HandleCommandReceived(command, arguments, response);
        }
    }

    protected override void PopulateStatusResponse(Message response)
    {
        base.PopulateStatusResponse(response);

        response.AddValue("AMRunningStatus", AlarmManager.RunningStatus);
    }
    protected override bool HandleCommandResponseReceived(string originalCommand, Message commandResponse, Message response)
    {
        if(originalCommand == AlarmManager.COMMAND_LIST_ALARMS)
        {
            try
            {
                AlarmManager.UpdateFromListAlarmsResponse(commandResponse);
            }
            catch (Exception e)
            {
                Logger.LogError(e, e.Message);
            }
        }
        return base.HandleCommandResponseReceived(originalCommand, commandResponse, response);
    }


    void requestAlarmsList(String remoteSource)
    {
        var msg = AlarmManager.CreateListAlarmsMessage(remoteSource);
        SendMessage(msg);
    }

    protected override bool HandleMessageReceived(Message message, Message response)
    {
        bool isFromRemoteSource = remoteSources.Contains(message.Sender);
        switch(message.Type)
        {
            case MessageType.NOTIFICATION: 
                //we expect remote source notifications here (such as a remote source has come online)
                if(IsServiceEventNotification(message) && isFromRemoteSource)
                {
                    var remoteSource = message.Sender;
                    var serviceEvent = message.Get<ServiceEvent>(MESSAGE_FIELD_SERVICE_EVENT);
                    switch(serviceEvent)
                    {
                        case ServiceEvent.Connected:
                            requestAlarmsList(remoteSource);
                            break;

                        case ServiceEvent.Disconnecting:
                            //Set alarm status to disconnected for all alarms in this remote source
                            AlarmManager.Disconnect(remoteSource);
                            break;
                    }

                    try
                    {
                        SysLogDBContext.Log(AlarmsDBContext.DEFAULT_DATABASE_NAME, 
                                                SysLogDBContext.LogEntryType.INFO,
                                               String.Format("{0}: {1}", remoteSource, serviceEvent));
                                            
                    } catch {}
                }
                break;

            case MessageType.SUBSCRIBE_RESPONSE:
                //Because we subscribe to remote sources we can capture/log the subscription ressponse here
                //For now this is just for exposition purposes
                break;

            case MessageType.ALERT:
                if(isFromRemoteSource && AlarmManager.IsAlertMessage(message))
                {
                    try
                    {
                        AlarmManager.UpdateFromAlertMessage(message);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, e.Message);
                    }
                }
                break;

        }
        return base.HandleMessageReceived(message, response);
    }
    #endregion

    #region Testing
    void runTest(Test testToRun, int runForSecs, String alarmID = null, AlarmManager.AlarmState alarmState = AlarmManager.AlarmState.CRITICAL)
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
                AlarmManager.StartTest(alarmID, alarmState, "Testing cuz");
                break;

            case Test.BUZZER:
                buzzer.TurnOn();
                break;

            case Test.PILOT:
                pilot.TurnOn();
                break;

            case Test.MASTER:
                master.TurnOn();
                break;

        }

        currentTest = testToRun;
        try
        {
            var msg = new Message(MessageType.NOTIFICATION);
            msg.AddValue("CurrentTest", currentTest);
            msg.AddValue("Testing", IsTesting);
            Broadcast(msg);
        }
        finally
        {
            testTimer.Interval = runForSecs * 1000;
            testTimer.Start();
        }
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

            case Test.MASTER:
                master.TurnOff();
                break;
        }

        try
        {
            currentTest = Test.NOT_TESTING;

            var msg = new Message(MessageType.NOTIFICATION);
            msg.AddValue("CurrentTest", currentTest);
            msg.AddValue("Testing", IsTesting);
            Broadcast(msg);
        } 
        catch (Exception e) 
        {
            Logger.LogError(e, e.Message);
        }
    }
    #endregion
}