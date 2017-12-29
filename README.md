# Cpu To Your Ears
Plays the music to correspond to the CPU load.

Requires .NET Framework 4.6.1.  This is a command line application.

* To get all possible command line parameters, execute `CpuToYourEars.exe /?`
* To connect to the CPU of your own machine, simply execute `CpuToYourEars.exe`
* To connect to the CPU of another machine, execute `CpuToYourEars.exe /m:MyMachine /dl:MyDomain\MyLogin`, where _MyMachine_ is the machine you want to connect to and _MyDomain\MyLogin_ is your domain and login, for instance CompanyDomain\userName

###### The project uses [Midi C# Toolkit](https://www.codeproject.com/Articles/6228/C-MIDI-Toolkit) by Leslie Sanford and [ShellProgress](https://www.nuget.org/packages/ShellProgress/) by Britz.
