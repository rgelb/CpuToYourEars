using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Microsoft.CommandLineHelper;
using Sanford.Multimedia.Midi;
using ShellProgress;

namespace AudioMonitor
{
    public enum Notes
    {
        C = 60,
        Cs = 61,
        D = 62,
        Ds = 63,
        E = 64,
        F = 65,
        Fs = 66,
        G = 67,
        Gs = 68,
        A = 69,
        As = 70,
        B = 71
    }

    public class Program
    {
        #region Private Variables

        private static OutputDevice outDevice;
        private static PerformanceCounter cpuCounter;
        private static Timer tmrCheckPerfCounter;

        private static readonly Chord Cmajor = new Chord(Notes.C, Notes.E, Notes.G);   // c major
        private static readonly Chord Dmajor = new Chord(Notes.D, Notes.Fs, Notes.A);  // d major
        private static readonly Chord Emajor = new Chord(Notes.E, Notes.Gs, Notes.B);  // e major
        private static readonly Chord Fmajor = new Chord(Notes.F, Notes.A, Notes.C);   // f major 
        private static readonly Chord Gmajor = new Chord(Notes.G, Notes.B, Notes.D);   // g major
        private static readonly Chord Amajor = new Chord(Notes.A, Notes.Cs, Notes.E);  // a major
        private static readonly Chord Bmajor = new Chord(Notes.B, Notes.Ds, Notes.Fs);  // b major
        
        private static Chord lastChord = null;
        private static int lastNoteOffset = 0;

        private static readonly Arguments appArgs = new Arguments();
        private static WindowsImpersonationContext impersonationContext = null;
        private static IProgressing appProgressBar = null;

        [DllImport("advapi32.dll")]
        public static extern bool LogonUser(string name, string domain, string pass, int logType, int logpv, out IntPtr pht);

        #endregion

        static void Main(string[] args)
        {
            if (!Parser.ParseArgumentsWithUsage(args, appArgs))
                Environment.Exit(-1);

            try
            {
                AskForPassword();
                SetPerfCounter();
                if (!SetupMidiOutputDevice()) return;
                SetTimer();
                SetupProgressBar();
                SetupWaitingToExit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        #region Setup Application

        private static void AskForPassword()
        {
            // Ask for password if there is a passed in Domain Login, but no password
            if (string.IsNullOrEmpty(appArgs.DomainLogin) || !string.IsNullOrEmpty(appArgs.Password)) return;

            string pass = "";
            Console.Write("Enter your password: ");
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                // Backspace Should Not Work
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    pass += key.KeyChar;
                    Console.Write("*");
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                    {
                        pass = pass.Substring(0, (pass.Length - 1));
                        Console.Write("\b \b");
                    }
                }
            }
            // Stops Receving Keys Once Enter is Pressed
            while (key.Key != ConsoleKey.Enter);

            appArgs.Password = pass;
            Console.WriteLine();
        }

        private static void SetPerfCounter()
        {
            Console.WriteLine("Connecting to Perfmon Counter...");
            if (appArgs.DomainLogin.Length > 0)
            {
                LogonUser(appArgs.Login, appArgs.Domain, appArgs.Password, 9, 0, out var ptr);
                WindowsIdentity windowsIdentity = new WindowsIdentity(ptr);
                impersonationContext = windowsIdentity.Impersonate();
            }

            cpuCounter = string.IsNullOrEmpty(appArgs.Machine) ?
                new PerformanceCounter("Processor", "% Processor Time", "_Total") :
                new PerformanceCounter("Processor", "% Processor Time", "_Total", appArgs.Machine);
        }

        private static bool SetupMidiOutputDevice()
        {
            if (OutputDevice.DeviceCount == 0)
            {
                Console.WriteLine("No MIDI output devices available.");
                return false;
            }
            else
            {
                try
                {
                    int outDeviceID = 0;
                    outDevice = new OutputDevice(outDeviceID);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return false;
                }
            }

            return true;
        }

        private static void SetTimer()
        {
            tmrCheckPerfCounter = new Timer(CheckPerfCounter, null, 100, 500);
        }

        private static void SetupProgressBar()
        {
            var factory = new ProgressBarFactory();
            appProgressBar = factory.CreateInstance(100);
        }

        private static void SetupWaitingToExit()
        {
            Console.WriteLine("Press ESC to stop");
            while (Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                ReleaseResources();
                Environment.Exit(0);
            }
        }

        #endregion

        #region Helper Methods

