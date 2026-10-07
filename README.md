<p align="center">
<img id="charmsbarPort" src="resource/logo.png"/>
</p>
<hr />
<blockquote>
"Your most unhappy customers are your greatest source of learning."<br />— Bill Gates
</blockquote>

[![GitHub release](https://img.shields.io/github/release/Kotofey008/charms-bar-fluent/all.svg)](https://github.com/Kotofey008/charms-bar-fluent/releases)
[![Github all releases](https://img.shields.io/github/downloads/Kotofey008/charms-bar-fluent/total.svg)](https://github.com/Kotofey008/charms-bar-fluent/releases)
[![GitHub stars](https://img.shields.io/github/stars/Kotofey008/charms-bar-fluent.svg)](https://github.com/Kotofey008/charms-bar-fluent/stargazers)
[![Documentation](https://img.shields.io/badge/Docs-WIP-red.svg)](https://github.com/Kotofey008/charms-bar-fluent/wiki)
[![Issues](https://img.shields.io/github/issues/Kotofey008/charms-bar-fluent.svg)](https://github.com/Kotofey008/charms-bar-fluent/issues)

## Contents
- [About](#about)
- [Why was this created?](#why-was-this-created)
- [How does it work?](#how-does-it-work)
- [Requirements](#requirements)
- [Features](#features)
- [Supported languages](#supported-languages)
- [Screenshots](#screenshots)
- [Q&As](#qas)
- [Disclamer](#disclaimer)
- [Support](#support)
  
## About
<b>Charms Bar Fluent</b> is a Windows 11 Mica/Fluent-inspired fork of the classic Windows 8.x Charms Bar, designed to feel more native on modern Windows systems while preserving the familiar behavior and look of the original design. It brings the iconic Charms Bar experience back to Windows 10 and Windows 11 with a refreshed visual style that fits naturally into a Mica-based desktop environment.

This project is a fork from the original repository, <a href="https://github.com/Icepenguins101/charms-bar-port">Charms Bar Port</a>, with enhancements focused on making the Windows 8/8.1 experience feel more at home on Windows 11 through Fluent styling, modern colors, and closer visual integration with the OS.

## Why was this created?
As you may know, the Icepenguins101's Charms Bar Port haven't recieved any update since ~2024 and have never got any release package. That was not good at all since there's no any good alternative to it, that would've been acively maintained.

Why wouldn't I just fork, build and use it for myself only? Because Icepenguins101 made CharmsMenu and CharmsClock the way that my OpenGlass couldn't the fetch and make Charms Bar look <i>✨aero✨</i>.

For these reasons I've decided to make Charms Bar look and feel native on Windows 11. Yeah, that simple.

## How does it work?
On touch screens, you should be able to swipe from the right edge towards to bring up the Charms Bar. If you're a mouse user, swipe to the top right corner and drag your cursor down to open the Charms Bar. You can also use the keyboard shortcut Windows key + C, just like it was on Windows 8.x (but I've temporarly disabled it, sorry). Included in the Charms Bar are:
<br />
<br />
<b>Search:</b> Opens the search bar from the taskbar on Win32 programs (easily remappable to function as <a href="https://github.com/srwi/EverythingToolbar">EverythingToolbar</a>, perhaps maybe even more programs that support hotkeys), or on supported Metro/UWP apps, brings their Search charm.<br />
<b>Share:</b> Opens the share charm.<br />
<b>Start:</b> Opens the start menu/screen. If you are using <a href="https://github.com/Open-Shell/Open-Shell-Menu">Open-Shell</a>, you can remap this button to open up their start menu instead or to completely disable its functionality.<br />
<b>Devices:</b> Opens the Connect charm. On supported Metro apps, they will open the print dialog.<br />
<b>Settings:</b> Opens the Settings Metro app on Win32 programs or the Settings charm on supported Metro apps.

## Requirements
* <b>Windows 11</b> (recommended) or Windows 10
* <a href="https://dotnet.microsoft.com/en-us/download/dotnet/7.0">.NET 7.0</a>

## Features
### New:
* Native DWM window rendering
* Fully supports DWMBlurGlass and OpenGlass
* Mica by default! (only on Windows 11)
</br>
### Already included:
* Powered by Visual Studio 2022
* Based on Windows 8.1 Update 3
* Formatting-aware (uses the OS' time and date formats. If you are using 24-hour and/or date formats like "MM/DD/YYYY", Charms Bar Port will use that format. Custom formats not supported yet)
* Language-aware (automatically switches depending on your OS language)
* Uses accent colors from your system
* Network and battery status icons included
* Supports Windows 8.x-era registry keys
* Supports high contrast and light/dark mode preferences
* Fully animated to emulate Windows 8.x (can be disabled in the OS settings)
* Multi-monitor support (please read <a href="https://raw.githubusercontent.com/Kotofey008/charms-bar-fluent/main/resource/helpwanted.txt">this</a>)
* Touch-friendly
* Customizable panels (can be removed through the Registry Editor)
* Fully designable: includes a Windows 7 Metro concept, Windows 8 Developer Preview, Windows 8.1 Update 3, Windows 11 Metro concept and Windows 11 Fluent concept styles by default, or you can define your custom theme instead.
* Pin anything to the Charms Bar for easy access
* Switch between Win32 and Metro modes for the currently-focused program

## Supported languages
* English
* Russian (русский) (will be added in the next major update)
<br> I don't know any other language, so there'll be only these two.

## Screenshots (OLD | MUST BE UPDATED)
<img src="resource/preview.png"/>
<img src="resource/previewdark.png"/>
<img src="resource/previewhighcontrast.png"/>

## Download
[GitHub Releases](https://github.com/Kotofey008/charms-bar-fluent/releases)

## Q&As
Q: Is this repository the complete edition of Charms Bar Port?<br />
A: No. This project is IN DEVELOPMENT and should not be used by inexperienced users and on daily basis.
<br />
<br />
Q: How can I disable the Charms Bar hot corners without closing the program?<br />
A: This requires fiddling with the registry. I am not responsible if you mess up your system.
<br />
1. Press the “WIN+R” key combination to launch the Run dialog box, then type regedit and press enter. It’ll open the Registry Editor, and go to following key: HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ImmersiveShell\
2. Under the ImmersiveShell key, create a new key called EdgeUI.
3. Now select the newly created key “EdgeUI” and in the right-side pane, create two new DWORDs named <b>DisableTRCorner</b> and <b>DisableBRCorner</b> and set their values to 1. Alternatively, select the newly created key “EdgeUI” and in the right-side pane, create a new DWORD named <b>DisableCharmsHint</b> and set the value to 1.
4. That’s it. It’ll immediately disable the Charms Bar hot corners. You do not need to log off or restart the system. If you want to revert the change, set the values of <b>DisableTRCorner</b> and <b>DisableBRCorner</b> or <b>DisableCharmsHint</b>, to 0 or delete the <b>DisableTRCorner</b> and <b>DisableBRCorner</b>, or <b>DisableCharmsHint</b> DWORDs.
<br /></ol>
Q: When will this be released?<br />
A: Beta "releases" are already available on [Github Releases](https://github.com/Kotofey008/charms-bar-fluent/releases). This is not a stable release, keep that in mind.
<br />
<br />
Q: I'm using a touch screen, why does the Action Center always open with the charms bar?<br />
A: This is because the action center uses the same gesture. Disable it first to start using the charms bar on your tablet/touch-enabled PC.
<br />
<br />
Q: How can I switch to the Windows 10 Technical Preview style on Charms Bar Port?<br />
A: For a true Windows 10 Technical Preview style, you must stay on the Windows 8.1 Charms Bar theme and requires fiddling with the registry. I am not responsible if you mess up your system.<br />
<br />
<ol>
  <li>Press the “WIN+R” key combination to launch the Run dialog box, then type regedit and press enter. It’ll open the Registry Editor, and go to following key: 
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ImmersiveShell\</li>
  <li>Under the ImmersiveShell key, create a new key called EdgeUI.</li>
  <li>Now select the newly created key “EdgeUI” and in the right-side pane, create a new DWORD named <b>DisableSettingsCharm</b> and set the value to 1.</li>
  <li>That’s it. It’ll remove the Settings panel from the Charms Bar, emulating the style of Windows 10 Technical Preview. You do not need to log off or restart the system. If you want to revert the change, set the value to 0 or delete the <b>DisableSettingsCharm</b> DWORD.</li>
  </ol>
<br />
Q: Win+C is taken, can you use another hotkey?<br />
A: No, this is to make the experience more authentic. Close the program that is using Win+C and Charms Bar Port will use that hotkey.
<br />
<br />
Q: Is this safe to use?<br />
A: Yes, it should be. Any antivirus programs complaining should be registered as a false positive.
<br />
<br />
Q: Why are the animations stiff?<br />
A: I'm new to C#, so the animations may not match.
<br />
<br />
Q: Why does this program not support Windows 7 and Windows 8.1?<br />
A: For Windows 7 users, Charms Bar Port is meant to be used on Windows 10 and Windows 11. If you want a Charms Bar for Windows 7 look elsewhere. And for Windows 8.1 users, the Charms Bar is already on your system. There's no need to create another one.
<br />
<br />
Q: How does multi-monitor support work?<br />
A: If you have two or more monitors, moving your mouse to the next monitor(s) will increase the activeScreen parameter (activeScreen = 0 is monitor 1, activeScreen = 1 is monitor 2, vice versa), forcing the Charms Bar to be moved over to the next screen. If it's activated by mouse but not completely "spread-out", moving to the next monitor will force the Charms Bar to deactivate, to fix a bug that the original version had (if you activated it on monitor 1 and moved your cursor to monitor 2 it will stay on the screen).
<br />
<br />
Q: I'm trying to ALT+F4 the program but it won't let me. Why?<br />
A: This was meant to fix a crash bug. Use Task Manager if you want to stop the program.
<br />
<br />
Q: Why is it lagging on my machine?<br />
A: The network icon in the Charms Clock uses the command prompt to receive network information. I'm still trying to improve performance of the program.
<br />
<br />
Q: Why is it flickering on my machine?<br />
A: This is a hardware specific problem. I'm planning to outsource this program to another developer to see if they can fix this better than I can. If you want to assist, please <a href="mailto:jaydenwmontoya@icloud.com">email me</a>.
<br />
<br />
Q: Can I fork this repository to release your work now?<br />
A: Of course yes. Keep in mind that Icepenguins101 answered with "No." on this question. I don't care about that anyway ^^
<br />
<br />
Q: Will you do more ports from Windows 8.1?<br />
A: Honestly, I'd at least try to port hot-corners (real ones with cool-looking animations) and Start Screen to Windows 11. Mayne, I'll do that in the future.
<br />
<br />
Q: Will there be a version for macOS, Linux and HaikuOS?<br />
A: <b>Maybe.</b> You see, I'm bad af in coding. I <i>could try</i> to port it to macOS, but I'll most definitely never port it to Linux because it's too difficult. HaikuOS port soon™
<br />
<br />
Q: How can I contact you?<br />
A: You can <a href="mailto:contact@mail.0nl.ru">email me</a> for any assistance regarding Charms Bar Port and other products I have created.

## Disclaimer
Me (Kotik-ocelotik / Kotofey008) and Charms Bar Fluent are not an official Windows product and not in any way affiliated with Microsoft; Windows® is a registered trademark of Microsoft Corporation.

## Support
Are you a fan of the Charms Bar Port program and want to help out? here are some options...

#### Programmer
Code contributions are welcome. If you are able to port Windows 8.1 features better than I can, or if you want to improve some features (especially multi-monitor support), please <a href="mailto:contact@mail.0nl.ru">email me</a>.

#### Localization
Help translate Charms Bar Port to more languages. If there's a language that isn't present in Charms Bar Port please <a href="mailto:contact@mail.0nl.ru">email me</a>.

#### Suggestions & Bug Report
Suggest new features or file bug reports to improve Charms Bar Port, [learn more...](https://github.com/Kotofey008/charms-bar-fluent/issues)

#### Spread the word
Star this repository, leave a review of the program anywhere on your website or share it to others that want the Windows 8.x experience back!
