Check the command "Include (VALUE)" in the "List of commands" section in the [CLPS2C Documentation](/CLPS2C-Documentation.txt) file for more information.<br>
All addresses listed in these example files are from the Sly Cooper Memory Addresses sheet:<br>
https://docs.google.com/spreadsheets/d/1ISxw587iICRDdaLJfLaTvJUaYkjGBReH4NY-yKN-Ip0

"Engine.txt", "Entity.txt", "Gui.txt" and "Player.txt" are example files to test the "Include" command.<br>
To include these files, write the following commands:<br>
```
Include "IncludeExample/Sly2/NTSC/Engine.txt"
Include "IncludeExample/Sly2/NTSC/Entity.txt"
Include "IncludeExample/Sly2/NTSC/Gui.txt"
Include "IncludeExample/Sly2/NTSC/Player.txt"
```
or, you can include the "All.txt" file like this:<br>
```
Include "IncludeExample/Sly2/NTSC/All.txt"
```
which will include the 4 .txt files above.

"Sly 2 - Band of Thieves (USA).sym" is an example file to test the "Include" command ability to parse a nocash symbol file.<br>
To include this file, write the following command:<br>
```
Include "IncludeExample/Sly2/NTSC/Sly 2 - Band of Thieves (USA).sym"
```