        private static void CheckPerfCounter(object state)
        {
            PlayBasedOnPerfCounter();
        }

        private static void PlayBasedOnPerfCounter()
        {
            float value = cpuCounter.NextValue();
            Debug.WriteLine(value);

            appProgressBar.Update((int) value);

            StopLastChord();

            ChordToPlayBasedOnValue(value, out var chordToPlay, out var octaveToPlay);
            PlayChord(chordToPlay, octaveToPlay);
        }

        private static void ChordToPlayBasedOnValue(float value, out Chord chordToPlay, out int octaveToPlay)
        {
            octaveToPlay = 0;   // set default

            if (value >= 0 && value < 10)
            {
                chordToPlay = Cmajor;
                octaveToPlay = -2;
            }
            else if (value >= 10 && value < 20)
            {
                chordToPlay = Dmajor;
                octaveToPlay = -1;
            }
            else if (value >= 20 && value < 30)
            {
                chordToPlay = Emajor;
                octaveToPlay = -1;
            }
            else if (value >= 30 && value < 40)
            {
                chordToPlay = Fmajor;
            }
            else if (value >= 40 && value < 50)
            {
                chordToPlay = Gmajor;
            }
            else if (value >= 50 && value < 60)
            {
                chordToPlay = Amajor;
            }
            else if (value >= 60 && value < 70)
            {
                chordToPlay = Bmajor;
            }
            else if (value >= 70 && value < 80)
            {
                chordToPlay = Cmajor;
                octaveToPlay = 1;
            }
            else if (value >= 80 && value < 90)
            {
                chordToPlay = Dmajor;
                octaveToPlay = 2;
            }
            else
            {
                chordToPlay = Emajor;
                octaveToPlay = 2;
            }
        }

        private static void PlayChord(Chord chord, int octave)
        {
            int noteOffset = (octave * 12);
            outDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, 0, (int)chord.Note1 + noteOffset, 127));
            outDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, 0, (int)chord.Note2 + noteOffset, 127));
            outDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, 0, (int)chord.Note3 + noteOffset, 127));

            lastChord = chord;
            lastNoteOffset = noteOffset;
        }

        private static void StopLastChord()
        {
            if (lastChord != null)
            {
                outDevice.Send(new ChannelMessage(ChannelCommand.NoteOff, 0, (int)lastChord.Note1 + lastNoteOffset, 0));
                outDevice.Send(new ChannelMessage(ChannelCommand.NoteOff, 0, (int)lastChord.Note2 + lastNoteOffset, 0));
                outDevice.Send(new ChannelMessage(ChannelCommand.NoteOff, 0, (int)lastChord.Note3 + lastNoteOffset, 0));
            }
        }

        #endregion

        #region Application Cleanup

        private static void ReleaseResources()
        {
            outDevice?.Dispose();

            try
            {
                impersonationContext?.Undo();
                impersonationContext?.Dispose();
            }
            catch
            {
                // nothing we can do
            }
        }

        #endregion
    }

    [CommandLineArguments(CaseSensitive = false)]
    public class Arguments
    {
        [Argument(ArgumentType.AtMostOnce, HelpText = "Machine Name", DefaultValue = "", LongName = "Machine", ShortName = "m")]
        public string Machine;
        [Argument(ArgumentType.AtMostOnce, HelpText = @"Domain Login, e.g CompanyDomain\username", DefaultValue = "", LongName = "DomainLogin", ShortName = "dl")]
        public string DomainLogin;
        [Argument(ArgumentType.AtMostOnce, HelpText = "Password", DefaultValue = "",  LongName = "Password", ShortName = "p")]
        public string Password;

        // helper properties
        public string Domain
        {
            get
            {
                if (string.IsNullOrEmpty(DomainLogin)) return "";

                var split = DomainLogin.Split(@"\".ToCharArray());
                if (split.Length == 2)
                    return split[0];

                return "";
            }
        }

        public string Login {
            get
            {
                if (string.IsNullOrEmpty(DomainLogin)) return "";

                var split = DomainLogin.Split(@"\".ToCharArray());
                if (split.Length == 2)
                    return split[1];

                return "";
            }
        }
    }

    public class Chord
    {
        public Chord() {}
        public Chord(Notes note1, Notes note2, Notes note3)
        {
            this.Note1 = note1;
            this.Note2 = note2;
            this.Note3 = note3;
        }
        public Notes Note1 { get; set; }
        public Notes Note2 { get; set; }
        public Notes Note3 { get; set; }
    }
}
