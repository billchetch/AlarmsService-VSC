using System;
using Chetch.Database;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chetch.AlarmsService;

public class AlarmsDBContext : ChetchDbContext
{
    #region Constants
    public const String DEFAULT_DATABASE_NAME = "alarms";
    public const String ALARMS_TABLE_NAME = "alarms";
    #endregion
   
    #region DB Entities
    [Table(ALARMS_TABLE_NAME)]
    public class Alarm{
        [Column("id")]
        public long ID { get; set; }

        [Column("alarm_id")]
        public String UID { get; set; } = String.Empty;

        [Column("alarm_name")]
        public String Name { get; set; } = String.Empty;

        [Column("alarm_source")]
        public String? Source { get; set; } = String.Empty;

        [Column("active")]
        public bool Active { get; set; } = false;
    
    }

    public DbSet<Alarm> Alarms { get; set; }
    #endregion


    #region Constructors
    public AlarmsDBContext(string databaseName = DEFAULT_DATABASE_NAME, string dbConfigKey = "DBConfig") : base(databaseName, dbConfigKey)
    {

    }
    #endregion
}
