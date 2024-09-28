# CLPS2C (Custom Language for PlayStation 2 Cheats) - Compiler
<p style="text-align: center">
  <img width="256" height="256" src="CLPS2C-Compiler/256x256.ico">
</p>

## Description
CLPS2C is a domain-specific language, built specifically for writing PS2 cheat codes. <br>
Please, refer to the [CLPS2C Documentation](/CLPS2C-Documentation.txt) file for more information. <br>
Please, refer to the [CLPS2C Changelog](/CLPS2C-Changelog.txt) file for information about changes for each version.

## Goal and intention
The main goal of the project is to have better managment and readability when cheats become too big/complex.<br>
The app supports, among other things:
- Write all code types supported by Cheat Device / ps2rd / CodeBreaker
- Output RAW cheat lines (or Pnach-formatted cheat lines)
- Write MIPS Assembly
- Automatic calculation for how many lines to execute in "If" commands
- Support for logical AND in "If" commands
- Set local variables to be referenced by name
- Define functions with arguments and call them
- Include CLPS2C files in other CLPS2C files

## Example code
```
// Variables which will be used later
Set MapID 0x3E1110
Set JobID 0x67381C
Set Player 0x2E1E40
Set CharHP 3D4AB0
Set CharHPMax 40
Set CoinCount 3D4B00
Set MyStr "Parkour_Start\0"

// Function to teleport the player to a location
Function WriteXYZ(base, valueX, valueY, valueZ)
    WritePointerFloat base,58,30 valueX
    WritePointerFloat base,58,34 valueY
    WritePointerFloat base,58,38 valueZ
EndFunction

// Main code
If MapID =: 3 // If in world 3
    Write32 20FB1580 0x123
    If JobID !. 0xFF && CoinCount =: 0 // If not in a job and the player has 0 coins
        Call WriteXYZ(Player, 1500, 2000, 600) // Warp player on top of the house
    EndIf
    Write32 CharHP CharHPMax // Set the player's hp to 40
    WriteString 0x87310 MyStr
EndIf
```
Output:
```
E0110003 003E1110
20FB1580 00000123
E10A00FF 1067381C
E0090000 003D4B00
602E1E40 44BB8000
00020002 00000058
00000030 00000000
602E1E40 44FA0000
00020002 00000058
00000034 00000000
602E1E40 44160000
00020002 00000058
00000038 00000000
203D4AB0 00000028
20087310 6B726150
20087314 5F72756F
20087318 72617453
1008731C 00000074
```

## How to run / Troubleshooting
**IMPORTANT:** .NET Desktop Runtime 8.0 must be installed in order to run this program.<br>
https://dotnet.microsoft.com/en-us/download/dotnet/8.0

Usage:
```
CLPS2C-Compiler.exe
  -i, --input     The file to be parsed. Example: -i "C:\Users\admin\Desktop\CLPS2C\Test1.txt"
  -o, --output    The file in which the output will be written to. Example: -o "C:\Users\admin\Desktop\pcsx2\cheats\SCUS-97316_07652DD9testmod.pnach"
                  If not passed, the output file path will be defaulted to the input's folder, with the same file name as the input file, and appending "-Output" to it.
                  (e.g. "C:\Users\admin\Desktop\CLPS2C\Test1-Output.txt")
  -p, --pnach     The app, by default, produces RAW lines. Enabling this option will convert them to Pnach-formatted lines.
  -d, --dtype     The app, by default, converts "If" commands to E-type codes. Enabling this option will convert them to D-type codes.
  --help          Display an help screen.
  --version       Display version information.
```

It is recommended to use [vscode-clps2c](https://github.com/NiV-L-A/vscode-clps2c), a Visual Studio Code extension for .clps2c files.
<p style="text-align: center">
  <img src="https://raw.githubusercontent.com/NiV-L-A/vscode-CLPS2C/master/Image1.png">
</p>
<p style="text-align: center">
  <img src="https://raw.githubusercontent.com/NiV-L-A/vscode-CLPS2C/master/Image2.png">
</p>

## TO-DO
- WritePointerString, WritePointerBytes and FillBytes commands.
- If commands with logical OR (||) operator.
- The (VALUE) for the SendRaw and WriteString commands will always be printed as decimal if it's a variable. Add ":X8" or some way to force it hexadecimal.

## Build instructions
CLPS2C-Compiler uses a [modified version of keystone](https://github.com/NiV-L-A/keystone) to parse the MIPS assembly instructions.<br>
The modification is a removal of an automatic addition of a "nop" instruction after any branch and jump instructions that act as a delay slot (see [issue #405](https://github.com/keystone-engine/keystone/issues/405) ).
1. Clone the CLPS2C-Compiler repository by clicking on the "Code" button and selecting "Open with Visual Studio".
2. Download "keystone.dll" and "Keystone.Net.dll" from the ["Releases" page of this fork of keystone](https://github.com/NiV-L-A/keystone/releases).
3. Place the 2 .dll files in the CLPS2C-Compiler/CLPS2C-Compiler folder (where the .csproj file is). They should appear in the Solution Explorer window in Visual Studio.
4. Visual Studio should recognize the files and the project should accept the "Keystone" namespace.
- If keystone-related errors are still present in the project:
5. In the Solution Explorer window, expand the project entry, right click on "Dependencies" and select "Add Project Reference...".
6. In the window that pops up, click on "Browse" on the left side and then click on "Browse..." at the bottom right.
7. Add the "Keystone.Net.dll" file and make sure its checkbox is checked. Click "OK".

## Credits
Author:
- NiV-L-A

Special thanks:
- Sly Cooper Modding Discord Server: https://discord.gg/2GSXcEzPJA
- Luigi Auriemma's QuickBMS: http://aluigi.altervista.org/quickbms.htm
- Icon made by Cooper941: https://www.youtube.com/@Cooper941
- TheOnlyZac for suggesting how to handle certain scenarios: https://github.com/TheOnlyZac
- Testing done by zzamizz: https://github.com/zzamizz
- MIPS assembler engine from Keystone-engine: https://github.com/NiV-L-A/keystone
- commandline by commandlineparser: https://github.com/commandlineparser/commandline