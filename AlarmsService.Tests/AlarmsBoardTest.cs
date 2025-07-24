using System;
using Arduino.Tests;
using Chetch.Arduino;
using Chetch.Arduino.Connections;
using Chetch.AlarmsService;
using System.IO.Compression;

namespace AlarmsService.Tests;

[TestClass]
public sealed class AlarmsBoardTest
{
    AlarmsBoard CreateBoard()
    {
        var board = new AlarmsBoard();

        board.ExceptionThrown += (sender, eargs) =>
        {
            Console.WriteLine("Board {0} throws exception: {1}", board.SID, eargs.GetException().Message);
        };

        board.Ready += (sender, ready) =>
        {
            Console.WriteLine("Board {0} ready: {1}", board.SID, ready);
        };

        board.ControlSwitches.Ready += (sender, ready) =>
        {
            Console.WriteLine("Constrol switches ready: {0}", ready);
        };


        board.Connection = Settings.GetConnection("LocalSocket");

        return board;
    }

    [TestMethod]
    public void Connect()
    {
        var board = CreateBoard();
        try
        {
            Console.WriteLine("Beginning board {0}...", board.SID);
            board.Begin();
            Console.WriteLine("Board {0} has begun!", board.SID);

            var started = DateTime.Now;
            while (!board.IsReady && (DateTime.Now - started).TotalSeconds < 10)
            {
                Thread.Sleep(1000);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            Console.WriteLine("Ending test for board {0}", board.SID);
            board.End();
            Thread.Sleep(500);
            Console.WriteLine("Board {0} has ended", board.SID);
        }
    }

    [TestMethod]
    public void LocalAlarmsRegime()
    {
        var board = CreateBoard();
        try
        {
            bool regimeStarted = false;
            bool regimeEnded = false;
            board.MessageReceived += (sender, message) =>
            {
                Console.WriteLine("<---- Message {0} received from {1}", message.Type, message.Sender);
            };

            board.NotificationReceived += (sender, eargs) =>
            {
                switch (eargs.Event)
                {
                    case ArduinoBoard.NotificationEvent.TEST_BEGUN:
                        regimeStarted = true;
                        regimeEnded = false;
                        break;
                    case ArduinoBoard.NotificationEvent.TEST_ENDED:
                        regimeStarted = false;
                        regimeEnded = true;
                        break;
                }
            };

            Console.WriteLine("Beginning board {0}...", board.SID);
            board.Begin();
            Console.WriteLine("Board {0} has begun!", board.SID);

            while (!board.IsReady)
            {
                Thread.Sleep(1000);
            }

            while (!regimeEnded)
            {
                Thread.Sleep(1000);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            Console.WriteLine("Ending test for board {0}", board.SID);
            board.End();
            Thread.Sleep(500);
            Console.WriteLine("Board {0} has ended", board.SID);
        }
    }

    [TestMethod]
    public void ControlSwitchesTest()
    {
        var board = CreateBoard();
        try
        {
            board.MessageReceived += (sender, message) =>
            {
                Console.WriteLine("<---- Message {0} received from {1}", message.Type, message.Sender);
            };

            Console.WriteLine("Beginning board {0}...", board.SID);
            board.Begin();
            Console.WriteLine("Board {0} has begun!", board.SID);

            while (!board.IsReady)
            {
                Thread.Sleep(1000);
            }

            for (int i = 0; i < 2; i++)
            {
                board.Master.TurnOn();
                Thread.Sleep(1000);
                board.Buzzer.TurnOn();
                Thread.Sleep(1000);
                board.Pilot.TurnOn();
                Thread.Sleep(1000);

                board.Master.TurnOff();
                Thread.Sleep(1000);
                board.Buzzer.TurnOff();
                Thread.Sleep(1000);
                board.Pilot.TurnOff();
                Thread.Sleep(1000);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            Console.WriteLine("Ending test for board {0}", board.SID);
            board.End();
            Thread.Sleep(500);
            Console.WriteLine("Board {0} has ended", board.SID);
        }
    }
}
