using Chetch.Arduino;
using Chetch.Arduino.Devices;

namespace Chetch.AlarmsService;

public class AlarmsService : ArduinoService<AlarmsService>
{
    public AlarmsService(ILogger<AlarmsService> Logger) : base(Logger)
    {

    }

    #region Service Lifecycle
    protected override Task Execute(CancellationToken stoppingToken)
    {
        ArduinoBoard board = new ArduinoBoard("alarms", 0x7523, 9600); //, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        board.Ready += (sender, ready) => {
            Console.WriteLine("Board is ready: {0}", ready);
            if(ready)
            {
                Console.WriteLine("Board millis: {0}", board. Millis);
                Console.WriteLine("Devices: {0}", board. DeviceCount);
                Console.WriteLine("Free memory: {0}", board.FreeMemory);
            }
        };

        var sd = new SwitchDevice(11, "gland1");
        sd.Switched += (sender, pinState) => {
            Console.WriteLine("Switch {0} has pin state {1}", sd.Name, pinState);
        };
        board.AddDevice(sd);

        //AddBoard(board);

        return base.Execute(stoppingToken);
    }
    #endregion
}
