using System;
using Chetch.Database;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Chetch.Alarms;

namespace Chetch.AlarmsService;

public class AlarmsDBContext : ChetchDbContext
{
    #region Constants
    public const String DEFAULT_DATABASE_NAME = "alarms";
    public const String ALARMS_TABLE_NAME = "alarms";
    public const String LOG_TABLE_NAME = "alarm_log";

    #endregion
   
    
    #region DB Entities
    [Table(ALARMS_TABLE_NAME)]
    public class Alarm{
        [Column("id")]
        public long ID { get; set; }

        [Column("alarm_id")]
        public String SID { get; set; } = String.Empty;

        [Column("alarm_name")]
        public String Name { get; set; } = String.Empty;

        [Column("alarm_source")]
        public String Source { get; set; } = String.Empty;

        [Column("active")]
        public bool Active { get; set; } = false;

        [Column("last_raised")]
        public DateTime? LastRaised { get; set; }

        [Column("last_lowered")]
        public DateTime? LastLowered { get; set; }

        [Column("last_disabled")]
        public DateTime? LastDisabled { get; set; }

        public bool HasBeenRaised => LastRaised != null; //default(DateTime);
        public bool HasBeenLowered => LastLowered != null; //default(DateTime);

        public TimeSpan? RaisedFor => HasBeenRaised && HasBeenLowered ? (LastRaised - LastLowered) : default(TimeSpan);

        public void AssignFrom(AlarmManager.Alarm alarm)
        {
            LastRaised = alarm.LastRaised;
            LastLowered = alarm.LastLowered;
            LastDisabled = alarm.LastDisabled;
        }

        public void AssignTo(AlarmManager.Alarm alarm)
        {
            alarm.Source = Source;
            alarm.LastRaised = LastRaised;
            alarm.LastLowered = LastLowered;
            alarm.LastDisabled = LastDisabled;
        }
    }


    [Table(LOG_TABLE_NAME)]
    public class LogEntry
    {
        [Column("id")]
        public long ID { get; set; }

        [Column("alarm_id")]
        public long AlarmID { get; set; }

        [Column("alarm_state")]
        public String AlarmStateRaw {get; internal set; } = String.Empty;

        [NotMapped]
        public AlarmManager.AlarmState AlarmState 
        { 
            get
            {
                return (AlarmManager.AlarmState)Enum.Parse(typeof(AlarmManager.AlarmState), AlarmStateRaw);
            } 
            set
            {
                AlarmStateRaw = value.ToString();
            }
        }

        [Column("alarm_message")]
        public String AlarmMessage { get; set; } = String.Empty;

        [Column("alarm_code")]
        public int AlarmCode { get; set; } = AlarmManager.NO_CODE;

        public LogEntry(){}

        public LogEntry(long alarmID, AlarmManager.Alarm alarm)
        {
            AlarmID = alarmID;
            AlarmState = alarm.State; //.ToString();
            AlarmMessage = alarm.Message;
            AlarmCode = alarm.Code;
        }

    }

    public DbSet<Alarm> Alarms { get; set; }

    public DbSet<LogEntry> AlarmLog { get; set; }
    #endregion


    #region Constructors
    public AlarmsDBContext(string databaseName = DEFAULT_DATABASE_NAME, string dbConfigKey = "DBConfig") : base(databaseName, dbConfigKey)
    {
        //Uncomment to assert utc time to prevent possibility of bad configuration
        //AssertDateTimeUtc();
    }
    #endregion
}
